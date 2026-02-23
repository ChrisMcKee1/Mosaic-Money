using System.Security.Cryptography;
using System.Text;

namespace MosaicMoney.Api.Domain.Ledger.Embeddings;

public static class EmbeddingTextHasher
{
    public static string ComputeHash(string? value)
    {
        var normalized = Normalize(value);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public static string Normalize(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? string.Empty
            : value.Trim();
    }
}
