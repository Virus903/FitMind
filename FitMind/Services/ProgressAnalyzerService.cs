using FitMind.Data;
using FitMind.Models;
using FitMind.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace FitMind.Services;

/// <summary>
/// Детерминированный анализ прогресса. Это НЕ нейросеть: сервис сравнивает только фактически сохранённые данные.
/// Если данных недостаточно, положительный прогресс не выдумывается.
/// </summary>
public class ProgressAnalyzerService(ApplicationDbContext db)
{
    public async Task<ProgressPageVm> BuildAsync(string userId)
    {
        var vm = new ProgressPageVm();
        vm.GoalName = await db.UserProfiles.Where(x => x.UserId == userId && x.GoalId != null)
            .Select(x => x.Goal!.Name).FirstOrDefaultAsync() ?? "Цель не выбрана";

        vm.Measurements = await db.BodyMeasurements.Where(x => x.UserId == userId)
            .Include(x => x.Type).OrderByDescending(x => x.DateTime).Take(120).ToListAsync();

        var now = DateTime.Now;
        var curFrom = now.AddDays(-30);
        var prevFrom = now.AddDays(-60);
        vm.WorkoutsLast30 = await db.DayEntries.CountAsync(x => x.UserId == userId && x.Type == ProgramItemType.Workout && x.Status == DayEntryStatus.Done && x.Start >= curFrom);
        vm.WorkoutsPrevious30 = await db.DayEntries.CountAsync(x => x.UserId == userId && x.Type == ProgramItemType.Workout && x.Status == DayEntryStatus.Done && x.Start >= prevFrom && x.Start < curFrom);

        var strengthNow = (await db.EntrySets
            .Where(s => s.Done && s.WeightKg != null && s.DayEntry!.UserId == userId && s.DayEntry.Start >= curFrom)
            .Select(s => s.WeightKg).ToListAsync()).DefaultIfEmpty(null).Max();
        var strengthPrev = (await db.EntrySets
            .Where(s => s.Done && s.WeightKg != null && s.DayEntry!.UserId == userId && s.DayEntry.Start >= prevFrom && s.DayEntry.Start < curFrom)
            .Select(s => s.WeightKg).ToListAsync()).DefaultIfEmpty(null).Max();
        vm.StrengthCurrent = strengthNow;
        vm.StrengthPrevious = strengthPrev;

        var sleeps = await db.DayEntries.Where(x => x.UserId == userId && x.Type == ProgramItemType.Bedtime && x.Status == DayEntryStatus.Done && x.Start >= now.AddDays(-14) && x.End != null).ToListAsync();
        if (sleeps.Count >= 2) vm.AvgSleepHours = sleeps.Average(x => (x.End!.Value - x.Start).TotalHours);

        var meals = await db.DayEntries.Where(x => x.UserId == userId && x.Type == ProgramItemType.Meal && x.Status == DayEntryStatus.Done && x.Start >= now.AddDays(-14))
            .Include(x => x.FoodItems).ThenInclude(x => x.Product).ToListAsync();
        if (meals.Count > 0)
        {
            var byDay = meals.GroupBy(x => x.Start.Date)
                .Select(g => g.Sum(m => m.FoodItems.Sum(NutritionHelper.ItemCalories)));
            vm.AvgCalories = byDay.Any() ? Math.Round(byDay.Average(), 0) : null;
        }

        var schedule = await db.DayEntries.Where(x => x.UserId == userId && x.Start >= now.AddDays(-30) && x.Start <= now).ToListAsync();
        if (schedule.Count > 0) vm.SchedulePercent = (int)Math.Round(schedule.Count(x => x.Status == DayEntryStatus.Done) * 100.0 / schedule.Count);

        AddMeasurementComparisons(vm, "Вес");
        AddMeasurementComparisons(vm, "Талия");
        AddMeasurementComparisons(vm, "Грудь");
        AddMeasurementComparisons(vm, "Бицепс");
        AddMeasurementComparisons(vm, "Бедро");
        AddMeasurementComparisons(vm, "Процент жира");

        DetermineStatus(vm);
        return vm;
    }

    private static void AddMeasurementComparisons(ProgressPageVm vm, string name)
    {
        var rows = vm.Measurements.Where(x => x.Type?.Name == name).OrderByDescending(x => x.DateTime).Take(2).ToList();
        if (rows.Count == 0) return;
        vm.Current[name] = $"{rows[0].Value:0.#} {rows[0].Type?.Unit}";
        if (rows.Count >= 2)
        {
            var diff = rows[0].Value - rows[1].Value;
            vm.Changes[name] = $"{(diff >= 0 ? "+" : "")}{diff:0.#} {rows[0].Type?.Unit}";
        }
    }

    private static void DetermineStatus(ProgressPageVm vm)
    {
        decimal? Change(string key)
        {
            var rows = vm.Measurements.Where(x => x.Type?.Name == key).OrderByDescending(x => x.DateTime).Take(2).ToList();
            return rows.Count < 2 ? null : rows[0].Value - rows[1].Value;
        }

        var weight = Change("Вес");
        var waist = Change("Талия");
        var strengthChange = vm.StrengthCurrent.HasValue && vm.StrengthPrevious.HasValue ? vm.StrengthCurrent - vm.StrengthPrevious : null;
        var enough = weight.HasValue || waist.HasValue || strengthChange.HasValue || (vm.WorkoutsLast30 + vm.WorkoutsPrevious30 >= 4);
        if (!enough)
        {
            vm.Status = "Недостаточно данных";
            vm.StatusText = "Для объективного сравнения нужны как минимум повторные замеры и/или история выполненных тренировок. FitMind не будет придумывать прогресс без фактов.";
            return;
        }

        var positive = false;
        var negative = false;
        var goal = vm.GoalName.ToLowerInvariant();
        if (goal.Contains("мас"))
        {
            if (weight > 0 || strengthChange > 0) positive = true;
            if (weight < -1 && strengthChange <= 0) negative = true;
        }
        else if (goal.Contains("сниж") || goal.Contains("похуд"))
        {
            if (weight < 0 || waist < 0) positive = true;
            if (weight > 1 && waist > 0) negative = true;
        }
        else if (goal.Contains("сил"))
        {
            if (strengthChange > 0) positive = true;
            if (strengthChange < 0) negative = true;
        }
        else
        {
            var stableWeight = weight.HasValue && Math.Abs(weight.Value) <= 1.5m;
            if (stableWeight && vm.WorkoutsLast30 >= 2 && vm.SchedulePercent >= 50) positive = true;
            if (weight.HasValue && Math.Abs(weight.Value) > 4) negative = true;
        }

        if (negative)
        {
            vm.Status = "Есть ухудшение показателей";
            vm.StatusText = "Некоторые фактические показатели движутся не в сторону выбранной цели. Посмотри отдельные разделы и сравни записи за период.";
        }
        else if (positive)
        {
            vm.Status = "Есть подтверждённый прогресс";
            vm.StatusText = "Изменение подтверждается сохранёнными замерами и/или результатами тренировок. Ни один показатель не был сгенерирован автоматически.";
        }
        else
        {
            vm.Status = "Прогресс пока не зафиксирован";
            vm.StatusText = "Данных уже достаточно для сравнения, но устойчивого улучшения по выбранной цели пока не видно.";
        }
    }
}

public static class NutritionHelper
{
    public static decimal ItemCalories(DayEntryFoodItem i)
    {
        var per100 = i.Product?.Calories ?? i.CaloriesPer100 ?? 0;
        return Math.Round(per100 * i.Grams / 100m, 1);
    }
    public static decimal ItemProtein(DayEntryFoodItem i)
    {
        var per100 = i.Product?.Protein ?? i.ProteinPer100 ?? 0;
        return Math.Round(per100 * i.Grams / 100m, 1);
    }
    public static decimal ItemFat(DayEntryFoodItem i)
    {
        var per100 = i.Product?.Fat ?? i.FatPer100 ?? 0;
        return Math.Round(per100 * i.Grams / 100m, 1);
    }
    public static decimal ItemCarbs(DayEntryFoodItem i)
    {
        var per100 = i.Product?.Carbs ?? i.CarbsPer100 ?? 0;
        return Math.Round(per100 * i.Grams / 100m, 1);
    }
}
