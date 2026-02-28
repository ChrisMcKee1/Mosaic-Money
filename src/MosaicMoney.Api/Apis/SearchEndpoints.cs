using Microsoft.EntityFrameworkCore;
using MosaicMoney.Api.Contracts.V1;
using MosaicMoney.Api.Data;
using MosaicMoney.Api.Domain.Ledger;
using MosaicMoney.Api.Domain.Ledger.Embeddings;
using Pgvector;
using Pgvector.EntityFrameworkCore;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MosaicMoney.Api.Apis;

public static class SearchEndpoints
{
    private static readonly char[] QueryTokenSeparators =
    [
        ' ', '\t', '\r', '\n', '-', '_', '.', ',', ';', ':', '!', '?', '/', '\\', '(', ')', '[', ']', '{', '}', '"', '\''
    ];

    private static readonly Regex AmountTokenRegex = new(
        @"(?<!\d)-?\$?\d{1,6}(?:,\d{3})*(?:\.\d{1,2})?(?!\d)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static RouteGroupBuilder MapSearchEndpoints(this RouteGroupBuilder group)
    {
        group.MapGet("/search/transactions", async (
            MosaicMoneyDbContext dbContext,
            ITransactionEmbeddingGenerator embeddingGenerator,
            string query,
            int limit = 10,
            CancellationToken cancellationToken = default) =>
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Results.Ok(Array.Empty<TransactionDto>());
            }

            var normalizedQuery = query.Trim();
            var embeddingArray = await embeddingGenerator.GenerateEmbeddingAsync(normalizedQuery, cancellationToken);
            var queryEmbedding = new Vector(embeddingArray);

            var boundedLimit = Math.Clamp(limit, 1, 50);
            var candidateLimit = Math.Clamp(boundedLimit * 4, boundedLimit, 200);
            var queryTerms = ExpandQueryTerms(normalizedQuery);
            var amountTerms = ExtractAmountTerms(normalizedQuery);

            var semanticCandidates = await GetSemanticTransactionCandidatesAsync(
                dbContext,
                queryEmbedding,
                candidateLimit,
                cancellationToken);

            var lexicalCandidates = await GetLexicalTransactionCandidatesAsync(
                dbContext,
                queryTerms,
                amountTerms,
                candidateLimit,
                cancellationToken);

            var rankedCandidates = RankHybridTransactionCandidates(semanticCandidates, lexicalCandidates, boundedLimit);

            return Results.Ok(rankedCandidates.Select(ApiEndpointHelpers.MapTransaction).ToList());
        });

        group.MapGet("/search/categories", async (
            MosaicMoneyDbContext dbContext,
            ITransactionEmbeddingGenerator embeddingGenerator,
            string query,
            CategoryOwnerType? ownerType,
            Guid? householdId,
            Guid? ownerUserId,
            int limit = 10,
            CancellationToken cancellationToken = default) =>
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                return Results.Ok(Array.Empty<object>());
            }

            var embeddingArray = await embeddingGenerator.GenerateEmbeddingAsync(query, cancellationToken);
            var queryEmbedding = new Vector(embeddingArray);

            var boundedLimit = Math.Clamp(limit, 1, 50);

            var scopedSubcategoryIdsQuery = ApplyCategoryOwnershipScope(
                    dbContext.Subcategories.AsNoTracking(),
                    ownerType,
                    householdId,
                    ownerUserId)
                .Select(x => x.Id);

            // Find subcategories from similar transactions
            var semanticSubcategoryIds = await dbContext.EnrichedTransactions
                .AsNoTracking()
                .Where(x =>
                    x.DescriptionEmbedding != null
                    && x.SubcategoryId != null
                    && scopedSubcategoryIdsQuery.Contains(x.SubcategoryId.Value))
                .OrderBy(x => x.DescriptionEmbedding!.CosineDistance(queryEmbedding))
                .Select(x => x.SubcategoryId!.Value)
                .Take(boundedLimit * 2)
                .ToListAsync(cancellationToken);

            // Also find subcategories by name
            var textMatchSubcategories = await ApplyCategoryOwnershipScope(
                    dbContext.Subcategories
                        .AsNoTracking()
                        .Include(x => x.Category),
                    ownerType,
                    householdId,
                    ownerUserId)
                .Where(x => EF.Functions.ILike(x.Name, $"%{query}%") || EF.Functions.ILike(x.Category.Name, $"%{query}%"))
                .Take(boundedLimit)
                .ToListAsync(cancellationToken);

            var combinedIds = semanticSubcategoryIds.Concat(textMatchSubcategories.Select(x => x.Id)).Distinct().Take(boundedLimit).ToList();

            var finalSubcategories = await ApplyCategoryOwnershipScope(
                    dbContext.Subcategories
                        .AsNoTracking()
                        .Include(x => x.Category),
                    ownerType,
                    householdId,
                    ownerUserId)
                .Where(x => combinedIds.Contains(x.Id))
                .ToListAsync(cancellationToken);

            // Sort to prioritize text matches, then semantic matches
            var sortedSubcategories = finalSubcategories
                .OrderByDescending(x => textMatchSubcategories.Any(t => t.Id == x.Id))
                .ThenBy(x => combinedIds.IndexOf(x.Id))
                .Select(x => new
                {
                    Id = x.Id,
                    Name = x.Name,
                    CategoryId = x.CategoryId,
                    CategoryName = x.Category.Name,
                    IsBusinessExpense = x.IsBusinessExpense
                })
                .ToList();

            return Results.Ok(sortedSubcategories);
        });

        return group;
    }

    internal static IReadOnlyList<EnrichedTransaction> RankHybridTransactionCandidates(
        IReadOnlyList<EnrichedTransaction> semanticCandidates,
        IReadOnlyList<EnrichedTransaction> lexicalCandidates,
        int limit)
    {
        var boundedLimit = Math.Clamp(limit, 1, 50);
        var semanticDenominator = Math.Max(semanticCandidates.Count, 1);
        var lexicalDenominator = Math.Max(lexicalCandidates.Count, 1);

        var mergedById = new Dictionary<Guid, HybridTransactionCandidate>();

        for (var index = 0; index < semanticCandidates.Count; index++)
        {
            var transaction = semanticCandidates[index];
            var semanticScore = 0.85d * ((semanticDenominator - index) / (double)semanticDenominator);
            if (!mergedById.TryGetValue(transaction.Id, out var existing))
            {
                mergedById[transaction.Id] = new HybridTransactionCandidate(transaction, semanticScore, index, null);
                continue;
            }

            mergedById[transaction.Id] = existing with
            {
                Score = existing.Score + semanticScore,
                SemanticRank = Math.Min(existing.SemanticRank ?? index, index),
            };
        }

        for (var index = 0; index < lexicalCandidates.Count; index++)
        {
            var transaction = lexicalCandidates[index];
            var lexicalScore = 0.15d * ((lexicalDenominator - index) / (double)lexicalDenominator);
            if (!mergedById.TryGetValue(transaction.Id, out var existing))
            {
                mergedById[transaction.Id] = new HybridTransactionCandidate(transaction, lexicalScore, null, index);
                continue;
            }

            mergedById[transaction.Id] = existing with
            {
                Score = existing.Score + lexicalScore,
                LexicalRank = Math.Min(existing.LexicalRank ?? index, index),
            };
        }

        return mergedById.Values
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.SemanticRank ?? int.MaxValue)
            .ThenBy(x => x.LexicalRank ?? int.MaxValue)
            .ThenByDescending(x => x.Transaction.TransactionDate)
            .ThenBy(x => x.Transaction.Id)
            .Take(boundedLimit)
            .Select(x => x.Transaction)
            .ToList();
    }

    internal static IReadOnlyList<string> ExpandQueryTerms(string query)
    {
        var normalizedQuery = EmbeddingTextHasher.Normalize(query);
        if (normalizedQuery.Length == 0)
        {
            return [];
        }

        var terms = new List<string>();

        AddTerm(normalizedQuery);

        var queryTokens = normalizedQuery.Split(
            QueryTokenSeparators,
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var token in queryTokens)
        {
            var normalizedToken = EmbeddingTextHasher.Normalize(token).ToLowerInvariant();
            if (normalizedToken.Length < 2)
            {
                continue;
            }

            AddTerm(normalizedToken);
        }

        return terms;

        void AddTerm(string candidate)
        {
            var trimmed = candidate.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                return;
            }

            if (terms.Any(existing => string.Equals(existing, trimmed, StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            terms.Add(trimmed);
        }
    }

    internal static IReadOnlyList<decimal> ExtractAmountTerms(string query)
    {
        var normalizedQuery = EmbeddingTextHasher.Normalize(query);
        if (normalizedQuery.Length == 0)
        {
            return [];
        }

        var parsed = new HashSet<decimal>();

        foreach (Match match in AmountTokenRegex.Matches(normalizedQuery))
        {
            if (!match.Success)
            {
                continue;
            }

            var valueToken = match.Value.Replace("$", string.Empty, StringComparison.Ordinal)
                .Replace(",", string.Empty, StringComparison.Ordinal)
                .Trim();

            if (!decimal.TryParse(
                    valueToken,
                    NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                    CultureInfo.InvariantCulture,
                    out var parsedValue))
            {
                continue;
            }

            var rounded = decimal.Round(parsedValue, 2, MidpointRounding.AwayFromZero);
            parsed.Add(rounded);

            if (rounded > 0m)
            {
                parsed.Add(-rounded);
            }
        }

        return parsed.ToList();
    }

    private static async Task<List<EnrichedTransaction>> GetSemanticTransactionCandidatesAsync(
        MosaicMoneyDbContext dbContext,
        Vector queryEmbedding,
        int candidateLimit,
        CancellationToken cancellationToken)
    {
        var isNpgsqlProvider = dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        if (!isNpgsqlProvider)
        {
            return [];
        }

        return await dbContext.EnrichedTransactions
            .AsNoTracking()
            .Include(x => x.Splits)
            .Where(x => x.DescriptionEmbedding != null)
            .OrderBy(x => x.DescriptionEmbedding!.CosineDistance(queryEmbedding))
            .ThenByDescending(x => x.TransactionDate)
            .ThenBy(x => x.Id)
            .Take(candidateLimit)
            .ToListAsync(cancellationToken);
    }

    private static async Task<List<EnrichedTransaction>> GetLexicalTransactionCandidatesAsync(
        MosaicMoneyDbContext dbContext,
        IReadOnlyList<string> queryTerms,
        IReadOnlyList<decimal> amountTerms,
        int candidateLimit,
        CancellationToken cancellationToken)
    {
        if (queryTerms.Count == 0 && amountTerms.Count == 0)
        {
            return [];
        }

        var isNpgsqlProvider = dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.OrdinalIgnoreCase) == true;
        if (!isNpgsqlProvider)
        {
            var allTransactions = await dbContext.EnrichedTransactions
                .AsNoTracking()
                .Include(x => x.Splits)
                .ToListAsync(cancellationToken);

            return allTransactions
                .Where(transaction =>
                    queryTerms.Any(term => MatchesTerm(transaction, term))
                    || amountTerms.Contains(transaction.Amount))
                .OrderByDescending(x => x.TransactionDate)
                .ThenBy(x => x.Id)
                .Take(candidateLimit)
                .ToList();
        }

        var seenIds = new HashSet<Guid>();
        var lexicalMatches = new List<EnrichedTransaction>();
        foreach (var term in queryTerms)
        {
            var likePattern = $"%{term}%";
            var termMatches = await dbContext.EnrichedTransactions
                .AsNoTracking()
                .Include(x => x.Splits)
                .Where(x =>
                    EF.Functions.ILike(x.Description, likePattern)
                    || (x.UserNote != null && EF.Functions.ILike(x.UserNote, likePattern))
                    || (x.AgentNote != null && EF.Functions.ILike(x.AgentNote, likePattern)))
                .OrderByDescending(x => EF.Functions.ILike(x.Description, likePattern))
                .ThenByDescending(x => x.TransactionDate)
                .ThenBy(x => x.Id)
                .Take(candidateLimit)
                .ToListAsync(cancellationToken);

            foreach (var match in termMatches)
            {
                if (!seenIds.Add(match.Id))
                {
                    continue;
                }

                lexicalMatches.Add(match);
                if (lexicalMatches.Count >= candidateLimit)
                {
                    return lexicalMatches;
                }
            }
        }

        if (amountTerms.Count > 0 && lexicalMatches.Count < candidateLimit)
        {
            var remaining = candidateLimit - lexicalMatches.Count;
            var amountMatches = await dbContext.EnrichedTransactions
                .AsNoTracking()
                .Include(x => x.Splits)
                .Where(x => amountTerms.Contains(x.Amount))
                .OrderByDescending(x => x.TransactionDate)
                .ThenBy(x => x.Id)
                .Take(remaining)
                .ToListAsync(cancellationToken);

            foreach (var match in amountMatches)
            {
                if (!seenIds.Add(match.Id))
                {
                    continue;
                }

                lexicalMatches.Add(match);
                if (lexicalMatches.Count >= candidateLimit)
                {
                    return lexicalMatches;
                }
            }
        }

        return lexicalMatches;
    }

    private static bool MatchesTerm(EnrichedTransaction transaction, string term)
    {
        return ContainsOrdinalIgnoreCase(transaction.Description, term)
               || ContainsOrdinalIgnoreCase(transaction.UserNote, term)
               || ContainsOrdinalIgnoreCase(transaction.AgentNote, term);
    }

    private static bool ContainsOrdinalIgnoreCase(string? source, string term)
    {
        return !string.IsNullOrWhiteSpace(source)
               && source.Contains(term, StringComparison.OrdinalIgnoreCase);
    }

    private sealed record HybridTransactionCandidate(
        EnrichedTransaction Transaction,
        double Score,
        int? SemanticRank,
        int? LexicalRank);

    private static IQueryable<Subcategory> ApplyCategoryOwnershipScope(
        IQueryable<Subcategory> query,
        CategoryOwnerType? ownerType,
        Guid? householdId,
        Guid? ownerUserId)
    {
        var resolvedOwnerType = ownerType ?? CategoryOwnerType.Platform;
        var activeQuery = query.Where(x => !x.IsArchived && !x.Category.IsArchived);

        return resolvedOwnerType switch
        {
            CategoryOwnerType.Platform => activeQuery.Where(x => x.Category.OwnerType == CategoryOwnerType.Platform),
            CategoryOwnerType.HouseholdShared when householdId.HasValue => activeQuery.Where(x =>
                x.Category.OwnerType == CategoryOwnerType.HouseholdShared
                && x.Category.HouseholdId == householdId.Value),
            CategoryOwnerType.User when householdId.HasValue && ownerUserId.HasValue => activeQuery.Where(x =>
                x.Category.OwnerType == CategoryOwnerType.User
                && x.Category.HouseholdId == householdId.Value
                && x.Category.OwnerUserId == ownerUserId.Value),
            _ => activeQuery.Where(_ => false),
        };
    }
}
