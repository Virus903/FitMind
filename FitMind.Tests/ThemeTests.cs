using Microsoft.AspNetCore.Mvc.Testing;
using System.Text.RegularExpressions;
using Xunit;

namespace FitMind.Tests;

[Collection("Account flow")]
public sealed class ThemeTests(FitMindFactory factory)
{
    private static readonly string[] StyleSheets={"site.css","refinement.css","public-pages.css","program-editor.css","admin.css","admin-refinements.css"};
    private static readonly string[] ThemeVariables={"--bg-page","--bg-card","--bg-card-hover","--bg-input","--border","--border-focus","--text-primary","--text-secondary","--text-muted","--accent","--accent-hover","--text-on-accent","--success","--danger","--warning","--shadow"};

    [Fact]
    public async Task Stylesheets_use_only_theme_variables_for_colors()
    {
        using var client=factory.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false});
        foreach(var file in StyleSheets)
        {
            var css=await client.GetStringAsync("/css/"+file);
            var withoutImages=Regex.Replace(css,@"url\([^)]*\)","");
            Assert.DoesNotMatch(@"#[0-9a-fA-F]{3,8}\b|rgba?\(",withoutImages);
            Assert.DoesNotContain("data-theme=\"light\"",css);
        }
    }

    [Fact]
    public async Task Theme_file_defines_dark_light_and_system_palettes()
    {
        using var client=factory.CreateClient(new WebApplicationFactoryClientOptions{AllowAutoRedirect=false});
        var css=await client.GetStringAsync("/css/themes.css");
        var dark=Regex.Match(css,@":root\{(?<b>[^}]*)\}").Groups["b"].Value;
        var light=Regex.Match(css,@"html\[data-theme=""light""\]\{(?<b>[^}]*)\}").Groups["b"].Value;
        var system=Regex.Match(css,@"@media \(prefers-color-scheme: light\)\{\s*html\[data-theme=""system""\]\{(?<b>[^}]*)\}").Groups["b"].Value;
        Assert.Contains("--bg-page:#070A11",dark);
        foreach(var name in ThemeVariables)
        {
            Assert.Contains(name+":",dark);
            if(name!="--shadow"){Assert.Contains(name+":",light);Assert.Contains(name+":",system);}
        }
        Assert.Contains("@import url(\"themes.css\")",await client.GetStringAsync("/css/site.css"));
    }
}
