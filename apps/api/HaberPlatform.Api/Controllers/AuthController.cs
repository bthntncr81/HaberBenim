using System.Security.Claims;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Models;
using HaberPlatform.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly JwtService _jwtService;
    private readonly PasswordHasher<string> _passwordHasher = new();

    public AuthController(AppDbContext db, JwtService jwtService)
    {
        _db = db;
        _jwtService = jwtService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest(new { error = "Email and password are required" });
        }

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email == request.Email.ToLowerInvariant());

        if (user == null)
        {
            return Unauthorized(new { error = "Invalid email or password" });
        }

        if (!user.IsActive)
        {
            return Unauthorized(new { error = "User account is inactive" });
        }

        var result = _passwordHasher.VerifyHashedPassword(
            user.Email, 
            user.PasswordHash, 
            request.Password);

        if (result == PasswordVerificationResult.Failed)
        {
            return Unauthorized(new { error = "Invalid email or password" });
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();
        var (token, expiresAt) = _jwtService.GenerateToken(user, roles);

        return Ok(new LoginResponse(
            AccessToken: token,
            ExpiresAtUtc: expiresAt,
            User: new UserDto(
                Id: user.Id,
                Email: user.Email,
                DisplayName: user.DisplayName,
                Roles: roles
            )
        ));
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<IActionResult> GetCurrentUser()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        
        if (string.IsNullOrEmpty(userIdClaim) || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new { error = "Invalid token" });
        }

        var user = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user == null)
        {
            return NotFound(new { error = "User not found" });
        }

        var roles = user.UserRoles.Select(ur => ur.Role.Name).ToArray();

        return Ok(new UserDto(
            Id: user.Id,
            Email: user.Email,
            DisplayName: user.DisplayName,
            Roles: roles
        ));
    }
}

