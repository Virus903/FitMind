using FitMind.Data;
using FitMind.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace FitMind.Services;

/// <summary>Пишет только безопасное описание административного действия.</summary>
public sealed class AdminAuditService(ApplicationDbContext db, IHttpContextAccessor context)
{
    public async Task WriteAsync(string action, string entityType, object? entityId, string summary)
    {
        var actorId = context.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
        db.AdminAuditLogs.Add(new AdminAuditLog
        {
            ActorUserId = actorId,
            OccurredAt = DateTime.UtcNow,
            Action = action,
            EntityType = entityType,
            EntityId = entityId?.ToString(),
            Summary = summary.Length > 400 ? summary[..400] : summary,
            IpAddress = context.HttpContext?.Connection.RemoteIpAddress?.ToString()
        });
        await db.SaveChangesAsync();
    }
}
