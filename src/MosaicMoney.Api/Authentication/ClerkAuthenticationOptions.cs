using System.ComponentModel.DataAnnotations;

namespace MosaicMoney.Api.Authentication;

public sealed class ClerkAuthenticationOptions
{
    public const string SectionName = "Authentication:Clerk";

    [Required]
    public string Issuer { get; init; } = string.Empty;

    public string SecretKey { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;
}
