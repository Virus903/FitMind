using System.Security.Claims;
using FitMind.Data;
using FitMind.Models;
using FitMind.Services;
using FitMind.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Controllers;

[Authorize]
public sealed class MySupplementsController(ApplicationDbContext db, IWebHostEnvironment environment) : Controller
{
    string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    IQueryable<FitnessProgram> MyPlans => db.Programs.Where(x => x.OwnerId == Uid && x.Kind == ProgramKind.Supplements);

    public async Task<IActionResult> Index(DateTime? date, DateTime? month)
    {
        var day = (date ?? DateTime.Today).Date;
        await EnsurePlanEntries(day);
        var plans = await MyPlans.Include(x => x.Items).ThenInclude(x => x.Supplement).OrderByDescending(x => x.Active).ThenByDescending(x => x.CreatedAt).ToListAsync();
        return View(new SupplementsRedesignVm
        {
            Date = day,
            Month = month.HasValue ? new DateTime(month.Value.Year, month.Value.Month, 1) : new DateTime(day.Year, day.Month, 1),
            WeekStart = day.AddDays(-(((int)day.DayOfWeek + 6) % 7)),
            Plans = plans,
            ActivePlan = plans.FirstOrDefault(x => x.Active),
            Items = await db.Supplements.Where(x => x.OwnerId == Uid && x.Active).Include(x => x.Source).ThenInclude(x => x!.Category).Include(x => x.Category).OrderBy(x => x.Name).ToListAsync(),
            TodayEntries = await db.DayEntries.Where(x => x.UserId == Uid && x.Type == ProgramItemType.Supplement && x.Start >= day && x.Start < day.AddDays(1)).Include(x => x.Supplement).OrderBy(x => x.Start).ToListAsync(),
            RecentIntakes = await db.DayEntries.Where(x => x.UserId == Uid && x.Type == ProgramItemType.Supplement && x.Status != DayEntryStatus.Planned).Include(x => x.Supplement).OrderByDescending(x => x.Start).Take(10).ToListAsync(),
            Notes = await db.PersonalNotes.Where(x => x.UserId == Uid && x.Area == "Добавки").OrderByDescending(x => x.CreatedAt).Take(8).ToListAsync()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetPlanActive(int id, bool active = true)
    {
        var plan = await MyPlans.FirstOrDefaultAsync(x => x.Id == id); if (plan is null) return NotFound();
        foreach (var other in await MyPlans.Where(x => x.Active && x.Id != id).ToListAsync())
        {
            other.Active = false;
            await ProgramScheduleService.UnplanFutureAsync(db, other);
        }
        plan.Active = active; await db.SaveChangesAsync(); if (active) await EnsurePlanEntries(DateTime.Today);
        else await ProgramScheduleService.UnplanFutureAsync(db, plan);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> CreatePlan(int? id)
    {
        await FillSupplements();
        if (!id.HasValue) return View(new SupplementPlanEditVm { Items = [new()] });
        var plan = await MyPlans.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id); if (plan is null) return NotFound();
        return View(new SupplementPlanEditVm
        {
            Id = plan.Id, Name = plan.Name, Description = plan.Description, Image = plan.Image, Weeks = plan.Weeks ?? 8, StartDate = plan.StartDate ?? DateTime.Today,
            Items = plan.Items.GroupBy(x => new { x.SupplementId, x.Dose, x.Time }).Select(x => new SupplementPlanItemEditVm { SupplementId = x.Key.SupplementId, Dose = x.Key.Dose, Time = x.Key.Time, Days = x.Where(i => i.DayOfWeek.HasValue).Select(i => i.DayOfWeek!.Value).Distinct().ToList() }).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePlan(SupplementPlanEditVm vm)
    {
        var upload = await ContentImageStorage.SaveAsync(vm.ImageFile, environment); if (upload.Error != null) ModelState.AddModelError(nameof(vm.ImageFile), upload.Error);
        vm.Items ??= []; await FillSupplements(); var rows = vm.Items.Where(x => x.SupplementId.HasValue || x.Time.HasValue || !string.IsNullOrWhiteSpace(x.Dose) || x.Days.Count != 0).ToList();
        if (rows.Count == 0) ModelState.AddModelError("", "Добавьте хотя бы один приём.");
        if (rows.Any(x => !x.SupplementId.HasValue || !x.Time.HasValue || string.IsNullOrWhiteSpace(x.Dose) || x.Days.Count == 0)) ModelState.AddModelError("", "Для каждого приёма выберите добавку, дозировку, время и дни недели.");
        var ids = rows.Where(x => x.SupplementId.HasValue).Select(x => x.SupplementId!.Value).Distinct().ToList();
        if (await db.Supplements.CountAsync(x => ids.Contains(x.Id) && x.OwnerId == Uid && x.Active) != ids.Count) ModelState.AddModelError("", "В схеме есть недоступная добавка.");
        if (!ModelState.IsValid) return View(vm);
        FitnessProgram plan;
        if (vm.Id.HasValue) { plan = await MyPlans.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == vm.Id) ?? null!; if (plan is null) return NotFound(); db.ProgramItems.RemoveRange(plan.Items); }
        else { plan = new FitnessProgram { Kind = ProgramKind.Supplements, OwnerId = Uid, CreatedAt = DateTime.UtcNow }; db.Programs.Add(plan); }
        plan.Name = vm.Name.Trim(); plan.Description = vm.Description?.Trim(); plan.Image = upload.Path ?? vm.Image ?? plan.Image ?? "/images/placeholders/supplements-dark.png"; plan.Weeks = vm.Weeks; plan.StartDate = vm.StartDate.Date; plan.EndDate = vm.StartDate.Date.AddDays(vm.Weeks * 7 - 1); await db.SaveChangesAsync();
        short order = 1;
        foreach (var row in rows) foreach (var day in row.Days.Distinct()) db.ProgramItems.Add(new ProgramItem { ProgramId = plan.Id, DayNumber = 1, DayOfWeek = day, Time = row.Time, Type = ProgramItemType.Supplement, SupplementId = row.SupplementId, Title = (await db.Supplements.Where(x => x.Id == row.SupplementId).Select(x => x.Name).SingleAsync()), Dose = row.Dose!.Trim(), Order = order++ });
        await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePlan(int id) { var plan = await MyPlans.FirstOrDefaultAsync(x => x.Id == id); if (plan is null) return NotFound(); db.Remove(plan); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }

    [HttpGet] public async Task<IActionResult> Add() { await FillCategories(); return View("Edit", new UserSupplementEditVm()); }
    [HttpGet] public async Task<IActionResult> Edit(int id) { await FillCategories(); var item = await db.Supplements.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == Uid); return item is null ? NotFound() : View(new UserSupplementEditVm { Id = item.Id, Name = item.Name, CategoryId = item.CategoryId, Description = item.ShortDescription, Image = item.Image }); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(UserSupplementEditVm vm)
    {
        await FillCategories(); var upload = await ContentImageStorage.SaveAsync(vm.ImageFile, environment); if (upload.Error != null) ModelState.AddModelError(nameof(vm.ImageFile), upload.Error); if (!await db.Categories.AnyAsync(x => x.Section=="Добавки" && x.Id == vm.CategoryId)) ModelState.AddModelError(nameof(vm.CategoryId), "Выберите категорию."); if (!ModelState.IsValid) return View(vm);
        var item = vm.Id.HasValue ? await db.Supplements.FirstOrDefaultAsync(x => x.Id == vm.Id && x.OwnerId == Uid) : new Supplement { OwnerId = Uid, Active = true };
        if (item is null) return NotFound(); if (!vm.Id.HasValue) db.Supplements.Add(item); item.Name = vm.Name.Trim(); item.CategoryId = vm.CategoryId; item.ShortDescription = vm.Description?.Trim(); item.Image = upload.Path ?? vm.Image ?? item.Image ?? "/images/placeholders/supplements-dark.png"; await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFromBase(int id)
    {
        var source = await db.Supplements.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == null && x.Active); if (source is null) return NotFound();
        var item = await db.Supplements.FirstOrDefaultAsync(x => x.OwnerId == Uid && x.SourceId == id);
        if (item is null) { db.Supplements.Add(new Supplement { OwnerId = Uid, SourceId = source.Id, CategoryId = source.CategoryId, Name = source.Name, ShortDescription = source.ShortDescription, Purpose = source.Purpose, Usage = source.Usage, StandardDosage = source.StandardDosage, Form = source.Form, BestTime = source.BestTime, Benefits = source.Benefits, Drawbacks = source.Drawbacks, Warning = source.Warning, Faq = source.Faq, Image = source.Image, Active = true }); await db.SaveChangesAsync(); }
        else if (!item.Active) { item.Active = true; await db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Take(long id, string status, DateTime? date = null)
    {
        var entry = await db.DayEntries.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid && x.Type == ProgramItemType.Supplement); if (entry is null) return NotFound();
        if (status is not ("Принято" or "Пропущено")) return BadRequest(); var day = (date ?? entry.Start.Date).Date; if (day > DateTime.Today) return BadRequest("Нельзя отметить приём в будущем.");
        entry.Status = status == "Принято" ? DayEntryStatus.Done : DayEntryStatus.Skipped;
        await db.SaveChangesAsync(); return RedirectToAction(nameof(Index), new { date = day.ToString("yyyy-MM-dd") });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id) { var item = await db.Supplements.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == Uid); if (item is null) return NotFound(); if (await db.ProgramItems.AnyAsync(x => x.SupplementId == id) || await db.DayEntries.AnyAsync(x => x.SupplementId == id)) item.Active = false; else db.Remove(item); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteEntry(long id) { var entry = await db.DayEntries.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid && x.Type == ProgramItemType.Supplement); if (entry is null) return NotFound(); db.Remove(entry); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> AddNote(string text, string? title = null) { if (!string.IsNullOrWhiteSpace(text)) { db.PersonalNotes.Add(new PersonalNote { UserId = Uid, Area = "Добавки", Title = title, Text = text.Trim(), CreatedAt = DateTime.Now }); await db.SaveChangesAsync(); } return RedirectToAction(nameof(Index)); }
    public async Task<IActionResult> History() => View(await db.DayEntries.Where(x => x.UserId == Uid && x.Type == ProgramItemType.Supplement && x.Status != DayEntryStatus.Planned).Include(x => x.Supplement).OrderByDescending(x => x.Start).ToListAsync());

    async Task FillSupplements() => ViewBag.Items = await db.Supplements.Where(x => x.OwnerId == Uid && x.Active).OrderBy(x => x.Name).ToListAsync();
    async Task FillCategories() => ViewBag.Categories = await db.Categories.Where(x=>x.Section=="Добавки").OrderBy(x => x.Name).ToListAsync();
    async Task EnsurePlanEntries(DateTime day)
    {
        var plan = await MyPlans.Include(x => x.Items).ThenInclude(x => x.Supplement).FirstOrDefaultAsync(x => x.Active); if (plan is null || (plan.StartDate.HasValue && day < plan.StartDate.Value.Date) || (plan.EndDate.HasValue && day > plan.EndDate.Value.Date)) return;
        foreach (var item in plan.Items.Where(x => x.DayOfWeek == day.DayOfWeek && x.Time.HasValue)) { var start = day.Add(item.Time!.Value); if (!await db.DayEntries.AnyAsync(x => x.UserId == Uid && x.ProgramItemId == item.Id && x.Start == start)) db.DayEntries.Add(new DayEntry { UserId = Uid, ProgramItemId = item.Id, SupplementId = item.SupplementId, Type = ProgramItemType.Supplement, Start = start, Title = item.Supplement?.Name ?? item.Title, Status = DayEntryStatus.Planned, Comment = item.Dose }); }
        await db.SaveChangesAsync();
    }
}
