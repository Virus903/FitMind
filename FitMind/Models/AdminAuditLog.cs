using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace FitMind.Models;

/// <summary>
/// Журнал значимых операций будущей административной панели.
/// Цель аудита намеренно хранится текстом: удалённая справочная запись не должна
/// уничтожать историю операции. Пароли, токены и другие секреты сюда не записываются.
/// </summary>
[Table("Журнал_действий_администратора")]
public class AdminAuditLog
{
    [Key, Column("ID_события")] public long Id { get; set; }
    [Column("ID_администратора")] public string? ActorUserId { get; set; }
    [Column("Дата_и_время")] public DateTime OccurredAt { get; set; }
    [Column("Действие")] public string Action { get; set; } = "";
    [Column("Тип_сущности")] public string EntityType { get; set; } = "";
    [Column("ID_сущности")] public string? EntityId { get; set; }
    [Column("Описание")] public string? Summary { get; set; }
    [Column("IP_адрес")] public string? IpAddress { get; set; }
}
