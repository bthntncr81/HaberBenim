using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;
using HaberPlatform.Api.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HaberPlatform.Api.Controllers;

[ApiController]
[Route("api/v1/admin/users")]
[Authorize(Roles = "Admin")]
public class AdminUsersController : ControllerBase
{
    private readonly AppDbContext _db;
    private readonly PasswordHasher<string> _passwordHasher = new();

    public AdminUsersController(AppDbContext db)
    {
        _db = db;
    }

    [HttpGet]
    public async Task<IActionResult> GetUsers()
    {
        var users = await _db.Users
            .Include(u => u.UserRoles)
            .ThenInclude(ur => ur.Role)
            .OrderByDescending(u => u.CreatedAtUtc)
            .Select(u => new UserListDto(
                u.Id,
                u.Email,
                u.DisplayName,
                u.IsActive,
                u.CreatedAtUtc,
                u.UserRoles.Select(ur => ur.Role.Name).ToArray()
            ))
            .ToListAsync();

        return Ok(users);
    }

    [HttpPost]
    public async Task<IActionResult> CreateUser([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || 
            string.IsNullOrWhiteSpace(request.DisplayName) ||
            string.IsNullOrWhiteSpace(request.TempPassword))
        {
            return BadRequest(new { error = "Email, display name, and temp password are required" });
        }

        var email = request.Email.ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email))
        {
            return Conflict(new { error = "User with this email already exists" });
        }

        var roles = await _db.Roles
            .Where(r => request.Roles.Contains(r.Name))
            .ToListAsync();

        if (roles.Count != request.Roles.Length)
        {
            return BadRequest(new { error = "One or more roles do not exist" });
        }

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = request.DisplayName,
            PasswordHash = _passwordHasher.HashPassword(email, request.TempPassword),
            IsActive = true,
            CreatedAtUtc = DateTime.UtcNow
        };

        foreach (var role in roles)
        {
            user.UserRoles.Add(new UserRole
            {
                UserId = user.Id,
                RoleId = role.Id
            });
        }

        _db.Users.Add(user);
        await _db.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetUsers),
            new UserListDto(
                user.Id,
                user.Email,
                user.DisplayName,
                user.IsActive,
                user.CreatedAtUtc,
                request.Roles
            )
        );
    }
}

