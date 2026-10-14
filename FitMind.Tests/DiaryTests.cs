using System.Net;
using System.Text.RegularExpressions;
using FitMind.Data;
using FitMind.Models;
using FitMind.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FitMind.Tests;

public sealed class NutritionCalculationTests
{
    [Fact]
    public void Nutrients_are_scaled_from_weight()
    {
        var item = new DayEntryFoodItem { Grams = 200, CaloriesPer100 = 250, ProteinPer100 = 15, FatPer100 = 10, CarbsPer100 = 25 };
        Assert.Equal(500, NutritionHelper.ItemCalories(item));
        Assert.Equal(30, NutritionHelper.ItemProtein(item));
        Assert.Equal(20, NutritionHelper.ItemFat(item));
        Assert.Equal(50, NutritionHelper.ItemCarbs(item));
    }
}

[Collection("Account flow")]
public sealed class DiaryScheduleTests(FitMindFactory factory)
{
    [Fact]
    public void Test_database_is_not_the_working_database()
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(factory.TestDatabaseName, db.Database.GetDbConnection().Database);
        Assert.NotEqual("FitMind", db.Database.GetDbConnection().Database);
    }

    [Fact]
    public async Task Active_nutrition_and_recovery_plans_sync_without_duplicates()
    {
        var user = await factory.CreateCompletedUserAsync($"plans-{Guid.NewGuid():N}@example.test", "valid-password-123");
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var nutrition = new FitnessProgram { OwnerId = user.Id, Kind = ProgramKind.Nutrition, Name = "Питание", Active = true, CreatedAt = DateTime.UtcNow };
        var recovery = new FitnessProgram { OwnerId = user.Id, Kind = ProgramKind.Sleep, Name = "Восстановление", Active = true, CreatedAt = DateTime.UtcNow };
        db.AddRange(nutrition, recovery); await db.SaveChangesAsync();
        db.ProgramItems.Add(new ProgramItem { ProgramId = nutrition.Id, Type = ProgramItemType.Meal, Title = "Завтрак", Time = new TimeSpan(8, 0, 0), Order = 1, Calories = 500 });
        db.ProgramItems.Add(new ProgramItem { ProgramId = recovery.Id, Type = ProgramItemType.Procedure, Title = "Растяжка", Time = new TimeSpan(20, 0, 0), Order = 1, Minutes = 15 });
        await db.SaveChangesAsync(); var day = new DateTime(2031, 2, 3);
        await ScheduleSyncService.SyncAsync(db, user.Id, day); await ScheduleSyncService.SyncAsync(db, user.Id, day);
        Assert.Single(await db.DayEntries.Where(x => x.UserId == user.Id && x.Type == ProgramItemType.Meal && x.Start >= day && x.Start < day.AddDays(1)).ToListAsync());
        Assert.Single(await db.DayEntries.Where(x => x.UserId == user.Id && x.Type == ProgramItemType.Procedure && x.Start >= day && x.Start < day.AddDays(1)).ToListAsync());
    }

    [Fact]
    public async Task Meal_and_water_are_saved_in_unified_day_entries_and_isolated_by_owner()
    {
        var user = await factory.CreateCompletedUserAsync($"daily-{Guid.NewGuid():N}@example.test", "valid-password-123");
        var other = await factory.CreateCompletedUserAsync($"daily-other-{Guid.NewGuid():N}@example.test", "valid-password-123");
        var day = new DateTime(2026, 10, 5);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, user.Email!, "valid-password-123");
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, $"/FoodDiary/AddMeal?date={day:yyyy-MM-dd}", new()
        {
            ["Type"]="Meal", ["Date"]=day.ToString("yyyy-MM-dd"), ["Time"]="08:15", ["Name"]="Фактический завтрак",
            ["CustomName"]="Овсянка", ["Grams"]="200", ["CaloriesPer100"]="100", ["ProteinPer100"]="5", ["FatPer100"]="2", ["CarbsPer100"]="18"
        }, "/FoodDiary/AddMeal")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, $"/FoodDiary/AddMeal?date={day:yyyy-MM-dd}&type=Water", new()
        {
            ["Type"]="Water", ["Date"]=day.ToString("yyyy-MM-dd"), ["Time"]="10:00", ["Name"]="Вода", ["Grams"]="750"
        }, "/FoodDiary/AddMeal")).StatusCode);
        var page = await client.GetAsync($"/FoodDiary?date={day:yyyy-MM-dd}"); page.EnsureSuccessStatusCode();
        var html = WebUtility.HtmlDecode(await page.Content.ReadAsStringAsync()); Assert.Contains("Фактический завтрак", html); Assert.Contains("750 мл", html);
        using var scope = factory.Services.CreateScope(); var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var meal = await db.DayEntries.Include(x => x.FoodItems).SingleAsync(x => x.UserId == user.Id && x.Type == ProgramItemType.Meal && x.Start.Date == day);
        Assert.Equal(200, NutritionHelper.ItemCalories(meal.FoodItems.Single()));
        Assert.Equal(750, await db.DayEntries.Where(x => x.UserId == user.Id && x.Type == ProgramItemType.Water && x.Start.Date == day).SumAsync(x => x.Value));
        Assert.Empty(await db.DayEntries.Where(x => x.UserId == other.Id && (x.Type == ProgramItemType.Meal || x.Type == ProgramItemType.Water)).ToListAsync());
    }

    [Fact]
    public async Task Recovery_date_shows_only_its_records_and_sleep_that_crosses_midnight()
    {
        var user = await factory.CreateCompletedUserAsync($"recovery-{Guid.NewGuid():N}@example.test", "valid-password-123");
        var start = new DateTime(2032, 4, 10, 23, 0, 0); var selected = start.AddDays(1).Date;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.DayEntries.Add(new DayEntry { UserId = user.Id, Type = ProgramItemType.Bedtime, Start = start, End = selected.AddHours(7), Title = "Ночной сон", Status = DayEntryStatus.Done, Rating = 4 });
            db.DayEntries.AddRange(new DayEntry { UserId = user.Id, Type = ProgramItemType.Procedure, Title = "Процедура выбранного дня", Start = selected.AddHours(20), Value = 20, Status = DayEntryStatus.Done }, new DayEntry { UserId = user.Id, Type = ProgramItemType.Procedure, Title = "Старая процедура", Start = selected.AddDays(-2).AddHours(20), Value = 20, Status = DayEntryStatus.Done });
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false }); await LoginAsync(client, user.Email!, "valid-password-123");
        var response = await client.GetAsync($"/MyRecovery?date={selected:yyyy-MM-dd}"); response.EnsureSuccessStatusCode();
        var html = WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
        Assert.Contains("Ночной сон", html); Assert.Contains("Процедура выбранного дня", html); Assert.DoesNotContain("Старая процедура", html);
    }

    [Fact]
    public async Task Active_sleep_program_creates_bedtime_and_wakeup_entries_for_my_day()
    {
        var user = await factory.CreateCompletedUserAsync($"sleep-plan-{Guid.NewGuid():N}@example.test", "valid-password-123");
        var day = new DateTime(2033, 5, 6);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var plan = new FitnessProgram { OwnerId = user.Id, Kind = ProgramKind.Sleep, Name = "Сон 8 часов", Active = true, StartDate = day, CreatedAt = DateTime.UtcNow };
            plan.Items.Add(new ProgramItem { Type = ProgramItemType.Bedtime, Title = "Отбой", Time = new TimeSpan(23, 0, 0), DayNumber = 1, Order = 1 });
            plan.Items.Add(new ProgramItem { Type = ProgramItemType.WakeUp, Title = "Подъём", Time = new TimeSpan(7, 0, 0), DayNumber = 1, Order = 2 });
            db.Programs.Add(plan); await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, user.Email!, "valid-password-123");
        (await client.GetAsync($"/MyRecovery?date={day:yyyy-MM-dd}")).EnsureSuccessStatusCode();
        using var verifyScope = factory.Services.CreateScope(); var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var entries = await verifyDb.DayEntries.Where(x => x.UserId == user.Id && x.Start >= day && x.Start < day.AddDays(1)).OrderBy(x => x.Start).ToListAsync();
        Assert.Contains(entries, x => x.Type == ProgramItemType.WakeUp && x.Start.TimeOfDay == new TimeSpan(7, 0, 0));
        Assert.Contains(entries, x => x.Type == ProgramItemType.Bedtime && x.Start.TimeOfDay == new TimeSpan(23, 0, 0));
    }

    [Fact]
    public async Task Weekday_supplement_plan_creates_0800_entry_without_leaking_to_another_user()
    {
        var user = await factory.CreateCompletedUserAsync($"supplement-plan-{Guid.NewGuid():N}@example.test", "valid-password-123");
        var other = await factory.CreateCompletedUserAsync($"supplement-plan-other-{Guid.NewGuid():N}@example.test", "valid-password-123");
        var monday = new DateTime(2034, 1, 2);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var categoryId = await db.Categories.Where(x=>x.Section=="Добавки").Select(x => x.Id).FirstAsync();
            var supplement = new Supplement { OwnerId = user.Id, CategoryId = categoryId, Name = "Креатин", Active = true };
            var plan = new FitnessProgram { OwnerId = user.Id, Kind = ProgramKind.Supplements, Name = "Курс креатина", Weeks = 8, StartDate = monday, EndDate = monday.AddDays(55), Active = true, CreatedAt = DateTime.UtcNow };
            db.AddRange(supplement, plan); await db.SaveChangesAsync();
            foreach (var day in new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }) plan.Items.Add(new ProgramItem { Type = ProgramItemType.Supplement, SupplementId = supplement.Id, Title = supplement.Name, Dose = "5 г", DayOfWeek = day, Time = new TimeSpan(8, 0, 0), DayNumber = 1, Order = (short)day });
            await db.SaveChangesAsync(); await ScheduleSyncService.SyncAsync(db, user.Id, monday);
        }
        using var verifyScope = factory.Services.CreateScope(); var verifyDb = verifyScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Single(await verifyDb.DayEntries.Where(x => x.UserId == user.Id && x.Type == ProgramItemType.Supplement && x.Start == monday.AddHours(8)).ToListAsync());
        Assert.Empty(await verifyDb.DayEntries.Where(x => x.UserId == other.Id && x.Type == ProgramItemType.Supplement).ToListAsync());
    }

    private static async Task LoginAsync(HttpClient client, string email, string password) => Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, "/Account/Login", new() { ["Email"] = email, ["Password"] = password, ["RememberMe"] = "false" }, "/Account/Login")).StatusCode);
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string tokenPage, Dictionary<string, string> values, string postUrl)
    {
        var get = await client.GetAsync(tokenPage); get.EnsureSuccessStatusCode(); var html = await get.Content.ReadAsStringAsync();
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\""); Assert.True(match.Success);
        values["__RequestVerificationToken"] = WebUtility.HtmlDecode(match.Groups[1].Value); return await client.PostAsync(postUrl, new FormUrlEncodedContent(values));
    }
}
