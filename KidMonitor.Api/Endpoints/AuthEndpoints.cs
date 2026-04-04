using System.Security.Claims;
using KidMonitor.Api.Data;
using KidMonitor.Api.Models;
using KidMonitor.Api.Services;
using Microsoft.EntityFrameworkCore;

namespace KidMonitor.Api.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/auth");

        group.MapPost("/register", Register);
        group.MapPost("/login", Login);
        group.MapPost("/refresh", Refresh);
        group.MapPost("/logout", Logout).RequireAuthorization();
    }

    // POST /auth/register
    private static async Task<IResult> Register(
        RegisterRequest req,
        AppDbContext db,
        TokenService tokens)
    {
        if (string.IsNullOrWhiteSpace(req.Email) || string.IsNullOrWhiteSpace(req.Password))
            return Results.BadRequest(new { error = "Email and password are required." });

        if (await db.Parents.AnyAsync(p => p.Email == req.Email.ToLowerInvariant()))
            return Results.Conflict(new { error = "An account with that email already exists." });

        var (refreshToken, refreshExpiry) = tokens.GenerateRefreshToken();

        var parent = new Parent
        {
            Email = req.Email.ToLowerInvariant().Trim(),
            DisplayName = req.DisplayName?.Trim() ?? req.Email.Split('@')[0],
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            RefreshToken = refreshToken,
            RefreshTokenExpiry = refreshExpiry,
        };

        db.Parents.Add(parent);
        await db.SaveChangesAsync();

        return Results.Ok(new AuthResponse(
            tokens.GenerateAccessToken(parent),
            refreshToken,
            parent.Id,
            parent.Email,
            parent.DisplayName));
    }

    // POST /auth/login
    private static async Task<IResult> Login(
        LoginRequest req,
        AppDbContext db,
        TokenService tokens)
    {
        var parent = await db.Parents.FirstOrDefaultAsync(
            p => p.Email == req.Email.ToLowerInvariant().Trim());

        if (parent is null || !BCrypt.Net.BCrypt.Verify(req.Password, parent.PasswordHash))
            return Results.Unauthorized();

        var (refreshToken, refreshExpiry) = tokens.GenerateRefreshToken();
        parent.RefreshToken = refreshToken;
        parent.RefreshTokenExpiry = refreshExpiry;
        await db.SaveChangesAsync();

        return Results.Ok(new AuthResponse(
            tokens.GenerateAccessToken(parent),
            refreshToken,
            parent.Id,
            parent.Email,
            parent.DisplayName));
    }

    // POST /auth/refresh
    private static async Task<IResult> Refresh(
        RefreshRequest req,
        AppDbContext db,
        TokenService tokens)
    {
        var parent = await db.Parents.FirstOrDefaultAsync(
            p => p.RefreshToken == req.RefreshToken);

        if (parent is null || parent.RefreshTokenExpiry < DateTime.UtcNow)
            return Results.Unauthorized();

        var (newRefreshToken, newRefreshExpiry) = tokens.GenerateRefreshToken();
        parent.RefreshToken = newRefreshToken;
        parent.RefreshTokenExpiry = newRefreshExpiry;
        await db.SaveChangesAsync();

        return Results.Ok(new AuthResponse(
            tokens.GenerateAccessToken(parent),
            newRefreshToken,
            parent.Id,
            parent.Email,
            parent.DisplayName));
    }

    // POST /auth/logout
    private static async Task<IResult> Logout(
        ClaimsPrincipal user,
        AppDbContext db)
    {
        var parentId = Guid.Parse(user.FindFirstValue(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)!);
        var parent = await db.Parents.FindAsync(parentId);
        if (parent is not null)
        {
            parent.RefreshToken = null;
            parent.RefreshTokenExpiry = null;
            await db.SaveChangesAsync();
        }
        return Results.NoContent();
    }
}

// Request / response records
public record RegisterRequest(string Email, string Password, string? DisplayName);
public record LoginRequest(string Email, string Password);
public record RefreshRequest(string RefreshToken);
public record AuthResponse(string AccessToken, string RefreshToken, Guid ParentId, string Email, string DisplayName);
