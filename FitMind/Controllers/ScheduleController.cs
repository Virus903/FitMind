using FitMind.Data;
using FitMind.Models;
using FitMind.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace FitMind.Controllers;

[Authorize]
public class ScheduleController(ApplicationDbContext db) : Controller
{
    string Uid => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    public async Task<IActionResult> Index(DateTime? date)
    {
        var day = (date ?? DateTime.Today).Date;
        await Services.ScheduleSyncService.SyncAsync(db, Uid, day);
        var items = await db.DayEntries.Where(x => x.UserId == Uid && x.Start >= day && x.Start < day.AddDays(1) && x.Status != DayEntryStatus.Skipped)
            .Include(x => x.ProgramItem).ThenInclude(x => x!.Program).Include(x => x.FoodItems).ThenInclude(x => x.Product)
            .OrderBy(x => x.Start).ToListAsync();
        var profile = await db.UserProfiles.AsNoTracking().FirstOrDefaultAsync(x => x.UserId == Uid);
        var next = items.FirstOrDefault(x => x.Status == DayEntryStatus.Planned && x.Start >= DateTime.Now)
            ?? items.FirstOrDefault(x => x.Status == DayEntryStatus.Planned);
        return View(new MyDayPageVm { UserName = string.IsNullOrWhiteSpace(profile?.Name) ? User.Identity?.Name?.Split('@')[0] ?? "Пользователь" : profile.Name,
            Date = day, Items = items, NextEvent = next });
    }

    [HttpGet]
    public async Task<IActionResult> Add(DateTime? date, ProgramItemType? type)
        => View(await FillLists(new ScheduleEditVm { Date = (date ?? DateTime.Today).Date,
            Time = new TimeSpan(DateTime.Now.Hour, DateTime.Now.Minute, 0), Type = type ?? ProgramItemType.Custom }));

    [HttpGet]
    public async Task<IActionResult> Edit(long id)
    {
        var x = await db.DayEntries.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid);
        if (x == null) return NotFound();
        return View(await FillLists(new ScheduleEditVm { Id = x.Id, Date = x.Start.Date, Time = x.Start.TimeOfDay,
            Type = x.Type, Title = x.Title, Comment = x.Comment, Status = x.Status, End = x.End,
            Value = x.Value, Rating = x.Rating, RecoveryMethodId = x.RecoveryMethodId, SupplementId = x.SupplementId }));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(ScheduleEditVm vm)
    {
        if (vm.Time < TimeSpan.Zero || vm.Time >= TimeSpan.FromDays(1) || vm.Date.Year is < 2000 or > 2100)
            ModelState.AddModelError("", "Проверьте дату и время.");
        if (string.IsNullOrWhiteSpace(vm.Title)) vm.Title = DefaultTitle(vm.Type);
        if (vm.RecoveryMethodId.HasValue && !await db.RecoveryMethods.AnyAsync(x => x.Id == vm.RecoveryMethodId && (x.OwnerId == null || x.OwnerId == Uid)))
            ModelState.AddModelError(nameof(vm.RecoveryMethodId), "Метод восстановления недоступен.");
        if (vm.SupplementId.HasValue && !await db.Supplements.AnyAsync(x => x.Id == vm.SupplementId && (x.OwnerId == null || x.OwnerId == Uid)))
            ModelState.AddModelError(nameof(vm.SupplementId), "Добавка недоступна.");
        if (!ModelState.IsValid) return View(vm.Id.HasValue ? "Edit" : "Add", await FillLists(vm));
        DayEntry x;
        if (vm.Id.HasValue) { x = await db.DayEntries.FirstOrDefaultAsync(x => x.Id == vm.Id && x.UserId == Uid) ?? null!; if (x == null) return NotFound(); }
        else { x = new DayEntry { UserId = Uid }; db.DayEntries.Add(x); }
        x.Start = vm.Date.Date.Add(vm.Time); x.End = vm.End; x.Type = vm.Type; x.Title = vm.Title.Trim();
        x.Comment = vm.Comment; x.Status = vm.Status; x.Value = vm.Value; x.Rating = vm.Rating;
        x.RecoveryMethodId = vm.Type == ProgramItemType.Procedure ? vm.RecoveryMethodId : null;
        x.SupplementId = vm.Type == ProgramItemType.Supplement ? vm.SupplementId : null;
        await db.SaveChangesAsync();
        TempData["Ok"] = "Пункт дня сохранён.";
        return RedirectToAction(nameof(Index), new { date = vm.Date.ToString("yyyy-MM-dd") });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SetStatus(long id, DayEntryStatus status)
    {
        var x = await db.DayEntries.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid);
        if (x == null) return NotFound();
        x.Status = status;
        if (status == DayEntryStatus.Done && x.Type is ProgramItemType.Workout or ProgramItemType.Bedtime) x.End ??= DateTime.Now;
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { date = x.Start.ToString("yyyy-MM-dd") });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(long id)
    {
        var x = await db.DayEntries.FirstOrDefaultAsync(x => x.Id == id && x.UserId == Uid);
        if (x == null) return NotFound();
        var date = x.Start.Date;
        db.Notifications.RemoveRange(await db.Notifications.Where(n => n.UserId == Uid && n.Link == "/Schedule/Edit/" + id).ToListAsync());
        if (x.ProgramItemId.HasValue)
        {
            // Сохраняем маркер пропуска: иначе синхронизация активной программы создаст пункт повторно.
            x.Status = DayEntryStatus.Skipped;
        }
        else
        {
            db.DayEntries.Remove(x);
        }
        await db.SaveChangesAsync();
        return RedirectToAction(nameof(Index), new { date = date.ToString("yyyy-MM-dd") });
    }

    async Task<ScheduleEditVm> FillLists(ScheduleEditVm vm)
    {
        vm.RecoveryMethods = await db.RecoveryMethods.Where(x => x.OwnerId == null || x.OwnerId == Uid).OrderBy(x => x.Name).ToListAsync();
        vm.Supplements = await db.Supplements.Where(x => x.OwnerId == null || x.OwnerId == Uid).OrderBy(x => x.Name).ToListAsync();
        return vm;
    }

    static string DefaultTitle(ProgramItemType type) => type switch { ProgramItemType.Workout => "Тренировка", ProgramItemType.Meal => "Приём пищи",
        ProgramItemType.Water => "Вода", ProgramItemType.Bedtime => "Сон", ProgramItemType.Procedure => "Процедура",
        ProgramItemType.Supplement => "Приём добавки", _ => "Личный пункт" };
}
