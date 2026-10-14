using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FitMind.Models.Identity;

namespace FitMind.Models;

[Table("Методы_восстановления")]
public class RecoveryMethod
{
    [Key, Column("ID_метода")] public int Id { get; set; }
    [Column("ID_владельца")] public string? OwnerId { get; set; }
    [Column("ID_источника")] public int? SourceId { get; set; }
    [Column("Название")] public string Name { get; set; } = "";
    [Column("Категория")] public string Category { get; set; } = "Восстановление";
    [Column("Краткое_описание")] public string? ShortDescription { get; set; }
    [Column("Полное_описание")] public string? FullDescription { get; set; }
    [Column("Польза")] public string? Benefits { get; set; }
    [Column("Рекомендации")] public string? Recommendations { get; set; }
    [Column("Предупреждения")] public string? Warnings { get; set; }
    [Column("Мин_длительность")] public short? MinMinutes { get; set; }
    [Column("Макс_длительность")] public short? MaxMinutes { get; set; }
    [Column("Изображение")] public string? Image { get; set; }
    [Column("Активно")] public bool Active { get; set; }
    public ApplicationUser? Owner { get; set; }
    public RecoveryMethod? Source { get; set; }
}

[Table("Типы_замеров")]
public class MeasurementType
{
    [Key, Column("ID_типа")] public int Id { get; set; }
    [Column("Название")] public string Name { get; set; } = "";
    [Column("Единица_измерения")] public string Unit { get; set; } = "";
    [Column("Описание")] public string? Description { get; set; }
    [Column("Основной")] public bool IsMain { get; set; }
}

[Table("Замеры_тела")]
public class BodyMeasurement
{
    [Key, Column("ID_замера")] public long Id { get; set; }
    [Column("ID_пользователя")] public string UserId { get; set; } = "";
    [Column("ID_типа")] public int TypeId { get; set; }
    [Column("Дата_замера")] public DateTime DateTime { get; set; }
    [Column("Значение")] public decimal Value { get; set; }
    [Column("Комментарий")] public string? Comment { get; set; }
    public MeasurementType? Type { get; set; }
}
