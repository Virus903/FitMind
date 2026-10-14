using FitMind.Data;
using FitMind.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FitMind.Controllers;

/// <summary>База программ тренировок FitMind (таблица «Программы», ID_владельца = null).</summary>
[Authorize]
public class ProgramsController(ApplicationDbContext db) : Controller
{
    string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    IQueryable<FitnessProgram> BasePrograms => db.Programs.Where(x => x.OwnerId == null && x.Kind == ProgramKind.Workout);

    public async Task<IActionResult> Index(string? q, int? goalId, int? levelId, string? place, string? sort, int page = 1)
    {
        var a = BasePrograms.Include(x => x.Level).Where(x => x.Active);
        if (!string.IsNullOrWhiteSpace(q)) a = a.Where(x => x.Name.Contains(q) || (x.Description ?? "").Contains(q));
        if (goalId.HasValue)
        {
            var goalName = await db.FitnessGoals.Where(x => x.Id == goalId).Select(x => x.Name).FirstOrDefaultAsync();
            a = a.Where(x => x.GoalText == goalName);
        }
        if (levelId.HasValue) a = a.Where(x => x.LevelId == levelId);
        if (!string.IsNullOrWhiteSpace(place)) a = a.Where(x => x.Place == place);
        a = sort switch { "short" => a.OrderBy(x => x.Weeks), "long" => a.OrderByDescending(x => x.Weeks), _ => a.OrderBy(x => x.Name) };
        ViewBag.Goals = await db.FitnessGoals.OrderBy(x => x.Name).ToListAsync();
        ViewBag.Levels = await db.TrainingLevels.OrderBy(x => x.Id).ToListAsync();
        page = Math.Max(1, page); const int pageSize = 18; var total = await a.CountAsync(); page = Math.Min(page, Math.Max(1, (int)Math.Ceiling(total / (double)pageSize)));
        ViewBag.Q = q; ViewBag.GoalId = goalId; ViewBag.LevelId = levelId; ViewBag.Place = place; ViewBag.Sort = sort; ViewBag.Pagination = new FitMind.ViewModels.PaginationVm(page, pageSize, total);
        return View(await a.AsNoTracking().Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var p = await BasePrograms.Include(x => x.Level).Include(x => x.Items).ThenInclude(x => x.Exercises).ThenInclude(x => x.Exercise).AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id && x.Active);
        return p == null ? NotFound() : View(p);
    }

    /// <summary>«Добавить в мои»: копия программы базы с пунктами и упражнениями, владелец — пользователь.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToMy(int id)
    {
        var src = await BasePrograms.Include(x => x.Items).ThenInclude(x => x.Exercises).AsSplitQuery().FirstOrDefaultAsync(x => x.Id == id && x.Active);
        if (src == null) return NotFound();
        var current = await db.Programs.FirstOrDefaultAsync(x => x.OwnerId == Uid && x.SourceProgramId == src.Id);
        if (current != null) { TempData["Ok"] = "Эта программа уже есть в «Моих тренировках»."; return RedirectToAction("Index", "MyWorkouts"); }
        var copy = new FitnessProgram
        {
            Kind = src.Kind, OwnerId = Uid, SourceProgramId = src.Id, Name = src.Name, Description = src.Description, ForWhom = src.ForWhom, GoalText = src.GoalText,
            LevelId = src.LevelId, Place = src.Place, Weeks = src.Weeks, WorkoutsPerWeek = src.WorkoutsPerWeek, AverageDurationMinutes = src.AverageDurationMinutes,
            CaloriesPerDay = src.CaloriesPerDay, WaterMl = src.WaterMl, Image = src.Image, Active = false, StartDate = DateTime.Today,
            EndDate = DateTime.Today.AddDays((src.Weeks ?? 8) * 7 - 1), CreatedAt = DateTime.Now
        };
        // Дни базы получают дни недели по порядку (Пн, Ср, Пт, …) и время 18:00 — их можно изменить в «Изменить».
        var weekdays = new[] { DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday, DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday, DayOfWeek.Sunday };
        var index = 0;
        foreach (var item in src.Items.OrderBy(x => x.DayNumber).ThenBy(x => x.Order))
        {
            var target = new ProgramItem
            {
                DayNumber = item.DayNumber, DayOfWeek = item.DayOfWeek ?? (index < weekdays.Length ? weekdays[index] : null), Time = item.Time ?? (index < weekdays.Length ? new TimeSpan(18, 0, 0) : null),
                Type = item.Type, Title = item.Title, Description = item.Description, ProductId = item.ProductId, SupplementId = item.SupplementId,
                RecoveryMethodId = item.RecoveryMethodId, Dose = item.Dose, Calories = item.Calories, WaterMl = item.WaterMl, Minutes = item.Minutes, Order = item.Order
            };
            foreach (var e in item.Exercises.OrderBy(x => x.Order))
                target.Exercises.Add(new ProgramItemExercise { ExerciseId = e.ExerciseId, Order = e.Order, Sets = e.Sets, RepsMin = e.RepsMin, RepsMax = e.RepsMax, Seconds = e.Seconds, RestSeconds = e.RestSeconds, WeightKg = e.WeightKg, Comment = e.Comment });
            copy.Items.Add(target);
            index++;
        }
        db.Programs.Add(copy);
        await db.SaveChangesAsync();
        TempData["Ok"] = "Программа добавлена. Проверьте дни недели и время, затем сделайте её активной.";
        return RedirectToAction("Index", "MyWorkouts");
    }
}
