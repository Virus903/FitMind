using System.ComponentModel.DataAnnotations;
using FitMind.Models;

namespace FitMind.ViewModels;

public class DashboardVm
{
    public UserProfile? Profile { get; set; }
    public string GoalName { get; set; } = "Цель не выбрана";
    public List<DayEntry> Schedule { get; set; } = [];
    public DayEntry? TodayWorkout { get; set; }
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Fat { get; set; }
    public decimal Carbs { get; set; }
    public int WaterMl { get; set; }
    public DayEntry? LastSleep { get; set; }
    public int SupplementsTaken { get; set; }
    public int SupplementsTotal { get; set; }
    public int ScheduleDone { get; set; }
    public int ScheduleTotal { get; set; }
}

public class SchedulePageVm
{
    public DateTime Date { get; set; } = DateTime.Today;
    public List<DayEntry> Items { get; set; } = [];
}

public class ScheduleEditVm
{
    public long? Id { get; set; }
    [Required] public DateTime Date { get; set; } = DateTime.Today;
    [Required] public TimeSpan Time { get; set; } = DateTime.Now.TimeOfDay;
    [Required] public ProgramItemType Type { get; set; } = ProgramItemType.Custom;
    [Required, StringLength(160)] public string Title { get; set; } = "";
    [StringLength(600)] public string? Comment { get; set; }
    [Required] public DayEntryStatus Status { get; set; } = DayEntryStatus.Planned;
    public DateTime? End { get; set; }
    [Range(0, 100000)] public decimal? Value { get; set; }
    [Range(1, 5)] public byte? Rating { get; set; }
    public int? RecoveryMethodId { get; set; }
    public int? SupplementId { get; set; }
    public List<RecoveryMethod> RecoveryMethods { get; set; } = [];
    public List<Supplement> Supplements { get; set; } = [];
}

public class WorkoutCreateVm
{
    public long? Id { get; set; }
    [Required, StringLength(160)] public string Name { get; set; } = "";
    public string? Type { get; set; }
    [Required] public DateTime StartAt { get; set; } = DateTime.Now.AddHours(1);
    [Range(1, 600)] public short? PlannedDurationMinutes { get; set; } = 60;
    public string? Note { get; set; }
    public List<int> ExerciseIds { get; set; } = [];
    public List<string> CustomExercises { get; set; } = [];
}

public class WorkoutRunVm
{
    public DayEntry Workout { get; set; } = new();
    public List<RunExerciseVm> Exercises { get; set; } = [];
    public string StatusText { get; set; } = "Запланирована";
}

/// <summary>Упражнение записи дня: подходы одной строки выполнения.</summary>
public class RunExerciseVm
{
    public Exercise Exercise { get; set; } = new();
    public List<EntrySet> Sets { get; set; } = [];
}

public class FoodDiaryPageVm
{
    public DateTime Date { get; set; } = DateTime.Today;
    public List<DayEntry> Entries { get; set; } = [];
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Fat { get; set; }
    public decimal Carbs { get; set; }
    public int WaterMl { get; set; }
}

public class MealEditVm
{
    public long? EntryId { get; set; }
    public ProgramItemType Type { get; set; } = ProgramItemType.Meal;
    public DateTime Date { get; set; } = DateTime.Today;
    public TimeSpan Time { get; set; } = DateTime.Now.TimeOfDay;
    public string? Name { get; set; }
    public string? Comment { get; set; }
    public int? ProductId { get; set; }
    public string? CustomName { get; set; }
    [Range(1, 5000)] public decimal Grams { get; set; } = 100;
    public decimal? CaloriesPer100 { get; set; }
    public decimal? ProteinPer100 { get; set; }
    public decimal? FatPer100 { get; set; }
    public decimal? CarbsPer100 { get; set; }
}

public class FoodLibraryVm
{
    public List<FoodProduct> Products { get; set; } = [];
    public List<FoodProduct> Recipes { get; set; } = [];
    public string? Query { get; set; }
    public string? Type { get; set; }
    public string? Category { get; set; }
    public string? Sort { get; set; }
}

public class MyRecoveryPageVm
{
    public List<DayEntry> SleepRecords { get; set; } = [];
    public List<DayEntry> RecoveryRecords { get; set; } = [];
    public double AverageSleepHours { get; set; }
    public double AverageSleepQuality { get; set; }
}

public class SleepEditVm
{
    public DateTime Date { get; set; } = DateTime.Today;
    public TimeSpan StartTime { get; set; } = new(23, 0, 0);
    public TimeSpan EndTime { get; set; } = new(7, 0, 0);
    [Range(1, 5)] public byte? Quality { get; set; }
    public string? MorningFeeling { get; set; }
    public string? Comment { get; set; }
}

public class RecoveryEditVm
{
    public int? RecoveryMethodId { get; set; }
    public string? CustomName { get; set; }
    public DateTime Date { get; set; } = DateTime.Today;
    public TimeSpan Time { get; set; } = DateTime.Now.TimeOfDay;
    [Range(1, 1440)] public short? DurationMinutes { get; set; }
    [Range(1,5)] public byte? WellbeingBefore { get; set; }
    [Range(1,5)] public byte? WellbeingAfter { get; set; }
    public string? Comment { get; set; }
}

public class RecoveryRecordEditVm
{
    public ProgramItemType Type { get; set; } = ProgramItemType.Bedtime;
    public DateTime Date { get; set; } = DateTime.Today;
    public TimeSpan StartTime { get; set; } = new(23, 0, 0);
    public TimeSpan EndTime { get; set; } = new(7, 0, 0);
    public int? RecoveryMethodId { get; set; }
    public string? CustomName { get; set; }
    [Range(1, 1440)] public short? DurationMinutes { get; set; }
    [Range(1, 5)] public byte? Rating { get; set; }
    public string? Comment { get; set; }
}

public class MySupplementsPageVm
{
    public List<Supplement> Items { get; set; } = [];
    public int TakenToday { get; set; }
    public int TotalToday { get; set; }
}

public class UserSupplementEditVm
{
    public int? Id { get; set; }
    [Required, StringLength(160)] public string Name { get; set; } = "";
    public int CategoryId { get; set; }
    [StringLength(1000)] public string? Description { get; set; }
    public string? Image { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile? ImageFile { get; set; }
}

public class ProgressPageVm
{
    public string GoalName { get; set; } = "Цель не выбрана";
    public string Status { get; set; } = "Недостаточно данных";
    public string StatusText { get; set; } = "Добавь несколько фактических записей, чтобы сравнение стало возможным.";
    public Dictionary<string, string> Current { get; set; } = [];
    public Dictionary<string, string> Changes { get; set; } = [];
    public List<BodyMeasurement> Measurements { get; set; } = [];
    public int WorkoutsLast30 { get; set; }
    public int WorkoutsPrevious30 { get; set; }
    public decimal? StrengthCurrent { get; set; }
    public decimal? StrengthPrevious { get; set; }
    public double? AvgSleepHours { get; set; }
    public decimal? AvgCalories { get; set; }
    public int SchedulePercent { get; set; }
}

public class BodyMeasurementsVm
{
    public DateTime Date { get; set; } = DateTime.Today;
    public decimal? Weight { get; set; }
    public decimal? Waist { get; set; }
    public decimal? Chest { get; set; }
    public decimal? Biceps { get; set; }
    public decimal? Thigh { get; set; }
    public decimal? BodyFat { get; set; }
    public string? Comment { get; set; }
}

public class CalculatorInputVm
{
    public string Calculator { get; set; } = "BMI";
    public string Gender { get; set; } = "Мужской";
    public int Age { get; set; } = 25;
    public decimal HeightCm { get; set; } = 175;
    public decimal WeightKg { get; set; } = 70;
    public decimal Activity { get; set; } = 1.55m;
    public string Goal { get; set; } = "Поддержание";
    public decimal? WaistCm { get; set; }
    public decimal? NeckCm { get; set; }
    public decimal? HipCm { get; set; }
    public int? HeartRateRest { get; set; }
    public int? WorkoutMinutes { get; set; }
    public decimal? LiftWeightKg { get; set; }
    public int? Reps { get; set; }
}

public class CalculatorResultVm
{
    public CalculatorInputVm Input { get; set; } = new();
    public string Title { get; set; } = "";
    public string MainValue { get; set; } = "";
    public string Explanation { get; set; } = "";
    public Dictionary<string,string> Extra { get; set; } = [];
}

// Изменение отдельной тренировки (бывший WorkoutManagerController).
public class WorkoutManageVm
{
    public long Id { get; set; }
    [Required, StringLength(160)] public string Name { get; set; } = "";
    [StringLength(2000)] public string? Note { get; set; }
    [Range(1, 1440)] public short? Duration { get; set; }
    public List<WorkoutLineVm> Lines { get; set; } = [];
    public List<Exercise> Catalog { get; set; } = [];
}

public class WorkoutLineVm
{
    public int? ExerciseId { get; set; }
    [Range(1, 30)] public short Sets { get; set; } = 3;
    [Range(0, 1000)] public short? Reps { get; set; } = 10;
    [Range(0, 2000)] public decimal? Weight { get; set; }
    [Range(0, 32767)] public short? Seconds { get; set; }
}
