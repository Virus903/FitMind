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
public sealed class MyRecoveryController(ApplicationDbContext db, IWebHostEnvironment environment) : Controller
{
    string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    IQueryable<FitnessProgram> MyPlans => db.Programs.Where(x => x.OwnerId == Uid && x.Kind == ProgramKind.Sleep);

    public async Task<IActionResult> Index(DateTime? date, DateTime? month)
    {
        var day = (date ?? DateTime.Today).Date;
        await EnsurePlanEntries(day);
        var currentMonth = month.HasValue ? new DateTime(month.Value.Year, month.Value.Month, 1) : new DateTime(day.Year, day.Month, 1);
        var rangeStart = currentMonth.AddDays(-7); var rangeEnd = currentMonth.AddMonths(1).AddDays(7);
        var records = await db.DayEntries.Where(x => x.UserId == Uid && x.Start < rangeEnd && (x.Type == ProgramItemType.Bedtime || x.Type == ProgramItemType.Procedure)).Include(x => x.RecoveryMethod).ToListAsync();
        var plans = await MyPlans.Include(x => x.Items).OrderByDescending(x => x.Active).ThenByDescending(x => x.CreatedAt).ToListAsync();
        return View(new RecoveryRedesignVm
        {
            Date = day, Month = currentMonth, WeekStart = day.AddDays(-(((int)day.DayOfWeek + 6) % 7)), Plans = plans, ActivePlan = plans.FirstOrDefault(x => x.Active),
            MyItems = await db.RecoveryMethods.Where(x => x.OwnerId == Uid && x.Active).OrderBy(x => x.Name).ToListAsync(),
            SleepRecords = records.Where(x => x.Type == ProgramItemType.Bedtime && x.Status == DayEntryStatus.Done && x.Start < day.AddDays(1) && (x.End ?? x.Start) > day).OrderBy(x => x.Start).ToList(),
            RecoveryRecords = records.Where(x => x.Type == ProgramItemType.Procedure && x.Status == DayEntryStatus.Done && x.Start >= day && x.Start < day.AddDays(1)).OrderBy(x => x.Start).ToList(),
            MarkedDates = records.Where(x => x.Status == DayEntryStatus.Done).SelectMany(x => x.Type == ProgramItemType.Bedtime && x.End.HasValue ? new[] { x.Start.Date, x.End.Value.Date } : new[] { x.Start.Date }).Distinct().ToList(),
            Notes = await db.PersonalNotes.Where(x => x.UserId == Uid && x.Area == "Сон").OrderByDescending(x => x.CreatedAt).Take(8).ToListAsync()
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
        await FillMethods();
        if (id.HasValue)
        {
            var plan = await MyPlans.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == id); if (plan is null) return NotFound();
            return View(new RecoveryPlanEditVm { Id = plan.Id, Name = plan.Name, Description = plan.Description, Image = plan.Image, Weeks = plan.Weeks ?? 4, StartDate = plan.StartDate ?? DateTime.Today, Items = plan.Items.OrderBy(x => x.Order).Select(x => new RecoveryPlanItemEditVm { Type = x.Type, Name = x.Title, Time = x.Time, DurationMinutes = x.Minutes, RecoveryMethodId = x.RecoveryMethodId, DayOfWeek = x.DayOfWeek }).ToList() });
        }
        return View(new RecoveryPlanEditVm { Items = [new() { Type = ProgramItemType.Bedtime, Name = "Отбой", Time = new(23, 0, 0) }, new() { Type = ProgramItemType.WakeUp, Name = "Подъём", Time = new(7, 0, 0) }, new() { Type = ProgramItemType.Procedure, Name = "Растяжка", Time = new(20, 0, 0), DurationMinutes = 15 }] });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreatePlan(RecoveryPlanEditVm vm)
    {
        var upload = await ContentImageStorage.SaveAsync(vm.ImageFile, environment); if (upload.Error != null) ModelState.AddModelError(nameof(vm.ImageFile), upload.Error);
        vm.Items ??= []; await FillMethods(); var items = vm.Items.Where(x => !string.IsNullOrWhiteSpace(x.Name)).ToList();
        if (!items.Any(x => x.Type == ProgramItemType.Bedtime) || !items.Any(x => x.Type == ProgramItemType.WakeUp)) ModelState.AddModelError("", "Добавьте время отбоя и подъёма.");
        if (items.Any(x => x.Type is not (ProgramItemType.Bedtime or ProgramItemType.WakeUp or ProgramItemType.Procedure) || !x.Time.HasValue || (x.Type == ProgramItemType.Procedure && !x.DurationMinutes.HasValue))) ModelState.AddModelError("", "Проверьте тип, время и длительность процедур.");
        if (items.Any(x => x.RecoveryMethodId.HasValue) && await db.RecoveryMethods.CountAsync(x => items.Where(i => i.RecoveryMethodId.HasValue).Select(i => i.RecoveryMethodId!.Value).Contains(x.Id) && x.Active && (x.OwnerId == null || x.OwnerId == Uid)) != items.Where(x => x.RecoveryMethodId.HasValue).Select(x => x.RecoveryMethodId).Distinct().Count()) ModelState.AddModelError("", "Выбран недоступный метод восстановления.");
        if (!ModelState.IsValid) return View(vm);
        FitnessProgram plan;
        if (vm.Id.HasValue) { plan = await MyPlans.Include(x => x.Items).FirstOrDefaultAsync(x => x.Id == vm.Id) ?? null!; if (plan is null) return NotFound(); db.ProgramItems.RemoveRange(plan.Items); }
        else { plan = new FitnessProgram { Kind = ProgramKind.Sleep, OwnerId = Uid, CreatedAt = DateTime.UtcNow, Image = "/images/placeholders/sleep-programs-dark.png" }; db.Programs.Add(plan); }
        plan.Name = vm.Name.Trim(); plan.Description = vm.Description?.Trim(); plan.Image = upload.Path ?? vm.Image ?? plan.Image ?? "/images/placeholders/sleep-programs-dark.png"; plan.Weeks = vm.Weeks; plan.StartDate = vm.StartDate.Date; plan.EndDate = vm.StartDate.Date.AddDays(vm.Weeks * 7 - 1); await db.SaveChangesAsync();
        short order = 1; foreach (var item in items) db.ProgramItems.Add(new ProgramItem { ProgramId = plan.Id, DayNumber = 1, DayOfWeek = item.DayOfWeek, Time = item.Time, Type = item.Type, Title = item.Name!.Trim(), RecoveryMethodId = item.RecoveryMethodId, Minutes = item.DurationMinutes, Order = order++ });
        await db.SaveChangesAsync(); TempData["Ok"] = "Программа сна и восстановления сохранена."; return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeletePlan(int id) { var plan = await MyPlans.FirstOrDefaultAsync(x => x.Id == id); if (plan is null) return NotFound(); db.Remove(plan); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }

    [HttpGet] public async Task<IActionResult> CreateItem(int? id) { if (!id.HasValue) return View(new UserRecoveryItemEditVm()); var item = await db.RecoveryMethods.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == Uid); return item is null ? NotFound() : View(new UserRecoveryItemEditVm { Id = item.Id, Name = item.Name, Category = item.Category, Description = item.ShortDescription, DurationMinutes = item.MinMinutes, Image = item.Image }); }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateItem(UserRecoveryItemEditVm vm)
    {
        var upload = await ContentImageStorage.SaveAsync(vm.ImageFile, environment); if (upload.Error != null) ModelState.AddModelError(nameof(vm.ImageFile), upload.Error); if (!ModelState.IsValid) return View(vm); var item = vm.Id.HasValue ? await db.RecoveryMethods.FirstOrDefaultAsync(x => x.Id == vm.Id && x.OwnerId == Uid) : new RecoveryMethod { OwnerId = Uid, Active = true };
        if (item is null) return NotFound(); if (!vm.Id.HasValue) db.RecoveryMethods.Add(item); item.Name = vm.Name.Trim(); item.Category = vm.Category; item.ShortDescription = vm.Description; item.MinMinutes = vm.DurationMinutes; item.MaxMinutes = vm.DurationMinutes; item.Image = upload.Path ?? vm.Image ?? item.Image ?? "/images/placeholders/recovery-dark.png";
        await db.SaveChangesAsync(); return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddFromBase(int id)
    {
        var source = await db.RecoveryMethods.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == null && x.Active); if (source is null) return NotFound();
        var item = await db.RecoveryMethods.FirstOrDefaultAsync(x => x.OwnerId == Uid && x.SourceId == id);
        if (item is null) { db.RecoveryMethods.Add(new RecoveryMethod { OwnerId = Uid, SourceId = source.Id, Name = source.Name, Category = source.Category, ShortDescription = source.ShortDescription, FullDescription = source.FullDescription, Benefits = source.Benefits, Recommendations = source.Recommendations, Warnings = source.Warnings, MinMinutes = source.MinMinutes, MaxMinutes = source.MaxMinutes, Image = source.Image, Active = true }); await db.SaveChangesAsync(); }
        else if (!item.Active) { item.Active = true; await db.SaveChangesAsync(); }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteItem(int id) { var item = await db.RecoveryMethods.FirstOrDefaultAsync(x => x.Id == id && x.OwnerId == Uid); if (item is null) return NotFound(); if (await db.DayEntries.AnyAsync(x => x.RecoveryMethodId == id) || await db.ProgramItems.AnyAsync(x => x.RecoveryMethodId == id)) item.Active = false; else db.Remove(item); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }

    [HttpGet]
    public async Task<IActionResult> AddRecord(DateTime? date, ProgramItemType type = ProgramItemType.Bedtime, int? recoveryMethodId = null)
    {
        await FillMethods(); return View(new RecoveryRecordEditVm { Date = (date ?? DateTime.Today).Date, Type = type is ProgramItemType.Bedtime or ProgramItemType.Procedure ? type : ProgramItemType.Bedtime, RecoveryMethodId = recoveryMethodId });
    }
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRecord(RecoveryRecordEditVm vm)
    {
        await FillMethods();
        if (vm.Type == ProgramItemType.Bedtime && vm.StartTime == vm.EndTime) ModelState.AddModelError("", "Начало и окончание сна не должны совпадать.");
        if (vm.Type == ProgramItemType.Procedure && !vm.RecoveryMethodId.HasValue && string.IsNullOrWhiteSpace(vm.CustomName)) ModelState.AddModelError(nameof(vm.CustomName), "Выберите метод или укажите название.");
        RecoveryMethod? method = null; if (vm.RecoveryMethodId.HasValue) { method = await db.RecoveryMethods.FirstOrDefaultAsync(x => x.Id == vm.RecoveryMethodId && x.Active && (x.OwnerId == null || x.OwnerId == Uid)); if (method is null) return NotFound(); }
        if (!ModelState.IsValid) return View(vm);
        if (vm.Type == ProgramItemType.Bedtime) { var start = vm.Date.Date.Add(vm.StartTime); var end = vm.Date.Date.Add(vm.EndTime); if (end <= start) end = end.AddDays(1); if ((end - start).TotalHours > 24) return BadRequest(); db.DayEntries.Add(new DayEntry { UserId = Uid, Type = ProgramItemType.Bedtime, Start = start, End = end, Title = "Ночной сон", Status = DayEntryStatus.Done, Rating = vm.Rating, Comment = vm.Comment }); }
        else db.DayEntries.Add(new DayEntry { UserId = Uid, Type = ProgramItemType.Procedure, Start = vm.Date.Date.Add(vm.StartTime), Title = method?.Name ?? vm.CustomName!.Trim(), RecoveryMethodId = method?.Id, Status = DayEntryStatus.Done, Value = vm.DurationMinutes, Rating = vm.Rating, Comment = vm.Comment });
        await db.SaveChangesAsync(); return RedirectToAction(nameof(Index), new { date = vm.Date.ToString("yyyy-MM-dd") });
    }

    [HttpGet] public IActionResult AddSleep(DateTime? date) => RedirectToAction(nameof(AddRecord), new { date = date?.ToString("yyyy-MM-dd"), type = ProgramItemType.Bedtime });
    [HttpGet] public IActionResult AddRecovery(int? methodId, DateTime? date) => RedirectToAction(nameof(AddRecord), new { date = date?.ToString("yyyy-MM-dd"), type = ProgramItemType.Procedure, recoveryMethodId = methodId });
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> DeleteRecord(long id) { var item = await db.DayEntries.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid && (x.Type == ProgramItemType.Bedtime || x.Type == ProgramItemType.Procedure)); if (item is null) return NotFound(); db.Remove(item); await db.SaveChangesAsync(); return RedirectToAction(nameof(Index)); }
    [HttpPost, ValidateAntiForgeryToken] public async Task<IActionResult> AddNote(string text, string? title = null) { if (!string.IsNullOrWhiteSpace(text)) { db.PersonalNotes.Add(new PersonalNote { UserId = Uid, Area = "Сон", Title = title, Text = text.Trim(), CreatedAt = DateTime.Now }); await db.SaveChangesAsync(); } return RedirectToAction(nameof(Index)); }
    public async Task<IActionResult> History() => View(new RecoveryRedesignVm { SleepRecords = await db.DayEntries.Where(x => x.UserId == Uid && x.Type == ProgramItemType.Bedtime && x.Status == DayEntryStatus.Done).OrderByDescending(x => x.Start).ToListAsync(), RecoveryRecords = await db.DayEntries.Where(x => x.UserId == Uid && x.Type == ProgramItemType.Procedure && x.Status == DayEntryStatus.Done).Include(x => x.RecoveryMethod).OrderByDescending(x => x.Start).ToListAsync(), Notes = await db.PersonalNotes.Where(x => x.UserId == Uid && x.Area == "Сон").OrderByDescending(x => x.CreatedAt).ToListAsync() });

    async Task FillMethods() => ViewBag.Methods = await db.RecoveryMethods.Where(x => x.Active && (x.OwnerId == null || x.OwnerId == Uid)).OrderByDescending(x => x.OwnerId == Uid).ThenBy(x => x.Name).ToListAsync();
    async Task EnsurePlanEntries(DateTime day)
    {
        var plan = await MyPlans.Include(x => x.Items).FirstOrDefaultAsync(x => x.Active); if (plan is null) return;
        foreach (var item in plan.Items.Where(x => x.DayOfWeek == null || x.DayOfWeek == day.DayOfWeek)) { var start = day.Add(item.Time ?? TimeSpan.Zero); if (!await db.DayEntries.AnyAsync(x => x.UserId == Uid && x.ProgramItemId == item.Id && x.Start == start)) db.DayEntries.Add(new DayEntry { UserId = Uid, ProgramItemId = item.Id, RecoveryMethodId = item.RecoveryMethodId, Type = item.Type, Start = start, Title = item.Title, Status = DayEntryStatus.Planned, Value = item.Minutes }); }
        await db.SaveChangesAsync();
    }
}
