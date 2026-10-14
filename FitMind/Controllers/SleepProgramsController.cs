using System.Security.Claims;
using FitMind.Data;
using FitMind.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Controllers;

[Authorize]
public sealed class SleepProgramsController(ApplicationDbContext db) : Controller
{
    string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    IQueryable<FitnessProgram> BasePrograms => db.Programs.Where(x => x.OwnerId == null && x.Kind == ProgramKind.Sleep);

    public async Task<IActionResult> Index(string? search, string sort = "name")
    {
        var query = BasePrograms.Where(x => x.Active).AsNoTracking();
        if (!string.IsNullOrWhiteSpace(search))
        {
            search = search.Trim();
            query = query.Where(x => x.Name.Contains(search) || (x.Description != null && x.Description.Contains(search)));
        }
        query = sort switch
        {
            "weeks" => query.OrderBy(x => x.Weeks).ThenBy(x => x.Name),
            "newest" => query.OrderByDescending(x => x.CreatedAt),
            _ => query.OrderBy(x => x.Name)
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
        if (await db.Programs.AnyAsync(x => x.OwnerId == Uid && x.SourceProgramId == id)) return RedirectToAction("Index", "MyRecovery");
        var copy = new FitnessProgram { Kind = ProgramKind.Sleep, OwnerId = Uid, SourceProgramId = source.Id, Name = source.Name, Description = source.Description, Weeks = source.Weeks, Image = source.Image, StartDate = DateTime.Today, EndDate = DateTime.Today.AddDays((source.Weeks ?? 1) * 7 - 1), CreatedAt = DateTime.UtcNow };
        foreach (var item in source.Items.OrderBy(x => x.Order)) copy.Items.Add(new ProgramItem { Type = item.Type, DayNumber = item.DayNumber, DayOfWeek = item.DayOfWeek, Time = item.Time, Title = item.Title, Description = item.Description, RecoveryMethodId = item.RecoveryMethodId, Minutes = item.Minutes, Order = item.Order });
        db.Programs.Add(copy); await db.SaveChangesAsync();
        return RedirectToAction("Index", "MyRecovery");
    }
}
