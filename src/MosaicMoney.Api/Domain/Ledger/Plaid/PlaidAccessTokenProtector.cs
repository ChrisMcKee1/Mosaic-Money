using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.DataProtection;

namespace MosaicMoney.Api.Domain.Ledger.Plaid;

public sealed class PlaidAccessTokenProtector(IDataProtectionProvider dataProtectionProvider)
{
    private readonly IDataProtector protector = dataProtectionProvider.CreateProtector("MosaicMoney.Api.Plaid.AccessToken.v1");

    public string Protect(string accessToken)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            throw new ArgumentException("Access token is required.", nameof(accessToken));
        }

        return protector.Protect(accessToken.Trim());
    }

    public static string ComputeFingerprint(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim()));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
