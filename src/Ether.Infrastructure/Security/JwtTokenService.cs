using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Ether.Application.Abstractions;
using Ether.Contracts.Configuration;
using Ether.Domain.Accounts;

using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Ether.Infrastructure.Security;

/// <summary>
/// HMAC-SHA256 JWT issuer/validator for access, refresh and game tokens
/// (Blueprint v5.0 §47/§84). Token purpose is carried in the <c>use</c> claim so
/// one token kind can never be used as another.
/// </summary>
public sealed class JwtTokenService : ITokenService
{
    private const string UseClaim = "use";
    private const string CharacterIdClaim = "characterId";
    private const string AccessUse = "access";
    private const string RefreshUse = "refresh";
    private const string GameUse = "game";

    private readonly AuthenticationOptions _options;
    private readonly TimeProvider _timeProvider;

    public JwtTokenService(IOptions<AuthenticationOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public IssuedToken CreateAccessToken(AccountId accountId) =>
        Create(accountId, AccessUse, TimeSpan.FromMinutes(_options.AccessTokenLifetimeMinutes), characterId: null);

    public IssuedToken CreateRefreshToken(AccountId accountId) =>
        Create(accountId, RefreshUse, TimeSpan.FromDays(_options.RefreshTokenLifetimeDays), characterId: null);

    public IssuedToken CreateGameToken(AccountId accountId, Guid characterId) =>
        Create(accountId, GameUse, TimeSpan.FromMinutes(_options.GameTokenLifetimeMinutes), characterId);

    public AccountId? ValidateRefreshToken(string token)
    {
        var status = ValidateDetailed(token, RefreshUse, out var claims);
        return status == TokenValidationStatus.Valid ? claims?.AccountId : null;
    }

    public GameTokenValidation ValidateGameToken(string token)
    {
        var status = ValidateDetailed(token, GameUse, out var claims);

        if (status != TokenValidationStatus.Valid || claims is null)
        {
            return GameTokenValidation.Failure(status == TokenValidationStatus.Valid ? TokenValidationStatus.Invalid : status);
        }

        if (claims.Value.CharacterId is null)
        {
            return GameTokenValidation.Failure(TokenValidationStatus.Invalid);
        }

        return GameTokenValidation.Valid(new GameTokenClaims(claims.Value.AccountId, claims.Value.CharacterId.Value));
    }

    private IssuedToken Create(AccountId accountId, string use, TimeSpan lifetime, Guid? characterId)
    {
        var now = _timeProvider.GetUtcNow();
        var expiresAt = now.Add(lifetime);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, accountId.Value.ToString()),
            new(UseClaim, use),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        if (characterId is not null)
        {
            claims.Add(new Claim(CharacterIdClaim, characterId.Value.ToString()));
        }

        var credentials = new SigningCredentials(SigningKey(), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now.UtcDateTime,
            expires: expiresAt.UtcDateTime,
            signingCredentials: credentials);

        return new IssuedToken(new JwtSecurityTokenHandler().WriteToken(token), expiresAt);
    }

    private TokenValidationStatus ValidateDetailed(
        string token,
        string expectedUse,
        out (AccountId AccountId, Guid? CharacterId)? claims)
    {
        claims = null;

        if (string.IsNullOrWhiteSpace(token))
        {
            return TokenValidationStatus.Invalid;
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = SigningKey(),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };

        ClaimsPrincipal principal;
        try
        {
            principal = new JwtSecurityTokenHandler().ValidateToken(token, parameters, out _);
        }
        catch (SecurityTokenExpiredException)
        {
            return TokenValidationStatus.Expired;
        }
        catch (Exception exception) when (exception is SecurityTokenException or ArgumentException)
        {
            return TokenValidationStatus.Invalid;
        }

        if (!string.Equals(principal.FindFirst(UseClaim)?.Value, expectedUse, StringComparison.Ordinal))
        {
            return TokenValidationStatus.WrongPurpose;
        }

        var subject = principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value
                      ?? principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;

        if (!Guid.TryParse(subject, out var accountGuid) || accountGuid == Guid.Empty)
        {
            return TokenValidationStatus.Invalid;
        }

        Guid? characterId = null;
        var characterValue = principal.FindFirst(CharacterIdClaim)?.Value;
        if (Guid.TryParse(characterValue, out var characterGuid) && characterGuid != Guid.Empty)
        {
            characterId = characterGuid;
        }

        claims = (new AccountId(accountGuid), characterId);
        return TokenValidationStatus.Valid;
    }

    private SymmetricSecurityKey SigningKey() =>
        new(Encoding.UTF8.GetBytes(_options.ResolveSigningKey()));
}
