using Microsoft.AspNetCore.Razor.TagHelpers;

namespace FitMind.Ui;

[HtmlTargetElement("catalog-placeholder")]
public sealed class CatalogPlaceholderTagHelper : TagHelper
{
    [HtmlAttributeName("kind")] public string Kind { get; set; } = "";
    [HtmlAttributeName("alt")] public string Alt { get; set; } = "";
    [HtmlAttributeName("custom-src")] public string? CustomSrc { get; set; }

    public override void Process(TagHelperContext context, TagHelperOutput output)
    {
        output.TagName = "picture";
        output.TagMode = TagMode.StartTagAndEndTag;
        output.Attributes.SetAttribute("class", MergeClass(output.Attributes["class"]?.Value?.ToString(), "catalog-placeholder"));

        if (IsCustomImage(CustomSrc))
        {
            output.Content.SetHtmlContent($"<img class=\"catalog-placeholder__image catalog-placeholder__custom\" src=\"{Encode(CustomSrc!)}\" alt=\"{Encode(Alt)}\" />");
            return;
        }

        var (dark, light) = CatalogImagePaths.For(Kind);
        output.Content.SetHtmlContent(
            $"<img class=\"catalog-placeholder__image theme-image-dark\" src=\"{dark}\" alt=\"{Encode(Alt)}\" />" +
            $"<img class=\"catalog-placeholder__image theme-image-light\" src=\"{light}\" alt=\"\" aria-hidden=\"true\" />");
    }

    private static bool IsCustomImage(string? path) =>
        !string.IsNullOrWhiteSpace(path) &&
        path.StartsWith("/ContentImages/Get", StringComparison.OrdinalIgnoreCase);

    private static string MergeClass(string? current, string required) =>
        string.IsNullOrWhiteSpace(current) ? required : $"{current} {required}";

    private static string Encode(string value) => System.Net.WebUtility.HtmlEncode(value);
}
