using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitMind.Controllers;

[Authorize]
public sealed class ContentImagesController(IWebHostEnvironment environment) : Controller
{
    [HttpGet]
    public IActionResult Get(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Any(c => !char.IsLetterOrDigit(c))) return NotFound();
        var path = Path.Combine(environment.ContentRootPath, "App_Data", "content-images", name + ".img");
        if (!System.IO.File.Exists(path)) return NotFound();
        var data = System.IO.File.ReadAllBytes(path);
        var contentType = data[0] == 0x89 ? "image/png" : data[0] == 0xff ? "image/jpeg" : "image/webp";
        return File(data, contentType);
    }
}
