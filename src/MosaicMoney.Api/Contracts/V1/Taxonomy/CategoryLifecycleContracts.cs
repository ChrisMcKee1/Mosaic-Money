using System.ComponentModel.DataAnnotations;

namespace MosaicMoney.Api.Contracts.V1;

public sealed record CategorySubcategoryDto(
    Guid Id,
    Guid CategoryId,
    string Name,
    bool IsBusinessExpense,
    int DisplayOrder,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime LastModifiedAtUtc,
    DateTime? ArchivedAtUtc);

public sealed record CategoryLifecycleDto(
    Guid Id,
    string Name,
    int DisplayOrder,
    bool IsSystem,
    string OwnerType,
    Guid? HouseholdId,
    Guid? OwnerUserId,
    bool IsArchived,
    DateTime CreatedAtUtc,
    DateTime LastModifiedAtUtc,
    DateTime? ArchivedAtUtc,
    IReadOnlyList<CategorySubcategoryDto> Subcategories);

public sealed class CategoryScopeQueryRequest
{
    [Required]
    [MaxLength(40)]
    public string Scope { get; init; } = "Platform";

    public Guid? HouseholdId { get; init; }

    public Guid? OwnerUserId { get; init; }

    public bool IncludeArchived { get; init; }
}

public sealed class CreateCategoryRequest
{
    [Required]
    [MaxLength(100)]
    public string Name { get; init; } = string.Empty;

    [Range(0, 100000)]
    public int? DisplayOrder { get; init; }

    [Required]
    [MaxLength(40)]
    public string Scope { get; init; } = "User";

    public Guid? HouseholdId { get; init; }

    public Guid? OwnerUserId { get; init; }
}

public sealed class UpdateCategoryRequest
{
    [MaxLength(100)]
    public string? Name { get; init; }
}

public sealed class DeleteCategoryRequest
{
    public bool AllowLinkedTransactions { get; init; }
}

public sealed class ReorderCategoriesRequest
{
    [Required]
    [MaxLength(40)]
    public string Scope { get; init; } = "User";

    public Guid? HouseholdId { get; init; }

    public Guid? OwnerUserId { get; init; }

    [Required]
    [MinLength(1)]
    public IReadOnlyList<Guid> CategoryIds { get; init; } = [];

    public DateTime? ExpectedLastModifiedAtUtc { get; init; }
}

public sealed class CreateSubcategoryRequest
{
    [Required]
    public Guid CategoryId { get; init; }

    [Required]
    [MaxLength(120)]
    public string Name { get; init; } = string.Empty;

    public bool IsBusinessExpense { get; init; }

    [Range(0, 100000)]
    public int? DisplayOrder { get; init; }
}

public sealed class UpdateSubcategoryRequest
{
    [MaxLength(120)]
    public string? Name { get; init; }

    public bool? IsBusinessExpense { get; init; }

    [Range(0, 100000)]
    public int? DisplayOrder { get; init; }
}

public sealed class ReparentSubcategoryRequest
{
    [Required]
    public Guid TargetCategoryId { get; init; }

    [Range(0, 100000)]
    public int? DisplayOrder { get; init; }
}

public sealed class DeleteSubcategoryRequest
{
    public bool AllowLinkedTransactions { get; init; }
}