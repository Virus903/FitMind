using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Text.RegularExpressions;
using Xunit;

namespace FitMind.Tests;

[Collection("Account flow")]
public sealed class CatalogTests(FitMindFactory factory)
{
    [Fact]
    public async Task Catalogs_use_server_side_pagination_and_keep_filters()
    {
        var user=await factory.CreateCompletedUserAsync($"catalog-{Guid.NewGuid():N}@example.test","valid-password-123");
        using var client=factory.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false});
        await LoginAsync(client,user.Email!,"valid-password-123");

        foreach(var path in new[]{"/Exercises?page=2","/Programs?page=2","/SupplementLibrary?page=2","/RecoveryLibrary?page=2","/Knowledge?page=2","/FoodLibrary?page=1"})
        {
            var response=await client.GetAsync(path);response.EnsureSuccessStatusCode();
            var html=WebUtility.HtmlDecode(await response.Content.ReadAsStringAsync());
            Assert.Contains("Показано",html);Assert.DoesNotContain("An unhandled exception",html,StringComparison.OrdinalIgnoreCase);
        }

        var exercises=WebUtility.HtmlDecode(await client.GetStringAsync("/Exercises?q=жим&page=1"));
        Assert.Contains("value=\"жим\"",exercises,StringComparison.OrdinalIgnoreCase);
        Assert.InRange(Regex.Matches(exercises,"class=\"library-card\"").Count,1,24);
        var empty=WebUtility.HtmlDecode(await client.GetStringAsync("/Knowledge?q=___missing___"));
        Assert.Contains("Материалы не найдены",empty);
    }

    private static async Task LoginAsync(HttpClient client,string email,string password)
    {
        var get=await client.GetAsync("/Account/Login");get.EnsureSuccessStatusCode();var html=await get.Content.ReadAsStringAsync();
        var match=Regex.Match(html,"name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");Assert.True(match.Success);
        var response=await client.PostAsync("/Account/Login",new FormUrlEncodedContent(new Dictionary<string,string>{{"Email",email},{"Password",password},{"RememberMe","false"},{"__RequestVerificationToken",WebUtility.HtmlDecode(match.Groups[1].Value)}}));
        Assert.Equal(HttpStatusCode.Redirect,response.StatusCode);
    }
}
