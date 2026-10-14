using FitMind.Data;
using FitMind.Models;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Services;

/// <summary>
/// Расписание программ (шаг 5): активная программа создаёт записи дня (DayEntry, Planned) на свои дни
/// недели и время; при выключении будущие запланированные записи удаляются, прошлые остаются.
/// </summary>
public static class ProgramScheduleService
{
    // Не планируем больше полугода от начала, даже если программа длиннее.
    const int MaxDaysAhead = 182;

    public static DateTime ProgramEnd(FitnessProgram p)
    {
        var start = (p.StartDate ?? DateTime.Today).Date;
        return p.EndDate?.Date ?? start.AddDays((p.Weeks ?? 8) * 7 - 1);
    }

    /// <summary>Создаёт недостающие записи дня для активной программы, начиная с сегодняшнего дня.</summary>
    public static async Task<int> PlanAsync(ApplicationDbContext db, FitnessProgram program)
    {
        if (!program.Active || program.OwnerId == null) return 0;
        var items = program.Items.Count > 0 ? program.Items
            : await db.ProgramItems.Where(x => x.ProgramId == program.Id).ToListAsync();
        var timed = items.Where(x => x.Time.HasValue).ToList();
        if (timed.Count == 0) return 0;

        var from = (program.StartDate ?? DateTime.Today).Date;
        if (from < DateTime.Today) from = DateTime.Today;
        var to = ProgramEnd(program);
        if (to > from.AddDays(MaxDaysAhead)) to = from.AddDays(MaxDaysAhead);
        if (to < from) return 0;

        var ids = timed.Select(x => x.Id).ToList();
        var existing = (await db.DayEntries
            .Where(x => x.UserId == program.OwnerId && x.ProgramItemId != null && ids.Contains(x.ProgramItemId.Value) && x.Start >= from && x.Start < to.AddDays(1))
            .Select(x => new { x.ProgramItemId, x.Start })
            .ToListAsync())
            .Select(x => (x.ProgramItemId!.Value, x.Start.Date)).ToHashSet();

        var created = 0;
        for (var day = from; day <= to; day = day.AddDays(1))
        {
            foreach (var item in timed.Where(x => x.DayOfWeek == null || x.DayOfWeek == day.DayOfWeek).OrderBy(x => x.Time).ThenBy(x => x.Order))
            {
                var start = day.Add(item.Time!.Value);
                if (start < DateTime.Now || existing.Contains((item.Id, day))) continue;
                db.DayEntries.Add(new DayEntry
                {
                    UserId = program.OwnerId,
                    Start = start,
                    Type = item.Type,
                    ProgramItemId = item.Id,
                    Title = item.Title,
                    Status = DayEntryStatus.Planned,
                    Value = program.AverageDurationMinutes ?? 60,
                    Comment = program.Name
                });
                created++;
            }
        }
        await db.SaveChangesAsync();
        return created;
    }

    /// <summary>
    /// Активная программа, перенесённая из старых таблиц (или активированная до перехода), ещё не имеет
    /// будущих записей: планируем её один раз. Если будущие записи уже есть, ничего не делаем,
    /// чтобы не восстанавливать перенесённые или удалённые пользователем тренировки.
    /// </summary>
    public static async Task EnsureActivePlannedAsync(ApplicationDbContext db, string uid)
    {
        var program = await db.Programs.Include(x => x.Items)
            .Where(x => x.OwnerId == uid && x.Kind == ProgramKind.Workout && x.Active)
            .OrderByDescending(x => x.StartDate).FirstOrDefaultAsync();
        if (program == null) return;
        var itemIds = program.Items.Select(x => (int?)x.Id).ToList();
        var now = DateTime.Now;
        if (await db.DayEntries.AnyAsync(x => x.UserId == uid && itemIds.Contains(x.ProgramItemId) && x.Start >= now)) return;
        if (ProgramEnd(program) < DateTime.Today) return;
        await PlanAsync(db, program);
    }

    /// <summary>Удаляет будущие запланированные записи программы (без выполненных подходов) и их пункты в «Моём дне».</summary>
    public static async Task<int> UnplanFutureAsync(ApplicationDbContext db, FitnessProgram program)
    {
        var itemIds = await db.ProgramItems.Where(x => x.ProgramId == program.Id).Select(x => x.Id).ToListAsync();
        var from = DateTime.Today;
        var future = await db.DayEntries
            .Where(x => x.UserId == program.OwnerId && x.ProgramItemId != null && itemIds.Contains(x.ProgramItemId.Value)
                && x.Status == DayEntryStatus.Planned && x.Start >= from && !x.Sets.Any(s => s.Done))
            .ToListAsync();
        if (future.Count == 0) return 0;
        var links = future.Select(x => "/Schedule/Edit/" + x.Id).ToList();
        db.Notifications.RemoveRange(await db.Notifications.Where(x => x.UserId == program.OwnerId && links.Contains(x.Link!)).ToListAsync());
        db.DayEntries.RemoveRange(future);
        await db.SaveChangesAsync();
        return future.Count;
    }

    /// <summary>Подходы записи создаются из упражнений пункта программы при первом открытии.</summary>
    public static async Task EnsureSetsAsync(ApplicationDbContext db, DayEntry entry)
    {
        if (entry.Sets.Count > 0 || entry.ProgramItemId == null) return;
        var exercises = await db.ProgramItemExercises.Where(x => x.ProgramItemId == entry.ProgramItemId).OrderBy(x => x.Order).ToListAsync();
        foreach (var source in exercises)
            for (short n = 1; n <= Math.Clamp((int)source.Sets, 1, 30); n++)
                entry.Sets.Add(new EntrySet { ExerciseId = source.ExerciseId, Number = n, Reps = source.RepsMax ?? source.RepsMin, WeightKg = source.WeightKg, Seconds = source.Seconds });
        if (entry.Sets.Count > 0) await db.SaveChangesAsync();
    }

    public static string StatusText(DayEntry x) => x.Status switch
    {
        DayEntryStatus.Done => "Завершена",
        DayEntryStatus.Skipped => "Пропущена",
        _ => x.Sets.Any(s => s.Done) ? "В процессе" : "Запланирована"
    };

    public static string ToScheduleStatus(DayEntry x) => x.Status switch
    {
        DayEntryStatus.Done => "Выполнено",
        DayEntryStatus.Skipped => "Пропущено",
        _ => x.Sets.Any(s => s.Done) ? "Частично" : "Запланировано"
    };

}
