using System.Security.Claims;
using FitMind.Data;
using FitMind.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Controllers;

[Authorize]
public sealed class NutritionProgramsController(ApplicationDbContext db) : Controller
{
    string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    IQueryable<FitnessProgram> BasePrograms => db.Programs.Where(x => x.OwnerId == null && x.Kind == ProgramKind.Nutrition);

    public async Task<IActionResult> Index(string? search, string sort = "calories")
    {
        var query = BasePrograms.Where(x => x.Active).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(x => x.Name.Contains(search) || (x.Description != null && x.Description.Contains(search)) || (x.GoalText != null && x.GoalText.Contains(search)));
        }
        query = sort switch
        {
            "name" => query.OrderBy(x => x.Name),
            "calories_desc" => query.OrderByDescending(x => x.CaloriesPerDay),
            "weeks" => query.OrderBy(x => x.Weeks).ThenBy(x => x.Name),
            _ => query.OrderBy(x => x.CaloriesPerDay).ThenBy(x => x.Name)
        };
        ViewBag.Search = search;
        ViewBag.Sort = sort;
        return View(await query.ToListAsync());
    }

    public async Task<IActionResult> Details(int id)
    {
        var program = await BasePrograms.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id && x.Active);
        return program is null ? NotFound() : View(program);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddToMine(int id)
    {
        var source = await BasePrograms.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id && x.Active);
        if (source is null) return NotFound();
        if (await db.Programs.AnyAsync(x => x.OwnerId == Uid && x.SourceProgramId == id)) { TempData["Ok"] = "Эта программа уже добавлена."; return RedirectToAction("Index", "FoodDiary"); }
        var copy = new FitnessProgram { Kind = ProgramKind.Nutrition, OwnerId = Uid, SourceProgramId = source.Id, Name = source.Name, Description = source.Description, GoalText = source.GoalText, Weeks = source.Weeks, CaloriesPerDay = source.CaloriesPerDay, WaterMl = source.WaterMl, Image = source.Image, StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays((source.Weeks ?? 4) * 7 - 1), CreatedAt = DateTime.UtcNow };
        foreach (var item in source.Items.OrderBy(x => x.Order)) copy.Items.Add(new ProgramItem { Type = item.Type, DayNumber = item.DayNumber, DayOfWeek = item.DayOfWeek, Time = item.Time, Title = item.Title, ProductId = item.ProductId, Dose = item.Dose, Calories = item.Calories, WaterMl = item.WaterMl, Order = item.Order });
        db.Programs.Add(copy); await db.SaveChangesAsync(); TempData["Ok"] = "Программа добавлена в «Питание и вода».";
        return RedirectToAction("Index", "FoodDiary");
    }
}
