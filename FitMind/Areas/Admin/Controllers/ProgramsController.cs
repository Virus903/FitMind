using FitMind.Data;
using FitMind.Models;
using FitMind.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Areas.Admin.Controllers;

/// <summary>Программы базы FitMind (таблица «Программы», ID_владельца = null, Вид = тренировки).</summary>
[Area("Admin"), Authorize(Policy = "AdminOnly")]
public sealed class ProgramsController(ApplicationDbContext db, AdminAuditService audit, IWebHostEnvironment environment) : Controller
{
    IQueryable<FitnessProgram> BasePrograms => db.Programs.Where(x => x.OwnerId == null && x.Kind == ProgramKind.Workout);

    public async Task<IActionResult> Index(string? q, int page = 1)
    {
        var query = BasePrograms.AsNoTracking().Where(x => q == null || x.Name.Contains(q));
        ViewBag.Query = q;
        ViewBag.Pages = Math.Max(1, (int)Math.Ceiling(await query.CountAsync() / 20d));
        ViewBag.Page = Math.Clamp(page, 1, (int)ViewBag.Pages);
        return View(await query.OrderBy(x => x.Name).Skip(((int)ViewBag.Page - 1) * 20).Take(20).ToListAsync());
    }

    public async Task<IActionResult> Edit(int? id)
    {
        var program = id.HasValue
            ? await BasePrograms.AsNoTracking().Include(x => x.Items).ThenInclude(x => x.Exercises).ThenInclude(x => x.Exercise).AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id)
            : new FitnessProgram { Kind = ProgramKind.Workout, Weeks = 8, WorkoutsPerWeek = 3 };
        if (program is null) return NotFound();
        await Lookups();
        return View(program);
    }

    [HttpPost]
    public async Task<IActionResult> Save(int id, string name, string? description, string? forWhom, string? place, string? goalText, int levelId, byte weeks, byte workoutsPerWeek, short? averageDurationMinutes, bool active, IFormFile? imageFile, bool removeImage)
    {
        var item = id == 0 ? new FitnessProgram { Kind = ProgramKind.Workout, CreatedAt = DateTime.UtcNow } : await BasePrograms.FirstOrDefaultAsync(x => x.Id == id);
        if (item is null) return NotFound();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 180 || description?.Length > 4000 || forWhom?.Length > 500 || place?.Length > 180 || averageDurationMinutes is < 1 or > 1440 || weeks is < 1 or > 104 || workoutsPerWeek is < 1 or > 7 || !await db.FitnessGoals.AnyAsync(x => x.Name == goalText) || !await db.TrainingLevels.AnyAsync(x => x.Id == levelId))
            return BadRequest("Проверьте название, цель, уровень, недели (1–104) и частоту (1–7).");
        var upload=await ContentImageStorage.SaveAsync(imageFile,environment);if(upload.Error is not null){TempData["Error"]=upload.Error;return RedirectToAction(nameof(Edit),new{id=id==0?(int?)null:id});}
        item.Name = name.Trim(); item.Description = description?.Trim(); item.ForWhom = forWhom?.Trim(); item.Place = place?.Trim(); item.AverageDurationMinutes = averageDurationMinutes; item.GoalText = goalText; item.LevelId = levelId; item.Weeks = weeks; item.WorkoutsPerWeek = workoutsPerWeek; item.Active = active;
        if(upload.Path is not null)item.Image=upload.Path;else if(removeImage)item.Image=null;else if(id==0)item.Image=FitMind.Ui.CatalogImagePaths.TrainingPrograms;
        if (id == 0) db.Programs.Add(item);
        await db.SaveChangesAsync(); await audit.WriteAsync(id == 0 ? "Создание" : "Изменение", "Программа", item.Id, item.Name);
        TempData["Ok"] = "Программа сохранена.";
        return RedirectToAction(nameof(Edit), new { id = item.Id });
    }

    [HttpPost]
    public async Task<IActionResult> SaveDay(int programId, int id, string name, string? description, byte dayNumber)
    {
        if (!await BasePrograms.AnyAsync(x => x.Id == programId)) return NotFound();
        var day = id == 0 ? new ProgramItem { ProgramId = programId, Type = ProgramItemType.Workout } : await db.ProgramItems.FirstOrDefaultAsync(x => x.Id == id && x.ProgramId == programId);
        if (day is null) return NotFound();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 180 || description?.Length > 2000 || dayNumber == 0 || await db.ProgramItems.AnyAsync(x => x.ProgramId == programId && x.DayNumber == dayNumber && x.Id != id))
        { TempData["Error"] = "Укажите название и уникальный номер дня от 1 до 255."; return RedirectToAction(nameof(Edit), new { id = programId }); }
        day.Title = name.Trim(); day.Description = description?.Trim(); day.DayNumber = dayNumber; day.Order = dayNumber;
        if (id == 0) db.ProgramItems.Add(day);
        await db.SaveChangesAsync(); await audit.WriteAsync("Сохранение дня", "Программа", programId, day.Title);
        return RedirectToAction(nameof(Edit), new { id = programId });
    }

    [HttpPost]
    public async Task<IActionResult> SaveExercise(int programId, int dayId, int id, int exerciseId, byte order, byte sets, short? repsMin, short? repsMax, short? durationSeconds, short? restSeconds, string? comment)
    {
        if (!await db.ProgramItems.AnyAsync(x => x.Id == dayId && x.ProgramId == programId && x.Program!.OwnerId == null)) return NotFound();
        var item = id == 0 ? new ProgramItemExercise { ProgramItemId = dayId } : await db.ProgramItemExercises.FirstOrDefaultAsync(x => x.Id == id && x.ProgramItemId == dayId);
        if (item is null) return NotFound();
        if (!ModelState.IsValid || !await db.Exercises.AnyAsync(x => x.Id == exerciseId && x.OwnerId == null) || await db.ProgramItemExercises.AnyAsync(x => x.ProgramItemId == dayId && x.Order == order && x.Id != id) || sets is < 1 or > 30 || order == 0 || repsMin < 0 || repsMax < repsMin || durationSeconds < 0 || restSeconds < 0 || comment?.Length > 1000)
        { TempData["Error"] = "Проверьте упражнение, порядок, подходы (1–30) и неотрицательные значения."; return RedirectToAction(nameof(Edit), new { id = programId }); }
        item.ExerciseId = exerciseId; item.Order = order; item.Sets = sets; item.RepsMin = repsMin; item.RepsMax = repsMax; item.Seconds = durationSeconds; item.RestSeconds = restSeconds; item.Comment = comment?.Trim();
        if (id == 0) db.ProgramItemExercises.Add(item);
        await db.SaveChangesAsync(); await audit.WriteAsync("Сохранение упражнения дня", "Программа", programId, $"День {dayId}, упражнение {exerciseId}");
        return RedirectToAction(nameof(Edit), new { id = programId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteExercise(int programId, int dayId, int id)
    {
        if (!await db.ProgramItems.AnyAsync(x => x.Id == dayId && x.ProgramId == programId && x.Program!.OwnerId == null)) return NotFound();
        var item = await db.ProgramItemExercises.FirstOrDefaultAsync(x => x.Id == id && x.ProgramItemId == dayId);
        if (item is null) return NotFound(); db.Remove(item); await db.SaveChangesAsync();
        await audit.WriteAsync("Удаление упражнения дня", "Программа", programId, $"Запись {id}");
        return RedirectToAction(nameof(Edit), new { id = programId });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteDay(int programId, int id)
    {
        var day = await db.ProgramItems.FirstOrDefaultAsync(x => x.Id == id && x.ProgramId == programId && x.Program!.OwnerId == null);
        if (day is null) return NotFound(); db.Remove(day); await db.SaveChangesAsync();
        await audit.WriteAsync("Удаление дня", "Программа", programId, day.Title);
        return RedirectToAction(nameof(Edit), new { id = programId });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id)
    {
        var item = await BasePrograms.FirstOrDefaultAsync(x => x.Id == id); if (item is null) return NotFound();
        // Личные копии содержат собственные пункты; программа базы, которую добавили себе пользователи, не удаляется.
        if (await db.Programs.AnyAsync(x => x.SourceProgramId == id))
        { TempData["Error"] = "Программа используется пользователями. Снимите публикацию вместо удаления."; return RedirectToAction(nameof(Edit), new { id }); }
        db.Remove(item); await db.SaveChangesAsync(); await audit.WriteAsync("Удаление", "Программа", id, item.Name);
        return RedirectToAction(nameof(Index));
    }

    private async Task Lookups()
    {
        ViewBag.Goals = await db.FitnessGoals.AsNoTracking().ToListAsync();
        ViewBag.Levels = await db.TrainingLevels.AsNoTracking().ToListAsync();
        ViewBag.Exercises = await db.Exercises.AsNoTracking().Where(x => x.OwnerId == null).OrderBy(x => x.Name).ToListAsync();
    }
}
