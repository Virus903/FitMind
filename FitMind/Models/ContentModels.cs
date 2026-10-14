using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FitMind.Models.Identity;

namespace FitMind.Models;

[Table("Категории")]
public class Category
{
    [Key, Column("ID_категории")] public int Id { get; set; }
    [Column("Раздел")] public string Section { get; set; } = "";
    [Column("Название")] public string Name { get; set; } = "";
    [Column("Описание")] public string? Description { get; set; }
}

[Table("Спортивные_добавки")]
public class Supplement
{
    [Key, Column("ID_добавки")] public int Id { get; set; }
    [Column("ID_владельца")] public string? OwnerId { get; set; }
    [Column("ID_источника")] public int? SourceId { get; set; }
    [Column("ID_категории")] public int CategoryId { get; set; }
    [Column("Название")] public string Name { get; set; } = "";
    [Column("Краткое_описание")] public string? ShortDescription { get; set; }
    [Column("Назначение")] public string? Purpose { get; set; }
    [Column("Как_принимают")] public string? Usage { get; set; }
    [Column("Стандартная_дозировка")] public string? StandardDosage { get; set; }
    [Column("Форма")] public string? Form { get; set; }
    [Column("Оптимальное_время")] public string? BestTime { get; set; }
    [Column("Плюсы")] public string? Benefits { get; set; }
    [Column("Минусы")] public string? Drawbacks { get; set; }
    [Column("Предупреждение")] public string? Warning { get; set; }
    [Column("FAQ")] public string? Faq { get; set; }
    [Column("Изображение")] public string? Image { get; set; }
    [Column("Активно")] public bool Active { get; set; }
    public Category? Category { get; set; }
    public ApplicationUser? Owner { get; set; }
    public Supplement? Source { get; set; }
}

[Table("Статьи")]
public class Article
{
    [Key, Column("ID_статьи")] public int Id { get; set; }
    [Column("ID_категории")] public int CategoryId { get; set; }
    [Column("Заголовок")] public string Title { get; set; } = "";
    [Column("Краткое_описание")] public string? ShortDescription { get; set; }
    [Column("Текст")] public string Text { get; set; } = "";
    [Column("Советы")] public string? Tips { get; set; }
    [Column("Вывод")] public string? Conclusion { get; set; }
    [Column("Минут_чтения")] public short? ReadingMinutes { get; set; }
    [Column("Изображение")] public string? Image { get; set; }
    [Column("Дата_публикации")] public DateTime PublishedAt { get; set; }
    [Column("Опубликована")] public bool Published { get; set; }
    public Category? Category { get; set; }
}

[Table("Уведомления")]
public class Notification
{
    [Key, Column("ID_уведомления")] public long Id { get; set; }
    [Column("ID_пользователя")] public string UserId { get; set; } = "";
    [Column("Тип")] public string Type { get; set; } = "Система";
    [Column("Заголовок")] public string Title { get; set; } = "";
    [Column("Текст")] public string Text { get; set; } = "";
    [Column("Ссылка")] public string? Link { get; set; }
    [Column("Прочитано")] public bool Read { get; set; }
    [Column("Дата_создания")] public DateTime CreatedAt { get; set; }
}

[Table("Настройки_системы")]
public class SystemSetting
{
    [Key, Column("Ключ")] public string Key { get; set; } = "";
    [Column("Значение")] public string? Value { get; set; }
    [Column("Описание")] public string? Description { get; set; }
    [Column("Дата_изменения")] public DateTime ChangedAt { get; set; }
}
