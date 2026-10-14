using FitMind.Data;
using FitMind.Models;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Services;

public static class ScheduleSyncService
{
    public static async Task SyncAsync(ApplicationDbContext db, string uid, DateTime day)
    {
        await using var transaction = db.Database.CurrentTransaction == null ? await db.Database.BeginTransactionAsync() : null;
        await db.Database.ExecuteSqlInterpolatedAsync($@"DECLARE @result int;
EXEC @result = sp_getapplock @Resource = {"FitMind.ScheduleSync." + uid}, @LockMode = 'Exclusive', @LockOwner = 'Transaction', @LockTimeout = 15000;
IF @result < 0 THROW 50001, N'Расписание пользователя занято другим запросом, повторите позже.', 1;");
        day = day.Date;
        await ProgramScheduleService.EnsureActivePlannedAsync(db, uid);
        var programs = await db.Programs.Where(x => x.OwnerId == uid && x.Active && x.Kind != ProgramKind.Workout)
            .Include(x => x.Items).ThenInclude(x => x.Supplement).ToListAsync();
        var ids = programs.SelectMany(x => x.Items).Select(x => x.Id).ToList();
        var existing = await db.DayEntries.Where(x => x.UserId == uid && x.ProgramItemId != null && ids.Contains(x.ProgramItemId.Value)
            && x.Start >= day && x.Start < day.AddDays(1)).Select(x => x.ProgramItemId!.Value).ToListAsync();
        foreach (var program in programs.Where(x => day >= (x.StartDate ?? day).Date && day <= (x.EndDate ?? day).Date))
        foreach (var item in program.Items.Where(x => x.Time.HasValue && (x.DayOfWeek == null || x.DayOfWeek == day.DayOfWeek) && !existing.Contains(x.Id)))
            db.DayEntries.Add(new DayEntry { UserId = uid, Start = day.Add(item.Time!.Value), Type = item.Type,
                ProgramItemId = item.Id, RecoveryMethodId = item.RecoveryMethodId, SupplementId = item.SupplementId,
                Title = item.Supplement?.Name ?? item.Title, Status = DayEntryStatus.Planned,
                Value = item.Type switch { ProgramItemType.Water => item.WaterMl, ProgramItemType.Procedure => item.Minutes, _ => item.Calories },
                Comment = item.Dose ?? program.Name });
        await db.SaveChangesAsync();
        if (transaction != null) await transaction.CommitAsync();
    }
}
