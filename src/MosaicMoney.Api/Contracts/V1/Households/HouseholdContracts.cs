using System.ComponentModel.DataAnnotations;

namespace MosaicMoney.Api.Contracts.V1;

public sealed class CreateHouseholdRequest
{
    [Required, MaxLength(200)]
    public string Name { get; init; } = string.Empty;
}

public sealed record HouseholdDto(Guid Id, string Name, DateTime CreatedAtUtc);

public sealed class CreateHouseholdInviteRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MaxLength(32)]
    public string Role { get; init; } = "Member";
}

public sealed class AcceptHouseholdInviteRequest
{
    [MaxLength(200)]
    public string? DisplayName { get; init; }
}

public sealed class UpdateAccountSharingPresetRequest
{
    [Required]
    [MaxLength(16)]
    public string Preset { get; init; } = "Mine";
}

public sealed record HouseholdMemberDto(
    Guid Id,
    Guid HouseholdId,
    string DisplayName,
    string? ExternalUserKey,
    string MembershipStatus,
    string Role,
    DateTime? InvitedAtUtc,
    DateTime? ActivatedAtUtc,
    DateTime? RemovedAtUtc);

public sealed record HouseholdInviteDto(
    Guid Id,
    Guid HouseholdId,
    string Email,
    string Role,
    string MembershipStatus,
    DateTime? InvitedAtUtc,
    DateTime? RemovedAtUtc);

public sealed record HouseholdAccountAccessSummaryDto(
    Guid AccountId,
    Guid HouseholdId,
    string AccountName,
    string? InstitutionName,
    bool IsActive,
    string CurrentMemberAccessRole,
    string CurrentMemberVisibility,
    string SharingPreset,
    DateTime LastModifiedAtUtc);