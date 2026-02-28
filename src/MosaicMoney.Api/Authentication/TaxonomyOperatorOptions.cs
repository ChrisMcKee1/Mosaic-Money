using System.ComponentModel.DataAnnotations;

namespace MosaicMoney.Api.Authentication;

public sealed class TaxonomyOperatorOptions
{
    public const string SectionName = "TaxonomyOperator";
    public const string OperatorApiKeyHeaderName = "X-Mosaic-Operator-Key";

    [MaxLength(512)]
    public string ApiKey { get; init; } = string.Empty;

    [MaxLength(4000)]
    public string AllowedAuthSubjectsCsv { get; init; } = string.Empty;
}
