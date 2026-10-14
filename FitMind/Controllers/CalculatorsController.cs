using System.Security.Claims;
using FitMind.Data;
using FitMind.Models;
using FitMind.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Controllers;

[Authorize]
public class CalculatorsController(ApplicationDbContext db) : Controller
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index(string? calc)
    {
        var key = NormalizeKey(calc);
        var definition = CalculatorEngine.All.FirstOrDefault(x => x.Key == key);
        if (definition is null) return NotFound();

        return View(new CalculatorPage
        {
            Calculator = key,
            Values = definition.Fields.ToDictionary(x => x.Key, x => (decimal)x.Default),
            History = await ReadHistoryAsync()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Calculate(CalculatorPage vm)
    {
        if (!CalculatorEngine.All.Any(x => x.Key == vm.Calculator)) return BadRequest();
        if (ModelState.IsValid)
        {
            try { CalculatorEngine.Calculate(vm); }
            catch (ArgumentException exception) { ModelState.AddModelError("", exception.Message); }
        }

        vm.History = await ReadHistoryAsync();
        return View("Index", vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CalculatorPage vm)
    {
        if (!CalculatorEngine.All.Any(x => x.Key == vm.Calculator)) return BadRequest();

        try { CalculatorEngine.Calculate(vm); }
        catch (ArgumentException exception)
        {
            TempData["Error"] = exception.Message;
            return RedirectToAction(nameof(Index), new { calc = vm.Calculator });
        }

        var definition = vm.Definition;
        var result = string.Join("; ", vm.Results.Select(x => $"{x.Key}: {x.Value}"));
        db.PersonalNotes.Add(new PersonalNote
        {
            UserId = UserId,
            Area = "Калькуляторы",
            Title = definition.Title,
            Text = result,
            CreatedAt = DateTime.Now
        });
        await db.SaveChangesAsync();
        TempData["Ok"] = "Результат калькулятора сохранён в истории.";
        return RedirectToAction(nameof(Index), new { calc = vm.Calculator });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id, string? calc)
    {
        var item = await db.PersonalNotes.FirstOrDefaultAsync(x => x.Id == id && x.UserId == UserId && x.Area == "Калькуляторы");
        if (item != null) { db.PersonalNotes.Remove(item); await db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index), new { calc });
    }

    private async Task<List<PersonalNote>> ReadHistoryAsync() => await db.PersonalNotes
        .Where(x => x.UserId == UserId && x.Area == "Калькуляторы")
        .OrderByDescending(x => x.CreatedAt)
        .Take(8)
        .ToListAsync();

    private static string NormalizeKey(string? calc)
    {
        var key = calc?.ToUpperInvariant() ?? "BMI";
        return key == "IDEAL" ? "RANGE" : key;
    }
}
