using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FitMind.Models;

[Table("Личные_заметки")]
public class PersonalNote
{
    [Key, Column("ID_заметки")] public long Id { get; set; }
    [Column("ID_пользователя")] public string UserId { get; set; } = "";
    [Column("Раздел")] public string Area { get; set; } = "Общее";
    [Column("Заголовок")] public string? Title { get; set; }
    [Column("Текст")] public string Text { get; set; } = "";
    [Column("Дата_создания")] public DateTime CreatedAt { get; set; }
}
