using FitMind.Data;
using FitMind.Models;
using FitMind.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Areas.Admin.Controllers;

[Area("Admin"), Authorize(Policy = "AdminOnly")]
public sealed class NutritionProgramsController(ApplicationDbContext db, AdminAuditService audit, IWebHostEnvironment environment) : Controller
{
    IQueryable<FitnessProgram> Programs => db.Programs.Where(x => x.OwnerId == null && x.Kind == ProgramKind.Nutrition);
    public async Task<IActionResult> Index() => View(await Programs.AsNoTracking().OrderBy(x => x.Name).ToListAsync());
    public async Task<IActionResult> Edit(int? id)
    {
        var value = id.HasValue ? await Programs.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id) : new FitnessProgram { Kind = ProgramKind.Nutrition, Weeks = 4, WaterMl = 2000, Active = true };
        return value is null ? NotFound() : View(value);
    }
    [HttpPost]
    public async Task<IActionResult> Save(int id, string name, string? description, string? goalText, int caloriesPerDay, int waterMl, byte weeks, bool active, IFormFile? imageFile, bool removeImage)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 180 || description?.Length > 2000 || caloriesPerDay is < 500 or > 10000 || waterMl is < 250 or > 10000 || weeks is < 1 or > 104) return BadRequest("Проверьте название, калории, воду и длительность.");
        var upload=await ContentImageStorage.SaveAsync(imageFile,environment);if(upload.Error is not null){TempData["Error"]=upload.Error;return RedirectToAction(nameof(Edit),new{id=id==0?(int?)null:id});}
        var program = id == 0 ? new FitnessProgram { Kind = ProgramKind.Nutrition, CreatedAt = DateTime.UtcNow, Image = FitMind.Ui.CatalogImagePaths.NutritionPrograms } : await Programs.FirstOrDefaultAsync(x => x.Id == id);
        if (program is null) return NotFound();
        program.Name = name.Trim(); program.Description = description?.Trim(); program.GoalText = goalText?.Trim(); program.CaloriesPerDay = caloriesPerDay; program.WaterMl = waterMl; program.Weeks = weeks; program.Active = active;
        if(upload.Path is not null)program.Image=upload.Path;else if(removeImage)program.Image=null;
        if (id == 0) db.Programs.Add(program); await db.SaveChangesAsync(); await audit.WriteAsync(id == 0 ? "Создание" : "Изменение", "Программа питания", program.Id, program.Name);
        return RedirectToAction(nameof(Edit), new { id = program.Id });
    }
    [HttpPost]
    public async Task<IActionResult> SaveItem(int programId, int id, ProgramItemType type, string title, TimeSpan time, int? calories, int? waterMl, short order)
    {
        if (type is not (ProgramItemType.Meal or ProgramItemType.Water) || string.IsNullOrWhiteSpace(title) || time < TimeSpan.Zero || time >= TimeSpan.FromDays(1) || order < 1 || !await Programs.AnyAsync(x => x.Id == programId)) return BadRequest();
        var item = id == 0 ? new ProgramItem { ProgramId = programId, DayNumber = 1 } : await db.ProgramItems.FirstOrDefaultAsync(x => x.Id == id && x.ProgramId == programId);
        if (item is null) return NotFound(); item.Type = type; item.Title = title.Trim(); item.Time = time; item.Calories = calories; item.WaterMl = waterMl; item.Order = order;
        if (id == 0) db.ProgramItems.Add(item); await db.SaveChangesAsync(); return RedirectToAction(nameof(Edit), new { id = programId });
    }
    [HttpPost]
    public async Task<IActionResult> DeleteItem(int programId, int id) { var item = await db.ProgramItems.FirstOrDefaultAsync(x => x.Id == id && x.ProgramId == programId && x.Program!.OwnerId == null && x.Program.Kind == ProgramKind.Nutrition); if (item is null) return NotFound(); db.Remove(item); await db.SaveChangesAsync(); return RedirectToAction(nameof(Edit), new { id = programId }); }
    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var program = await Programs.FirstOrDefaultAsync(x => x.Id == id);
        if (program is null) return NotFound();
        if (await db.Programs.AnyAsync(x => x.SourceProgramId == id)) { TempData["Error"] = "Программа используется пользователями. Снимите публикацию вместо удаления."; return RedirectToAction(nameof(Edit), new { id }); }
        db.Remove(program); await db.SaveChangesAsync(); await audit.WriteAsync("Удаление", "Программа питания", id, program.Name);
        return RedirectToAction(nameof(Index));
    }
}
