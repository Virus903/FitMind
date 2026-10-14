using Microsoft.AspNetCore.Http;

namespace FitMind.Services;

public static class ContentImageStorage
{
    public static async Task<(string? Path, string? Error)> SaveAsync(IFormFile? file, IWebHostEnvironment environment)
    {
        if (file is null || file.Length == 0) return (null, null);
        if (file.Length < 12 || file.Length > 2 * 1024 * 1024)
            return (null, "Изображение должно быть PNG, JPEG или WebP размером до 2 МБ.");

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        var data = stream.ToArray();
        var png = data.Take(8).SequenceEqual(new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 });
        var jpeg = data[0] == 255 && data[1] == 216 && data[2] == 255;
        var webp = System.Text.Encoding.ASCII.GetString(data, 0, 4) == "RIFF" && System.Text.Encoding.ASCII.GetString(data, 8, 4) == "WEBP";
        if (!(png || jpeg || webp)) return (null, "Файл не является PNG, JPEG или WebP.");

        var name = Guid.NewGuid().ToString("N");
        var directory = Path.Combine(environment.ContentRootPath, "App_Data", "content-images");
        Directory.CreateDirectory(directory);
        await File.WriteAllBytesAsync(Path.Combine(directory, name + ".img"), data);
        return ($"/ContentImages/Get?name={name}", null);
    }
}
