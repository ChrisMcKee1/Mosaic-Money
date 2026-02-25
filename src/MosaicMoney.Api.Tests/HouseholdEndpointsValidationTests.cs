using MosaicMoney.Api.Apis;
using MosaicMoney.Api.Contracts.V1;
using Xunit;

namespace MosaicMoney.Api.Tests;

public sealed class HouseholdEndpointsValidationTests
{
    [Fact]
    public void ValidateCreateHouseholdInviteRequest_RejectsUnknownRole()
    {
        var request = new CreateHouseholdInviteRequest
        {
            Email = "invitee@example.com",
            Role = "SuperAdmin",
        };

        var errors = HouseholdEndpoints.ValidateCreateHouseholdInviteRequest(request);

        Assert.Contains(errors, x => x.Field == nameof(CreateHouseholdInviteRequest.Role));
    }

    [Theory]
    [InlineData("Member")]
    [InlineData("Admin")]
    [InlineData("Owner")]
    [InlineData(" member ")]
    public void ValidateCreateHouseholdInviteRequest_AcceptsSupportedRoles(string role)
    {
        var request = new CreateHouseholdInviteRequest
        {
            Email = "invitee@example.com",
            Role = role,
        };

        var errors = HouseholdEndpoints.ValidateCreateHouseholdInviteRequest(request);

        Assert.DoesNotContain(errors, x => x.Field == nameof(CreateHouseholdInviteRequest.Role));
    }

    [Fact]
    public void ValidateCreateHouseholdInviteRequest_RequiresValidEmail()
    {
        var request = new CreateHouseholdInviteRequest
        {
            Email = "not-an-email",
            Role = "Member",
        };

        var errors = HouseholdEndpoints.ValidateCreateHouseholdInviteRequest(request);

        Assert.Contains(errors, x => x.Field == nameof(CreateHouseholdInviteRequest.Email));
    }
}
