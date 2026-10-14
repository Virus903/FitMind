using FitMind.Data;
using FitMind.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FitMind.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Policy="AdminOnly")]
public sealed class MediaController(ApplicationDbContext db, IWebHostEnvironment env, AdminAuditService audit):Controller
{
    [HttpPost, RequestSizeLimit(6*1024*1024)]
    public async Task<IActionResult> Upload(string kind,int id,IFormFile? image)
    {
        object? entity=kind switch {"exercises"=>await db.Exercises.FindAsync(id),"products"=>await db.FoodProducts.FirstOrDefaultAsync(x=>x.Id==id&&x.Kind==FitMind.Models.FoodProductKind.Product&&x.OwnerId==null),"recipes"=>await db.FoodProducts.FirstOrDefaultAsync(x=>x.Id==id&&x.Kind==FitMind.Models.FoodProductKind.Recipe&&x.OwnerId==null),"supplements"=>await db.Supplements.FirstOrDefaultAsync(x=>x.Id==id&&x.OwnerId==null),"articles"=>await db.Articles.FindAsync(id),"recovery"=>await db.RecoveryMethods.FindAsync(id),"programs"=>await db.Programs.FirstOrDefaultAsync(x=>x.Id==id&&x.OwnerId==null),_=>null};
        if(entity is null)return NotFound();
        if(image is null||image.Length<24||image.Length>5*1024*1024){TempData["Error"]="Выберите PNG, JPEG или WebP до 5 МБ.";return Back(kind,id);}
        using var buffer=new MemoryStream();await image.CopyToAsync(buffer);var bytes=buffer.ToArray();
        var extension=DetectExtension(bytes);
        if(extension is null){TempData["Error"]="Содержимое файла не соответствует PNG, JPEG или WebP.";return Back(kind,id);}
        var directory=Path.Combine(env.WebRootPath,"images","uploads");Directory.CreateDirectory(directory);
        var filename=Guid.NewGuid().ToString("N")+extension;
        var filePath=Path.Combine(directory,filename);
        await System.IO.File.WriteAllBytesAsync(filePath,bytes);
        entity.GetType().GetProperty("Image")!.SetValue(entity,"/images/uploads/"+filename);
        try
        {
            await db.SaveChangesAsync();
            await audit.WriteAsync("Загрузка изображения",kind,id,"Локальное изображение");
        }
        catch
        {
            if(System.IO.File.Exists(filePath))System.IO.File.Delete(filePath);
            throw;
        }
        TempData["Ok"]="Изображение сохранено.";return Back(kind,id);
    }

    public static string? DetectExtension(byte[] data)
    {
        if(data.Length<24)return null;
        if(data.Take(8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10})&&System.Text.Encoding.ASCII.GetString(data,12,4)=="IHDR")return ".png";
        if(data[0]==255&&data[1]==216&&data[2]==255&&data[^2]==255&&data[^1]==217)return ".jpg";
        if(System.Text.Encoding.ASCII.GetString(data,0,4)=="RIFF"&&System.Text.Encoding.ASCII.GetString(data,8,4)=="WEBP")return ".webp";
        return null;
    }

    private IActionResult Back(string kind,int id)=>kind=="programs"?RedirectToAction("Edit","Programs",new{id}):RedirectToAction("Edit","Catalog",new{kind,id});
}
