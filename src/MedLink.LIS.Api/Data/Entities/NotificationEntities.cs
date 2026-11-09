// Налаштування каналів сповіщень (вмикання/вимикання адміністратором). Параметри підключення й секрети — лише в конфігурації сервера.
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MedLink.LIS.Api.Data.Entities;

/// <summary>[ЛІС] Стан каналу сповіщень: EMAIL | SMS | VIBER | TELEGRAM. Немає запису — канал увімкнено (якщо налаштований у конфігурації).</summary>
[Table("lab_notify_channel")]
public class LabNotifyChannel : GuidEntity
{
    [MaxLength(16)] public string Channel { get; set; } = "";
    public bool IsEnabled { get; set; } = true;
}
