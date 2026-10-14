using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FitMind.Models.Identity;

namespace FitMind.Models;

[Table("Цели")]
public class FitnessGoal
{
    [Key, Column("ID_цели")] public int Id { get; set; }
    [Column("Название")] public string Name { get; set; } = "";
    [Column("Описание")] public string? Description { get; set; }
}

[Table("Уровни_подготовки")]
public class TrainingLevel
{
    [Key, Column("ID_уровня")] public int Id { get; set; }
    [Column("Название")] public string Name { get; set; } = "";
    [Column("Описание")] public string? Description { get; set; }
}

[Table("Уровни_активности")]
public class ActivityLevel
{
    [Key, Column("ID_активности")] public int Id { get; set; }
    [Column("Название")] public string Name { get; set; } = "";
    [Column("Коэффициент")] public decimal Coefficient { get; set; }
    [Column("Описание")] public string? Description { get; set; }
}

[Table("Форматы_тренировок")]
public class TrainingFormat
{
    [Key, Column("ID_формата")] public int Id { get; set; }
    [Column("Название")] public string Name { get; set; } = "";
    [Column("Описание")] public string? Description { get; set; }
    [Column("Иконка")] public string? Icon { get; set; }
}

// Профиль пользователя: анкета, места тренировок, основная цель и настройки интерфейса в одной таблице.
[Table("Профили")]
public class UserProfile
{
    [Key, Column("ID_профиля")] public int Id { get; set; }
    [Column("ID_пользователя")] public string UserId { get; set; } = "";
    [Column("Имя")] public string Name { get; set; } = "";

    // Старое поле оставлено для совместимости с уже созданной БД.
    [Column("Дата_рождения")] public DateTime? BirthDate { get; set; }

    [Column("Возраст")] public byte? Age { get; set; }
    [Column("Пол")] public string? Gender { get; set; }
    [Column("Рост_см")] public short? HeightCm { get; set; }
    [Column("Вес_кг")] public decimal? WeightKg { get; set; }
    [Column("О_себе")] public string? AboutMe { get; set; }

    [Column("ID_уровня_подготовки")] public int? TrainingLevelId { get; set; }
    [Column("Дома")] public bool AtHome { get; set; }
    [Column("На_улице")] public bool Outdoor { get; set; }
    [Column("В_зале")] public bool AtGym { get; set; }
    [Column("ID_цели")] public int? GoalId { get; set; }
    [Column("ID_активности")] public int? ActivityLevelId { get; set; }
    [Column("Тренировок_в_неделю")] public byte? WorkoutsPerWeek { get; set; }
    [Column("Цель_сна_часов")] public decimal? SleepGoalHours { get; set; }

    [Column("Шаг_анкеты")] public byte OnboardingStep { get; set; } = 1;
    [Column("Анкета_завершена")] public bool OnboardingCompleted { get; set; }
    [Column("Дата_создания")] public DateTime CreatedAt { get; set; }

    // Настройки (раньше таблица Настройки_пользователя).
    [Column("Тема")] public string Theme { get; set; } = "Тёмная";
    [Column("Единицы")] public string Units { get; set; } = "Метрические";
    [Column("Уведомления")] public bool NotificationsEnabled { get; set; } = true;
    [Column("Email_уведомления")] public bool EmailNotifications { get; set; } = true;
    [Column("Тихие_часы")] public bool QuietHoursEnabled { get; set; }
    [Column("Тихие_часы_начало")] public TimeSpan? QuietStart { get; set; }
    [Column("Тихие_часы_конец")] public TimeSpan? QuietEnd { get; set; }
    [Column("Цель_воды_мл")] public int WaterGoalMl { get; set; } = 2000;

    public ApplicationUser? User { get; set; }
    public TrainingLevel? TrainingLevel { get; set; }
    public FitnessGoal? Goal { get; set; }

    [NotMapped] public bool HasPlace => AtHome || Outdoor || AtGym;
    [NotMapped] public string PlacesText => HasPlace ? string.Join(", ", new[] { AtHome ? "Дома" : null, Outdoor ? "На улице" : null, AtGym ? "В зале" : null }.Where(x => x != null)) : "Не указано";
    public ActivityLevel? ActivityLevel { get; set; }
}

[Table("Пользовательские_форматы_тренировок")]
public class UserTrainingFormat
{
    [Column("ID_пользователя")] public string UserId { get; set; } = "";
    [Column("ID_формата")] public int FormatId { get; set; }
    [Column("Дата_добавления")] public DateTime CreatedAt { get; set; }
    public TrainingFormat? Format { get; set; }
}
