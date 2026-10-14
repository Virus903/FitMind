using FitMind.Data;
using FitMind.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FitMind.Tests;

[Collection("Account flow")]
public sealed class ProgramsUnifiedTests(FitMindFactory factory)
{
    [Fact]
    public async Task Seeder_fills_unified_program_tables_from_json()
    {
        _ = factory.CreateClient();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        // Программы базы из JSONData (номера 1–88): 10 тренировочных дней и 30 упражнений в них.
        var seeded = db.Programs.Where(x => x.OwnerId == null && x.Kind == ProgramKind.Workout && x.Id <= 88);
        Assert.Equal(88, await seeded.CountAsync());
        var items = await db.ProgramItems.Where(x => x.ProgramId <= 88 && x.Program!.OwnerId == null).Include(x => x.Exercises).AsNoTracking().ToListAsync();
        Assert.Equal(10, items.Count);
        Assert.All(items, x => Assert.Equal(ProgramItemType.Workout, x.Type));
        Assert.Equal(30, items.Sum(x => x.Exercises.Count));
        Assert.False(await db.Exercises.AnyAsync(x => x.OwnerId != null && x.Id <= 441 && x.SourceExerciseId == null && x.Name == ""));
    }

    [Fact]
    public async Task Add_to_my_copies_base_program_with_items()
    {
        var user = await factory.CreateCompletedUserAsync($"add-to-my-{Guid.NewGuid():N}@example.test", "valid-password-123");
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var source = await db.Programs.Include(x => x.Items).ThenInclude(x => x.Exercises).Where(x => x.OwnerId == null && x.Items.Any(i => i.Exercises.Any())).OrderBy(x => x.Id).FirstAsync();
        source.Active = true; await db.SaveChangesAsync();
        using var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var login = await client.GetStringAsync("/Account/Login");
        var token = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Match(login, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string> { ["Email"] = user.Email!, ["Password"] = "valid-password-123", ["__RequestVerificationToken"] = token }));
        var details = await client.GetStringAsync($"/Programs/Details/{source.Id}");
        token = System.Net.WebUtility.HtmlDecode(System.Text.RegularExpressions.Regex.Match(details, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"").Groups[1].Value);
        var add = await client.PostAsync("/Programs/AddToMy", new FormUrlEncodedContent(new Dictionary<string, string> { ["id"] = source.Id.ToString(), ["__RequestVerificationToken"] = token }));
        Assert.Equal(System.Net.HttpStatusCode.Redirect, add.StatusCode);
        var copy = await db.Programs.AsNoTracking().Include(x => x.Items).ThenInclude(x => x.Exercises).SingleAsync(x => x.OwnerId == user.Id && x.SourceProgramId == source.Id);
        Assert.Equal(source.Items.Count, copy.Items.Count);
        Assert.Equal(source.Items.Sum(x => x.Exercises.Count), copy.Items.Sum(x => x.Exercises.Count));
        Assert.All(copy.Items, x => { Assert.NotNull(x.DayOfWeek); Assert.NotNull(x.Time); });
        Assert.False(copy.Active);
    }
}
