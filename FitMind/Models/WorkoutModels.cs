using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FitMind.Models;

[Table("Группы_мышц")]
public class MuscleGroup
{
    [Key, Column("ID_группы")] public int Id { get; set; }
    [Column("Название")] public string Name { get; set; } = "";
    [Column("Описание")] public string? Description { get; set; }
}

[Table("Упражнения")]
public class Exercise
{
    [Key, Column("ID_упражнения")] public int Id { get; set; }
    [Column("ID_категории")] public int CategoryId { get; set; }
    [Column("Название")] public string Name { get; set; } = "";
    [Column("Описание")] public string? Description { get; set; }
    [Column("Техника")] public string? Technique { get; set; }
    [Column("Частые_ошибки")] public string? CommonMistakes { get; set; }
    [Column("Рекомендации")] public string? Recommendations { get; set; }
    [Column("Противопоказания")] public string? Contraindications { get; set; }
    [Column("Необходимое_оборудование")] public string? Equipment { get; set; }
    [Column("Место")] public string? Place { get; set; }
    [Column("Уровень_сложности")] public string Difficulty { get; set; } = "";
    [Column("Расход_ккал_час")] public int? CaloriesPerHour { get; set; }
    [Column("Изображение")] public string? Image { get; set; }
    [Column("Видео")] public string? Video { get; set; }
    [Column("Активно")] public bool Active { get; set; }
    // null — упражнение базы FitMind; иначе владелец (личное упражнение пользователя).
    [Column("ID_владельца")] public string? OwnerId { get; set; }
    // Из какого упражнения базы скопировано личное упражнение.
    [Column("ID_исходного_упражнения")] public int? SourceExerciseId { get; set; }
    public Category? Category { get; set; }
    public Exercise? SourceExercise { get; set; }
}

[Table("Упражнения_Мышцы")]
public class ExerciseMuscle
{
    [Column("ID_упражнения")] public int ExerciseId { get; set; }
    [Column("ID_группы")] public int MuscleGroupId { get; set; }
    [Column("Основная_группа")] public bool IsPrimary { get; set; }
    public Exercise? Exercise { get; set; }
    public MuscleGroup? MuscleGroup { get; set; }
}
