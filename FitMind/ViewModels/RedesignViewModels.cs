using System.ComponentModel.DataAnnotations;
using FitMind.Models;

namespace FitMind.ViewModels;

public class MyDayPageVm
{
    public string UserName { get; set; } = "Пользователь";
    public DateTime Date { get; set; } = DateTime.Today;
    public DayEntry? NextEvent { get; set; }
    public List<DayEntry> Items { get; set; } = [];
}

public class MyWorkoutsRedesignVm
{
    public List<DayEntry> CalendarWorkouts { get; set; } = [];
    public DateTime Month { get; set; }
    public DateTime WeekStart { get; set; }
    public DayEntry? TodayWorkout { get; set; }
    public FitnessProgram? ActiveProgram { get; set; }
    public List<FitnessProgram> Programs { get; set; } = [];
    public List<Exercise> Exercises { get; set; } = [];
    public List<DayEntry> History { get; set; } = [];
    public List<PersonalNote> Notes { get; set; } = [];
}

public class UserProgramEditVm
{
    public int? Id { get; set; }
    [Required, StringLength(160)] public string Name { get; set; } = "";
    [StringLength(10000)] public string? Description { get; set; }
    public string? GoalName { get; set; }
    [Range(1, 52)] public byte Weeks { get; set; } = 8;
    [Range(1, 7)] public byte WorkoutsPerWeek { get; set; } = 3;
    public string? Image { get; set; }
    // Загруженная обложка (PNG, JPEG, WebP до 2 МБ); иначе выбор из /images/covers.
    public Microsoft.AspNetCore.Http.IFormFile? CoverFile { get; set; }
    [DataType(DataType.Date)] public DateTime StartDate { get; set; } = DateTime.Today;
    public List<ProgramDayEditVm> Days { get; set; } = [];
}

public class ProgramDayEditVm
{
    public int? Id { get; set; }
    [Required, StringLength(120)] public string Name { get; set; } = "";
    [Range(0, 6)] public int DayOfWeek { get; set; }
    public TimeSpan Time { get; set; } = new(18, 0, 0);
    public List<ProgramExerciseEditVm> Exercises { get; set; } = [];
}

public class ProgramExerciseEditVm
{
    [Range(1, int.MaxValue)] public int ExerciseId { get; set; }
    [Range(1, 30)] public byte Sets { get; set; } = 3;
    [Range(0, 1000)] public short? Reps { get; set; } = 10;
    [Range(0, 32767)] public short? DurationSeconds { get; set; }
    [Range(0, 3600)] public short? RestSeconds { get; set; } = 60;
    [Range(0, 2000)] public decimal? WorkingWeightKg { get; set; }
    [StringLength(1000)] public string? Comment { get; set; }
}

public class UserExerciseEditVm
{
    public int? Id { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile? ImageFile { get; set; }
    [Required, StringLength(160)] public string Name { get; set; } = "";
    [StringLength(10000)] public string? Description { get; set; }
    public string? MuscleGroup { get; set; }
    public string? Equipment { get; set; }
    public string? Difficulty { get; set; }
    public string? Image { get; set; }
    [StringLength(2000)] public string? Technique { get; set; }
}

public class WorkoutRecordVm
{
    public long? WorkoutId { get; set; }
    [Required] public string Name { get; set; } = "";
    public DateTime Date { get; set; } = DateTime.Today;
    public TimeSpan Time { get; set; } = DateTime.Now.TimeOfDay;
    [Range(1, 600)] public int DurationMinutes { get; set; } = 60;
    [Range(0, 10000)] public int? Calories { get; set; }
    [Range(1, 5)] public byte? Wellbeing { get; set; }
    public string? Note { get; set; }
}

public class FoodDiaryRedesignVm
{
    public List<DateTime> MarkedDates { get; set; } = [];
    public DateTime Date { get; set; }
    public DateTime Month { get; set; }
    public DateTime WeekStart { get; set; }
    public FitnessProgram? ActivePlan { get; set; }
    public List<FitnessProgram> Plans { get; set; } = [];
    public List<FoodProduct> MyFood { get; set; } = [];
    public List<DayEntry> Entries { get; set; } = [];
    public List<PersonalNote> Notes { get; set; } = [];
    [Range(0, 10000)] public decimal Calories { get; set; }
    [Range(0, 10000)] public decimal Protein { get; set; }
    [Range(0, 10000)] public decimal Fat { get; set; }
    [Range(0, 10000)] public decimal Carbs { get; set; }
    public int WaterMl { get; set; }
    [Range(250, 10000)] public int WaterGoalMl { get; set; } = 2000;
}

public class NutritionPlanEditVm
{
    public int? Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public string? Description { get; set; }
    public string? Goal { get; set; }
    [Range(800, 6000)] public int? CaloriesTarget { get; set; }
    [Range(3, 6)] public byte MealsPerDay { get; set; } = 4;
    [Range(1, 52)] public byte Weeks { get; set; } = 4;
    [Range(250, 10000)] public int WaterMl { get; set; } = 2000;
    [DataType(DataType.Date)] public DateTime StartDate { get; set; } = DateTime.Today;
    public List<NutritionPlanItemEditVm> Items { get; set; } = [];
    public string? Image { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile? ImageFile { get; set; }
}

public class NutritionPlanItemEditVm
{
    [StringLength(120)] public string? Name { get; set; }
    public TimeSpan? Time { get; set; }
    [Range(0, 6000)] public int? Calories { get; set; }
    [Range(0, 10000)] public int? WaterMl { get; set; }
    public int? ProductId { get; set; }
    [Range(1, 5000)] public decimal? Grams { get; set; }
}

public class UserFoodItemEditVm
{
    [Required] public string Name { get; set; } = "";
    public FoodProductKind Kind { get; set; } = FoodProductKind.Recipe;
    public int CategoryId { get; set; }
    public string? Image { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile? ImageFile { get; set; }
    [Range(0, 10000)] public decimal Calories { get; set; }
    [Range(0, 10000)] public decimal Protein { get; set; }
    [Range(0, 10000)] public decimal Fat { get; set; }
    [Range(0, 10000)] public decimal Carbs { get; set; }
    public string? Description { get; set; }
    public string? Instructions { get; set; }
    [Range(1, 1440)] public short? PreparationMinutes { get; set; }
}

public class RecoveryRedesignVm
{
    public DateTime Date { get; set; }
    public DateTime Month { get; set; }
    public DateTime WeekStart { get; set; }
    public List<DateTime> MarkedDates { get; set; } = [];
    public FitnessProgram? ActivePlan { get; set; }
    public List<FitnessProgram> Plans { get; set; } = [];
    public List<RecoveryMethod> MyItems { get; set; } = [];
    public List<DayEntry> SleepRecords { get; set; } = [];
    public List<DayEntry> RecoveryRecords { get; set; } = [];
    public List<PersonalNote> Notes { get; set; } = [];
}

public class RecoveryPlanEditVm
{
    public int? Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public string? Description { get; set; }
    [Range(1, 104)] public byte Weeks { get; set; } = 4;
    public DateTime StartDate { get; set; } = DateTime.Today;
    public List<RecoveryPlanItemEditVm> Items { get; set; } = [];
    public string? Image { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile? ImageFile { get; set; }
}

public class RecoveryPlanItemEditVm
{
    public ProgramItemType Type { get; set; } = ProgramItemType.Procedure;
    [StringLength(120)] public string? Name { get; set; }
    public TimeSpan? Time { get; set; }
    [Range(1, 1440)] public short? DurationMinutes { get; set; }
    public int? RecoveryMethodId { get; set; }
    public DayOfWeek? DayOfWeek { get; set; }
}

public class UserRecoveryItemEditVm
{
    public int? Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public string Category { get; set; } = "Восстановление";
    public string? Description { get; set; }
    [Range(1, 1440)] public short? DurationMinutes { get; set; }
    public string? Image { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile? ImageFile { get; set; }
}

public class SupplementsRedesignVm
{
    public DateTime Date { get; set; }
    public DateTime Month { get; set; }
    public DateTime WeekStart { get; set; }
    public FitnessProgram? ActivePlan { get; set; }
    public List<FitnessProgram> Plans { get; set; } = [];
    public List<Supplement> Items { get; set; } = [];
    public List<DayEntry> TodayEntries { get; set; } = [];
    public List<DayEntry> RecentIntakes { get; set; } = [];
    public List<PersonalNote> Notes { get; set; } = [];
}

public class SupplementPlanEditVm
{
    public int? Id { get; set; }
    [Required] public string Name { get; set; } = "";
    public string? Description { get; set; }
    [Range(1, 104)] public byte Weeks { get; set; } = 8;
    public DateTime StartDate { get; set; } = DateTime.Today;
    public List<SupplementPlanItemEditVm> Items { get; set; } = [];
    public string? Image { get; set; }
    public Microsoft.AspNetCore.Http.IFormFile? ImageFile { get; set; }
}

public class SupplementPlanItemEditVm
{
    public int? SupplementId { get; set; }
    [StringLength(120)] public string? Dose { get; set; }
    public TimeSpan? Time { get; set; }
    public List<DayOfWeek> Days { get; set; } = [];
}

public class NoteEditVm
{
    [Required] public string Area { get; set; } = "Общее";
    public string? Title { get; set; }
    [Required, StringLength(2000)] public string Text { get; set; } = "";
}

public class MeasurementsPageVm
{
    public DateTime Date { get; set; } = DateTime.Today;
    public List<MeasurementType> Types { get; set; } = [];
    public List<BodyMeasurement> History { get; set; } = [];
}
