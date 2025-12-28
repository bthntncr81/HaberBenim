using System.Diagnostics;
using System.Security.Claims;
using HaberPlatform.Api.Data;
using HaberPlatform.Api.Entities;

namespace HaberPlatform.Api.Middleware;

public class AuditLogMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<AuditLogMiddleware> _logger;

    public AuditLogMiddleware(RequestDelegate next, ILogger<AuditLogMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context, AppDbContext db)
    {
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            await _next(context);
        }
        finally
        {
            stopwatch.Stop();
            await LogRequestAsync(context, db, stopwatch.ElapsedMilliseconds);
        }
    }

    private async Task LogRequestAsync(HttpContext context, AppDbContext db, long durationMs)
    {
        try
        {
            var userId = context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var userEmail = context.User.FindFirst(ClaimTypes.Email)?.Value;

            var auditLog = new AuditLog
            {
                Id = Guid.NewGuid(),
                CreatedAtUtc = DateTime.UtcNow,
                UserId = userId,
                UserEmail = userEmail,
                Method = context.Request.Method,
                Path = context.Request.Path.Value ?? "/",
                StatusCode = context.Response.StatusCode,
                IpAddress = GetClientIpAddress(context),
                UserAgent = context.Request.Headers.UserAgent.ToString(),
                DurationMs = durationMs
            };

            // Truncate long values
            if (auditLog.Path.Length > 2048)
                auditLog.Path = auditLog.Path[..2048];
            if (auditLog.UserAgent?.Length > 500)
                auditLog.UserAgent = auditLog.UserAgent[..500];

            db.AuditLogs.Add(auditLog);
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            // Never break the request pipeline due to logging failure
            _logger.LogError(ex, "Failed to write audit log");
        }
    }

    private static string? GetClientIpAddress(HttpContext context)
    {
        // Check for forwarded header first (for proxies/load balancers)
        var forwardedFor = context.Request.Headers["X-Forwarded-For"].FirstOrDefault();
        if (!string.IsNullOrEmpty(forwardedFor))
        {
            return forwardedFor.Split(',')[0].Trim();
        }

        return context.Connection.RemoteIpAddress?.ToString();
    }
}

public static class AuditLogMiddlewareExtensions
{
    public static IApplicationBuilder UseAuditLogging(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<AuditLogMiddleware>();
    }
}

