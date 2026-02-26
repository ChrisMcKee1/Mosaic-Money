using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;

namespace MosaicMoney.Api.Authentication;

public static class ClerkAuthenticationExtensions
{
    public static IServiceCollection AddClerkJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var clerkOptions = configuration
            .GetSection(ClerkAuthenticationOptions.SectionName)
            .Get<ClerkAuthenticationOptions>()
            ?? new ClerkAuthenticationOptions();

        services
            .AddOptions<ClerkAuthenticationOptions>()
            .Bind(configuration.GetSection(ClerkAuthenticationOptions.SectionName))
            .ValidateDataAnnotations()
            .Validate(options => Uri.TryCreate(options.Issuer, UriKind.Absolute, out _),
                "Authentication:Clerk:Issuer must be an absolute URL.")
            .ValidateOnStart();

        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(jwtOptions =>
            {
                jwtOptions.Authority = clerkOptions.Issuer;
                jwtOptions.MapInboundClaims = false;
                jwtOptions.RequireHttpsMetadata = true;

                jwtOptions.TokenValidationParameters = new TokenValidationParameters
                {
                    NameClaimType = ClaimTypes.NameIdentifier,
                    RoleClaimType = ClaimTypes.Role,
                    ValidateIssuer = true,
                    ValidIssuer = clerkOptions.Issuer,
                    ValidateAudience = !string.IsNullOrWhiteSpace(clerkOptions.Audience),
                    ValidAudience = clerkOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    ValidateLifetime = true,
                };
            });

        var requireJwtPolicy = new AuthorizationPolicyBuilder(JwtBearerDefaults.AuthenticationScheme)
            .RequireAuthenticatedUser()
            .Build();

        services
            .AddAuthorizationBuilder()
            .SetDefaultPolicy(requireJwtPolicy);

        return services;
    }
}
