// Українські назви ролей і статусів для повідомлень користувачу (технічні коди на екран не виводимо).
namespace MedLink.LIS.Api.Domain;

public static class UkLabels
{
    public static string Role(string? code) => code switch
    {
        LabRoles.Admin => "Адміністратор лабораторії",
        LabRoles.Doctor => "Лікар-лаборант",
        LabRoles.Technician => "Лаборант",
        LabRoles.Phlebotomist => "Медсестра пункту забору",
        LabRoles.Registrar => "Реєстратор",
        LabRoles.Courier => "Кур’єр",
        LabRoles.System => "Система",
        null or "" => "невідома роль",
        _ => code
    };

    public static string Roles(IEnumerable<string> codes) => string.Join(", ", codes.Select(Role));

    /// <summary>Назва статусу сутності (замовлення, тест, пробірка, посів, коннектор тощо).</summary>
    public static string Status(string entity, string status) => entity switch
    {
        LisEntities.Order => status switch
        {
            "NEW" => "Нове", "COLLECTED" => "Забрано", "IN_TRANSIT" => "У дорозі", "RECEIVED" => "Прийнято", "IN_PROGRESS" => "У роботі",
            "PARTIALLY_COMPLETED" => "Частково виконано", "COMPLETED" => "Виконано", "RELEASED" => "Видано", "CANCELLED" => "Скасовано", "REJECTED" => "Відхилено",
            _ => status
        },
        LisEntities.OrderTest => status switch
        {
            "PENDING" => "Очікує", "IN_ANALYSIS" => "В аналізі", "RESULTED" => "Є результат", "NEEDS_REVIEW" => "Очікує лікаря",
            "AUTO_VERIFIED" => "Автоверифіковано", "VERIFIED" => "Верифіковано", "REJECTED" => "Відхилено", "RERUN" => "Повтор", "SENT_OUT" => "У зовнішній лабораторії",
            _ => status
        },
        LisEntities.Sample => status switch
        {
            "PENDING" => "Очікує забору", "COLLECTED" => "Забрано", "IN_TRANSIT" => "У дорозі", "RECEIVED" => "Прийнято", "PROCESSING" => "В обробці",
            "STORED" => "В архіві", "DISPOSED" => "Утилізовано", "REJECTED" => "Відбраковано",
            _ => status
        },
        LisEntities.Culture => status switch
        {
            "INCUBATING" => "Інкубація", "PRELIMINARY" => "Попередній результат", "ISOLATED" => "Ідентифіковано", "COMPLETED" => "Завершено",
            _ => status
        },
        LisEntities.Connector => status switch
        {
            "PENDING" => "Очікує реєстрації", "ACTIVE" => "Активний", "OFFLINE" => "Не в мережі", "DISABLED" => "Вимкнений",
            _ => status
        },
        LisEntities.Manifest => status switch
        {
            "CREATED" => "Чернетка", "DISPATCHED" => "Відправлено", "RECEIVED" => "Прийнято", "REJECTED" => "Відхилено",
            _ => status
        },
        LisEntities.Container => status switch
        {
            "FREE" => "Вільний", "PACKING" => "Укладання", "SEALED" => "Запечатано", "IN_TRANSIT" => "У дорозі",
            "RECEIVED" => "Прийнято", "RETURNED" => "Повернуто", "LOST" => "Втрачено",
            _ => status
        },
        _ => status
    };

    public static string Statuses(string entity, IEnumerable<string> statuses) =>
        string.Join(", ", statuses.Select(s => $"«{Status(entity, s)}»"));
}
