using Microsoft.EntityFrameworkCore;

namespace FitMind.Data;

public static class DatabaseSchemaLifecycle
{
    public static Task EnsureReadyAsync(ApplicationDbContext db) => db.Database.MigrateAsync();
}
