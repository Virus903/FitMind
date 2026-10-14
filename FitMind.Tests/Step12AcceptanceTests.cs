using System.Net;
using System.Net.Http.Headers;
using System.Text.RegularExpressions;
using FitMind.Data;
using FitMind.Models;
using FitMind.Models.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FitMind.Tests;

[Collection("Account flow")]
public sealed class Step12AcceptanceTests(FitMindFactory factory)
{
    [Fact]
    public async Task Supplement_intake_can_be_marked_done()
    {
        var user = await factory.CreateCompletedUserAsync($"accept-supplement-{Guid.NewGuid():N}@example.test", "valid-password-123");
        long entryId;
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var category = await db.Categories.FirstAsync(x => x.Section == "Добавки");
            var supplement = new Supplement { OwnerId = user.Id, CategoryId = category.Id, Name = "Креатин приёмка", Active = true };
            db.Supplements.Add(supplement);
            await db.SaveChangesAsync();
            var entry = new DayEntry { UserId = user.Id, SupplementId = supplement.Id, Type = ProgramItemType.Supplement, Title = supplement.Name, Start = DateTime.Today.AddHours(8), Status = DayEntryStatus.Planned };
            db.DayEntries.Add(entry);
            await db.SaveChangesAsync();
            entryId = entry.Id;
        }

        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, user.Email!, "valid-password-123");
        var response = await PostAsync(client, "/MySupplements", new() { ["id"] = entryId.ToString(), ["status"] = "Принято" }, "/MySupplements/Take");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var verify = factory.Services.CreateScope();
        Assert.Equal(DayEntryStatus.Done, await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().DayEntries.Where(x => x.Id == entryId).Select(x => x.Status).SingleAsync());
    }

    [Fact]
    public async Task Calculator_result_is_saved_to_history()
    {
        var user = await factory.CreateCompletedUserAsync($"accept-calculator-{Guid.NewGuid():N}@example.test", "valid-password-123");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, user.Email!, "valid-password-123");
        var response = await PostAsync(client, "/Calculators?calc=BMI", new()
        {
            ["Calculator"] = "BMI", ["Values[w]"] = "70", ["Values[h]"] = "175"
        }, "/Calculators/Save");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        var history = WebUtility.HtmlDecode(await client.GetStringAsync("/Calculators?calc=BMI"));
        Assert.Contains("22,86", history);
        using var verify = factory.Services.CreateScope();
        Assert.True(await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().PersonalNotes.AnyAsync(x => x.UserId == user.Id && x.Area == "Калькуляторы" && x.Title == "ИМТ"));
    }

    [Fact]
    public async Task Profile_avatar_password_theme_and_export_work_together()
    {
        var user = await factory.CreateCompletedUserAsync($"accept-profile-{Guid.NewGuid():N}@example.test", "old-password-123");
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, user.Email!, "old-password-123");

        var avatarPage = await client.GetStringAsync("/Profile/Edit");
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StringContent(Token(avatarPage)), "__RequestVerificationToken");
        var png = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0);
        System.Text.Encoding.ASCII.GetBytes("IHDR").CopyTo(png, 12);
        var image = new ByteArrayContent(png);
        image.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        multipart.Add(image, "avatar", "avatar.png");
        Assert.Equal(HttpStatusCode.Redirect, (await client.PostAsync("/Profile/UploadAvatar", multipart)).StatusCode);
        var avatar = await client.GetAsync("/Profile/Avatar");
        avatar.EnsureSuccessStatusCode();
        Assert.Equal("image/png", avatar.Content.Headers.ContentType?.MediaType);

        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, "/Settings/Account", new()
        {
            ["currentPassword"] = "old-password-123", ["newPassword"] = "new-password-456", ["confirmPassword"] = "new-password-456"
        }, "/Settings/ChangePassword")).StatusCode);
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, "/Settings", new()
        {
            ["notificationsEnabled"] = "true", ["emailNotifications"] = "false", ["quietHoursEnabled"] = "false", ["units"] = "Метрические", ["theme"] = "Светлая"
        }, "/Settings")).StatusCode);
        var export = await client.GetAsync("/Settings/ExportData");
        export.EnsureSuccessStatusCode();
        Assert.Equal("application/json", export.Content.Headers.ContentType?.MediaType);

        using var verify = factory.Services.CreateScope();
        var users = verify.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await users.CheckPasswordAsync((await users.FindByIdAsync(user.Id))!, "new-password-456"));
        Assert.Equal("Светлая", await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().UserProfiles.Where(x => x.UserId == user.Id).Select(x => x.Theme).SingleAsync());

        await PostAsync(client, "/Profile/Edit", new(), "/Profile/DeleteAvatar");
    }

    [Fact]
    public async Task Administrator_creates_nutrition_program()
    {
        var marker = Guid.NewGuid().ToString("N");
        var admin = await factory.CreateCompletedUserAsync($"accept-admin-{marker}@example.test", "valid-password-123");
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            Assert.True((await users.AddToRoleAsync((await users.FindByIdAsync(admin.Id))!, "Администратор")).Succeeded);
        }
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        await LoginAsync(client, admin.Email!, "valid-password-123");
        var name = $"Приёмка питания {marker}";
        var response = await PostAsync(client, "/Admin/NutritionPrograms/Edit", new()
        {
            ["id"] = "0", ["name"] = name, ["goalText"] = "Поддержание формы", ["caloriesPerDay"] = "2200", ["waterMl"] = "2000", ["weeks"] = "4", ["active"] = "true"
        }, "/Admin/NutritionPrograms/Save");
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        using var verify = factory.Services.CreateScope();
        Assert.True(await verify.ServiceProvider.GetRequiredService<ApplicationDbContext>().Programs.AnyAsync(x => x.OwnerId == null && x.Kind == ProgramKind.Nutrition && x.Name == name));
    }

    private static async Task LoginAsync(HttpClient client, string email, string password) =>
        Assert.Equal(HttpStatusCode.Redirect, (await PostAsync(client, "/Account/Login", new() { ["Email"] = email, ["Password"] = password, ["RememberMe"] = "false" }, "/Account/Login")).StatusCode);

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string tokenPage, Dictionary<string, string> values, string postUrl)
    {
        var html = await client.GetStringAsync(tokenPage);
        values["__RequestVerificationToken"] = Token(html);
        return await client.PostAsync(postUrl, new FormUrlEncodedContent(values));
    }

    private static string Token(string html)
    {
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success);
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
