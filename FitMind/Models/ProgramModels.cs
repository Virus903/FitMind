using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FitMind.Models.Identity;

namespace FitMind.Models;

// Шаг 5: общие таблицы программ (тренировки, питание, сон, добавки) и журнала дня.
// Класс программы назван FitnessProgram, а не Program: имя Program занято классом запуска
// приложения (Program.cs), и в контроллерах оно указывало бы на него.

public enum ProgramKind { Workout = 0, Nutrition = 1, Sleep = 2, Supplements = 3 }

public enum ProgramItemType { Workout = 0, Meal = 1, Water = 2, Bedtime = 3, WakeUp = 4, Procedure = 5, Supplement = 6, Custom = 7 }

public enum DayEntryStatus { Planned = 0, Done = 1, Skipped = 2 }

[Table("Программы")]
public class FitnessProgram
{
    [Key, Column("ID_программы")] public int Id { get; set; }
    [Column("Вид")] public ProgramKind Kind { get; set; }
    // null — программа базы FitMind; иначе владелец (пользователь).
    [Column("ID_владельца")] public string? OwnerId { get; set; }
    // Из какой программы базы скопирована.
    [Column("ID_исходной_программы")] public int? SourceProgramId { get; set; }
    [Column("Название")] public string Name { get; set; } = "";
    [Column("Описание")] public string? Description { get; set; }
    [Column("Для_кого")] public string? ForWhom { get; set; }
    [Column("Цель")] public string? GoalText { get; set; }
    [Column("ID_уровня")] public int? LevelId { get; set; }
    [Column("Место")] public string? Place { get; set; }
    [Column("Недель")] public byte? Weeks { get; set; }
    [Column("Тренировок_в_неделю")] public byte? WorkoutsPerWeek { get; set; }
    [Column("Средняя_длительность_мин")] public short? AverageDurationMinutes { get; set; }
    [Column("Ккал_в_день")] public int? CaloriesPerDay { get; set; }
    [Column("Вода_мл")] public int? WaterMl { get; set; }
    [Column("Изображение")] public string? Image { get; set; }
    [Column("Активна")] public bool Active { get; set; }
    [Column("Дата_начала")] public DateTime? StartDate { get; set; }
    [Column("Дата_окончания")] public DateTime? EndDate { get; set; }
    [Column("Создано")] public DateTime CreatedAt { get; set; }

    public ApplicationUser? Owner { get; set; }
    public FitnessProgram? SourceProgram { get; set; }
    public TrainingLevel? Level { get; set; }
    public List<ProgramItem> Items { get; set; } = [];
}

[Table("Пункты_программы")]
public class ProgramItem
{
    [Key, Column("ID_пункта")] public int Id { get; set; }
    [Column("ID_программы")] public int ProgramId { get; set; }
    [Column("Номер_дня")] public byte DayNumber { get; set; }
    // null — каждый день (для пунктов со временем).
    [Column("День_недели")] public DayOfWeek? DayOfWeek { get; set; }
    [Column("Время")] public TimeSpan? Time { get; set; }
    [Column("Тип")] public ProgramItemType Type { get; set; }
    [Column("Название")] public string Title { get; set; } = "";
    [Column("Описание")] public string? Description { get; set; }
    [Column("ID_продукта")] public int? ProductId { get; set; }
    [Column("ID_добавки")] public int? SupplementId { get; set; }
    [Column("ID_метода_восстановления")] public int? RecoveryMethodId { get; set; }
    [Column("Доза")] public string? Dose { get; set; }
    [Column("Ккал")] public int? Calories { get; set; }
    [Column("Вода_мл")] public int? WaterMl { get; set; }
    [Column("Минут")] public short? Minutes { get; set; }
    [Column("Порядок")] public short Order { get; set; }

    public FitnessProgram? Program { get; set; }
    public FoodProduct? Product { get; set; }
    public Supplement? Supplement { get; set; }
    public RecoveryMethod? RecoveryMethod { get; set; }
    public List<ProgramItemExercise> Exercises { get; set; } = [];
}

[Table("Упражнения_пункта")]
public class ProgramItemExercise
{
    [Key, Column("ID_записи")] public int Id { get; set; }
    [Column("ID_пункта")] public int ProgramItemId { get; set; }
    [Column("ID_упражнения")] public int ExerciseId { get; set; }
    [Column("Порядок")] public byte Order { get; set; }
    [Column("Подходы")] public byte Sets { get; set; }
    [Column("Повторения_мин")] public short? RepsMin { get; set; }
    [Column("Повторения_макс")] public short? RepsMax { get; set; }
    [Column("Секунд")] public short? Seconds { get; set; }
    [Column("Отдых_сек")] public short? RestSeconds { get; set; }
    [Column("Вес_кг")] public decimal? WeightKg { get; set; }
    [Column("Комментарий")] public string? Comment { get; set; }

    public ProgramItem? ProgramItem { get; set; }
    public Exercise? Exercise { get; set; }
}

[Table("Записи_дня")]
public class DayEntry
{
    [Key, Column("ID_записи")] public long Id { get; set; }
    [Column("ID_пользователя")] public string UserId { get; set; } = "";
    [Column("Начало")] public DateTime Start { get; set; }
    [Column("Окончание")] public DateTime? End { get; set; }
    [Column("Тип")] public ProgramItemType Type { get; set; }
    [Column("ID_пункта_программы")] public int? ProgramItemId { get; set; }
    [Column("ID_метода_восстановления")] public int? RecoveryMethodId { get; set; }
    [Column("ID_добавки")] public int? SupplementId { get; set; }
    [Column("Название")] public string Title { get; set; } = "";
    [Column("Статус")] public DayEntryStatus Status { get; set; }
    // Для тренировки — плановая длительность в минутах; для воды — мл и т.п.
    [Column("Значение")] public decimal? Value { get; set; }
    // Самочувствие / оценка 1–5.
    [Column("Оценка")] public byte? Rating { get; set; }
    [Column("Комментарий")] public string? Comment { get; set; }

    public ProgramItem? ProgramItem { get; set; }
    public RecoveryMethod? RecoveryMethod { get; set; }
    public Supplement? Supplement { get; set; }
    public List<EntrySet> Sets { get; set; } = [];
    public List<DayEntryFoodItem> FoodItems { get; set; } = [];
}

[Table("Подходы")]
public class EntrySet
{
    [Key, Column("ID_подхода")] public long Id { get; set; }
    [Column("ID_записи_дня")] public long DayEntryId { get; set; }
    [Column("ID_упражнения")] public int ExerciseId { get; set; }
    [Column("Номер")] public short Number { get; set; }
    [Column("Вес_кг")] public decimal? WeightKg { get; set; }
    [Column("Повторения")] public short? Reps { get; set; }
    [Column("Секунд")] public short? Seconds { get; set; }
    [Column("Выполнено")] public bool Done { get; set; }

    public DayEntry? DayEntry { get; set; }
    public Exercise? Exercise { get; set; }
}
