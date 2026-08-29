using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using QuotesApi.Data;
using QuotesApi.Models;

namespace QuotesApi.Extensions;

public class LoginRequest
{
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RefreshRequest
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

public static class AuthEndpointExtensions
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth");

        group.MapPost("/login", async (LoginRequest request, IOptions<JwtOptions> jwtOptions, QuoteDbContext dbContext) =>
        {
            var jwt = jwtOptions.Value;

            // Simple mock login
            if (request.Username != "testuser" || request.Password != "password")
                return Results.Unauthorized();

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwt.Key);
            var jwtId = Guid.NewGuid().ToString();

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, request.Username),
                    new Claim(JwtRegisteredClaimNames.Jti, jwtId),
                    new Claim(ClaimTypes.NameIdentifier, request.Username),
                    new Claim("scope", "quotes.write")
                }),
                Expires = DateTime.UtcNow.Add(jwt.AccessTokenLifetime),
                Issuer = jwt.Issuer,
                Audience = jwt.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var accessToken = tokenHandler.WriteToken(token);

            var refreshToken = new RefreshToken
            {
                JwtId = jwtId,
                UserId = request.Username,
                CreationDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddMonths(1),
                Token = Guid.NewGuid().ToString("N")
            };

            dbContext.RefreshTokens.Add(refreshToken);
            await dbContext.SaveChangesAsync();

            return Results.Ok(new { Token = accessToken, RefreshToken = refreshToken.Token });
        });

        group.MapPost("/refresh", async (RefreshRequest request, IOptions<JwtOptions> jwtOptions, QuoteDbContext dbContext) =>
        {
            var jwt = jwtOptions.Value;

            var storedRefreshToken = await dbContext.RefreshTokens
                .FirstOrDefaultAsync(x => x.Token == request.RefreshToken);

            if (storedRefreshToken == null)
                return Results.Unauthorized();

            // REUSE DETECTION: If token is already used or invalidated, revoke ALL tokens for this user
            if (storedRefreshToken.Used || storedRefreshToken.Invalidated)
            {
                var allTokens = await dbContext.RefreshTokens
                    .Where(x => x.UserId == storedRefreshToken.UserId)
                    .ToListAsync();
                foreach (var t in allTokens)
                {
                    t.Invalidated = true;
                }
                await dbContext.SaveChangesAsync();
                return Results.Unauthorized(); // Chain revoked
            }

            if (storedRefreshToken.ExpiryDate < DateTime.UtcNow)
                return Results.Unauthorized();

            // Mark as used
            storedRefreshToken.Used = true;
            await dbContext.SaveChangesAsync();

            // Generate new tokens
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(jwt.Key);
            var newJwtId = Guid.NewGuid().ToString();

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(JwtRegisteredClaimNames.Sub, storedRefreshToken.UserId),
                    new Claim(JwtRegisteredClaimNames.Jti, newJwtId),
                    new Claim(ClaimTypes.NameIdentifier, storedRefreshToken.UserId),
                    new Claim("scope", "quotes.write")
                }),
                Expires = DateTime.UtcNow.Add(jwt.AccessTokenLifetime),
                Issuer = jwt.Issuer,
                Audience = jwt.Audience,
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            var accessToken = tokenHandler.WriteToken(token);

            var newRefreshToken = new RefreshToken
            {
                JwtId = newJwtId,
                UserId = storedRefreshToken.UserId,
                CreationDate = DateTime.UtcNow,
                ExpiryDate = DateTime.UtcNow.AddMonths(1),
                Token = Guid.NewGuid().ToString("N")
            };

            dbContext.RefreshTokens.Add(newRefreshToken);
            await dbContext.SaveChangesAsync();

            return Results.Ok(new { Token = accessToken, RefreshToken = newRefreshToken.Token });
        });

        return endpoints;
    }
}

