using System.Net;
using System.Text.RegularExpressions;
using FitMind.Data;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace FitMind.Tests;

[Collection("Account flow")]
public sealed class ProfileOnboardingTests(FitMindFactory factory)
{
    [Fact]
    public async Task Onboarding_saves_several_places_goal_and_formats_in_one_profile()
    {
        var email=$"onboarding-{Guid.NewGuid():N}@example.test";
        using var client=factory.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false});
        var register=await PostFormAsync(client,"/Account/Register",new(){["Name"]="Анкета Тест",["Email"]=email,["Password"]="secure-pass-123",["ConfirmPassword"]="secure-pass-123"});
        Assert.Equal(HttpStatusCode.Redirect,register.StatusCode);

        using var scope=factory.Services.CreateScope();
        var db=scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var activityId=await db.ActivityLevels.Select(x=>x.Id).FirstAsync();
        var setup=await PostFormAsync(client,"/Profile/Setup",new(){["Age"]="25",["HeightCm"]="180",["WeightKg"]="75",["Gender"]="Мужской",["ActivityLevelId"]=activityId.ToString()});
        Assert.Equal("/Profile/Goal",setup.Headers.Location?.OriginalString);

        var goalPage=await client.GetStringAsync("/Profile/Goal");
        // Все цели из таблицы «Цели» (другие тесты общей базы могут добавить цель через админку).
        Assert.Equal(await db.FitnessGoals.CountAsync(),Regex.Matches(goalPage,"name=\"GoalId\"").Count);
        foreach(var name in new[]{"Снижение веса","Набор мышечной массы","Развитие силы","Поддержание формы"})Assert.Contains(name,WebUtility.HtmlDecode(goalPage));
        Assert.Contains("onboarding-v04-secondary",goalPage);
        var goalId=await db.FitnessGoals.Where(x=>x.Name=="Поддержание формы").Select(x=>x.Id).SingleAsync();
        var levelId=await db.TrainingLevels.Select(x=>x.Id).FirstAsync();
        var goal=await PostFormAsync(client,"/Profile/Goal",new(){["GoalId"]=goalId.ToString(),["TrainingLevelId"]=levelId.ToString()});
        Assert.Equal("/Profile/TrainingPreferences",goal.Headers.Location?.OriginalString);

        var prefsPage=await client.GetStringAsync("/Profile/TrainingPreferences");
        Assert.Equal(5,Regex.Matches(prefsPage,"name=\"FormatIds\"").Count);
        Assert.Contains("Функциональные",WebUtility.HtmlDecode(prefsPage));

        var noPlace=await PostFormAsync(client,"/Profile/TrainingPreferences",new(){["WorkoutsPerWeek"]="4",["FormatIds"]="1"});
        Assert.Equal(HttpStatusCode.OK,noPlace.StatusCode);
        Assert.Contains("Выберите хотя бы одно место",WebUtility.HtmlDecode(await noPlace.Content.ReadAsStringAsync()));

        var formatIds=await db.TrainingFormats.Where(x=>x.Name=="Силовые"||x.Name=="Функциональные").Select(x=>x.Id).ToListAsync();
        var token=ReadAntiforgeryToken(await client.GetStringAsync("/Profile/TrainingPreferences"));
        var form=new List<KeyValuePair<string,string>>{new("AtHome","true"),new("AtGym","true"),new("WorkoutsPerWeek","4"),new("__RequestVerificationToken",token)};
        form.AddRange(formatIds.Select(id=>new KeyValuePair<string,string>("FormatIds",id.ToString())));
        var prefs=await client.PostAsync("/Profile/TrainingPreferences",new FormUrlEncodedContent(form));
        Assert.Equal(HttpStatusCode.Redirect,prefs.StatusCode);

        var user=await db.Users.SingleAsync(x=>x.Email==email);
        var profile=await db.UserProfiles.AsNoTracking().SingleAsync(x=>x.UserId==user.Id);
        Assert.True(profile.AtHome);Assert.True(profile.AtGym);Assert.False(profile.Outdoor);
        Assert.Equal(goalId,profile.GoalId);Assert.True(profile.OnboardingCompleted);
        Assert.Equal(2,await db.UserTrainingFormats.CountAsync(x=>x.UserId==user.Id));
        Assert.Equal("Тёмная",profile.Theme);

        var page=WebUtility.HtmlDecode(await client.GetStringAsync("/Profile"));
        Assert.Contains("Дома, В зале",page);
        Assert.Contains("Поддержание формы",WebUtility.HtmlDecode(await client.GetStringAsync("/Profile/Edit")));
    }

    [Fact]
    public async Task Login_keeps_google_button_visible_without_google_keys()
    {
        using var client=factory.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false});
        var html=WebUtility.HtmlDecode(await client.GetStringAsync("/Account/Login"));
        Assert.Contains("Войти через Google",html);
    }

    private static async Task<HttpResponseMessage> PostFormAsync(HttpClient client,string path,Dictionary<string,string> values)
    {
        var get=await client.GetAsync(path);get.EnsureSuccessStatusCode();
        values["__RequestVerificationToken"]=ReadAntiforgeryToken(await get.Content.ReadAsStringAsync());
        return await client.PostAsync(path,new FormUrlEncodedContent(values));
    }

    private static string ReadAntiforgeryToken(string html)
    {
        var match=Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success,"Antiforgery token was not rendered.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }
}
