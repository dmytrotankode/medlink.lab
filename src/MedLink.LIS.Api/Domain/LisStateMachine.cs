// =============================================================================
// MedLink LIS 4.0 — ЄДИНЕ МІСЦЕ ВИЗНАЧЕННЯ МАШИНИ СТАНІВ ТА РОЛЬОВИХ ПРАВИЛ.
// Сутності: Order, Sample, OrderTest, Manifest (логістика), Culture (посів), Connector.
// Кожна дія (ActionRule) описує: з яких статусів дозволена, у який статус переводить,
// які ролі (org_employee.lab_role) можуть її виконати. LAB_ADMIN може все.
// Невалідний перехід → 409 (ConflictException), заборонена роль → 403 (ForbiddenException).
// =============================================================================
using MedLink.LIS.Api.Data.Entities;
using MedLink.LIS.Api.Infrastructure;

namespace MedLink.LIS.Api.Domain;

public static class LabRoles
{
    public const string Admin = "LAB_ADMIN";
    public const string Doctor = "LAB_DOCTOR";
    public const string Technician = "LAB_TECHNICIAN";
    public const string Phlebotomist = "PHLEBOTOMIST";
    public const string Courier = "LOGISTICS_COURIER";
    public const string Registrar = "REGISTRAR";
    public static readonly string[] All = { Admin, Doctor, Technician, Phlebotomist, Courier, Registrar };
    /// <summary>Технічний актор (коннектор/система) — не підлягає рольовим обмеженням.</summary>
    public const string System = "SYSTEM";
}

public sealed record ActionRule(
    string Action,
    string[] FromStatuses,
    string? ToStatus,
    string[] Roles,
    string Label,
    /// <summary>Дія не змінює статус (редагування, друк).</summary>
    bool IsNonTransition = false);

public static class LisEntities
{
    public const string Order = "Order", Sample = "Sample", OrderTest = "OrderTest", Manifest = "Manifest",
        Culture = "Culture", Connector = "Connector";
}

public static class OrderActions
{
    public const string Edit = "EDIT", AddTest = "ADD_TEST", RemoveTest = "REMOVE_TEST", AddSample = "ADD_SAMPLE",
        Collect = "COLLECT", Dispatch = "DISPATCH", Receive = "RECEIVE", EnterResult = "ENTER_RESULT", Verify = "VERIFY",
        Complete = "COMPLETE", Release = "RELEASE", Reopen = "REOPEN", Cancel = "CANCEL", Reject = "REJECT", Delete = "DELETE",
        PrintLabels = "PRINT_LABELS", PrintReport = "PRINT_REPORT";
}

public static class SampleActions
{
    public const string Collect = "COLLECT", Dispatch = "DISPATCH", Receive = "RECEIVE", Process = "PROCESS", Store = "STORE",
        Dispose = "DISPOSE", Reject = "REJECT", EditFlags = "EDIT_FLAGS", Replace = "REPLACE", Delete = "DELETE", Uncollect = "UNCOLLECT";
}

public static class TestActions
{
    public const string EnterResult = "ENTER_RESULT", EditResult = "EDIT_RESULT", DeleteResult = "DELETE_RESULT", Verify = "VERIFY",
        Reject = "REJECT", Rerun = "RERUN", Reopen = "REOPEN", AssignAnalyzer = "ASSIGN_ANALYZER", Remove = "REMOVE", StartAnalysis = "START_ANALYSIS",
        SendOut = "SEND_OUT", RecallSendOut = "RECALL_SEND_OUT";
}

public static class ManifestActions
{
    public const string Edit = "EDIT", Dispatch = "DISPATCH", Receive = "RECEIVE", Reject = "REJECT", Delete = "DELETE";
}

public static class CultureActions
{
    public const string Edit = "EDIT", Preliminary = "PRELIMINARY", Isolate = "ISOLATE", Complete = "COMPLETE", Delete = "DELETE",
        AddIsolate = "ADD_ISOLATE", AddSusceptibility = "ADD_SUSCEPTIBILITY";
}

public static class ConnectorActions
{
    public const string Register = "REGISTER", Heartbeat = "HEARTBEAT", MarkOffline = "MARK_OFFLINE", Disable = "DISABLE",
        Enable = "ENABLE", RotateKey = "ROTATE_KEY", Edit = "EDIT", Delete = "DELETE";
}

public static class LisStateMachine
{
    private static readonly string[] AllRoles = LabRoles.All;
    private static readonly string[] AdminDoctor = { LabRoles.Admin, LabRoles.Doctor };
    private static readonly string[] AdminDoctorTech = { LabRoles.Admin, LabRoles.Doctor, LabRoles.Technician };
    private static readonly string[] AdminRegistrarDoctor = { LabRoles.Admin, LabRoles.Registrar, LabRoles.Doctor };
    private static readonly string[] Collectors = { LabRoles.Admin, LabRoles.Phlebotomist, LabRoles.Technician };
    private static readonly string[] Couriers = { LabRoles.Admin, LabRoles.Courier, LabRoles.Phlebotomist };
    private static readonly string[] Receivers = { LabRoles.Admin, LabRoles.Technician, LabRoles.Doctor, LabRoles.Courier };

    // ------------------------------------------------------------------ ORDER
    public static readonly ActionRule[] OrderRules =
    {
        new(OrderActions.Edit, new[]{ OrderStatuses.New }, null, AdminRegistrarDoctor, "Редагувати замовлення", true),
        new(OrderActions.AddTest, new[]{ OrderStatuses.New, OrderStatuses.Collected, OrderStatuses.Received, OrderStatuses.InProgress, OrderStatuses.PartiallyCompleted }, null, AdminRegistrarDoctor, "Додати тест", true),
        new(OrderActions.RemoveTest, new[]{ OrderStatuses.New }, null, AdminRegistrarDoctor, "Прибрати тест", true),
        new(OrderActions.AddSample, new[]{ OrderStatuses.New, OrderStatuses.Collected }, null, new[]{ LabRoles.Admin, LabRoles.Registrar, LabRoles.Phlebotomist, LabRoles.Technician }, "Додати пробірку", true),
        new(OrderActions.Collect, new[]{ OrderStatuses.New }, OrderStatuses.Collected, Collectors, "Забір біоматеріалу"),
        new(OrderActions.Dispatch, new[]{ OrderStatuses.Collected }, OrderStatuses.InTransit, Couriers, "Відправити в лабораторію"),
        new(OrderActions.Receive, new[]{ OrderStatuses.Collected, OrderStatuses.InTransit }, OrderStatuses.Received, Receivers, "Прийняти в лабораторії"),
        new(OrderActions.EnterResult, new[]{ OrderStatuses.Received, OrderStatuses.InProgress, OrderStatuses.PartiallyCompleted }, OrderStatuses.InProgress, AdminDoctorTech, "Ввести результат"),
        new(OrderActions.Verify, new[]{ OrderStatuses.InProgress, OrderStatuses.PartiallyCompleted, OrderStatuses.Completed }, null, AdminDoctor, "Верифікувати результати", true),
        new(OrderActions.Complete, new[]{ OrderStatuses.InProgress, OrderStatuses.PartiallyCompleted }, OrderStatuses.Completed, AdminDoctorTech, "Завершити (усі тести верифіковано)"),
        new(OrderActions.Release, new[]{ OrderStatuses.Completed }, OrderStatuses.Released, AdminDoctor, "Видати результати пацієнту"),
        new(OrderActions.Reopen, new[]{ OrderStatuses.Completed, OrderStatuses.Released }, OrderStatuses.InProgress, AdminDoctor, "Повернути в роботу"),
        new(OrderActions.Cancel, new[]{ OrderStatuses.New, OrderStatuses.Collected, OrderStatuses.InTransit, OrderStatuses.Received }, OrderStatuses.Cancelled, AdminRegistrarDoctor, "Скасувати замовлення"),
        new(OrderActions.Reject, new[]{ OrderStatuses.Collected, OrderStatuses.InTransit, OrderStatuses.Received, OrderStatuses.InProgress, OrderStatuses.PartiallyCompleted }, OrderStatuses.Rejected, AdminDoctorTech, "Відхилити (брак біоматеріалу)"),
        new(OrderActions.Delete, new[]{ OrderStatuses.New, OrderStatuses.Cancelled }, null, new[]{ LabRoles.Admin, LabRoles.Registrar }, "Видалити замовлення", true),
        new(OrderActions.PrintLabels, new[]{ OrderStatuses.New, OrderStatuses.Collected, OrderStatuses.InTransit, OrderStatuses.Received, OrderStatuses.InProgress, OrderStatuses.PartiallyCompleted, OrderStatuses.Completed, OrderStatuses.Released }, null, AllRoles, "Друк етикеток", true),
        new(OrderActions.PrintReport, new[]{ OrderStatuses.InProgress, OrderStatuses.PartiallyCompleted, OrderStatuses.Completed, OrderStatuses.Released }, null, AllRoles, "Друк бланка", true),
    };

    // ------------------------------------------------------------------ SAMPLE
    public static readonly ActionRule[] SampleRules =
    {
        new(SampleActions.Collect, new[]{ SampleStatuses.Pending }, SampleStatuses.Collected, Collectors, "Забір пробірки"),
        new(SampleActions.Uncollect, new[]{ SampleStatuses.Collected }, SampleStatuses.Pending, new[]{ LabRoles.Admin, LabRoles.Phlebotomist }, "Скасувати відмітку забору"),
        new(SampleActions.Dispatch, new[]{ SampleStatuses.Collected }, SampleStatuses.InTransit, Couriers, "Відправити"),
        new(SampleActions.Receive, new[]{ SampleStatuses.Collected, SampleStatuses.InTransit }, SampleStatuses.Received, Receivers, "Прийняти"),
        new(SampleActions.Process, new[]{ SampleStatuses.Received }, SampleStatuses.Processing, AdminDoctorTech, "В обробці (центрифугування/аналіз)"),
        new(SampleActions.Store, new[]{ SampleStatuses.Received, SampleStatuses.Processing }, SampleStatuses.Stored, AdminDoctorTech, "Розмістити в біобанку"),
        new(SampleActions.Dispose, new[]{ SampleStatuses.Stored, SampleStatuses.Received, SampleStatuses.Processing, SampleStatuses.Rejected }, SampleStatuses.Disposed, AdminDoctorTech, "Утилізувати"),
        new(SampleActions.Reject, new[]{ SampleStatuses.Collected, SampleStatuses.InTransit, SampleStatuses.Received, SampleStatuses.Processing }, SampleStatuses.Rejected, AdminDoctorTech, "Відбракувати пробірку"),
        new(SampleActions.EditFlags, new[]{ SampleStatuses.Collected, SampleStatuses.InTransit, SampleStatuses.Received, SampleStatuses.Processing }, null, Receivers, "Відмітки гемоліз/ліпемія/іктеричність", true),
        new(SampleActions.Replace, new[]{ SampleStatuses.Pending, SampleStatuses.Collected, SampleStatuses.Rejected }, null, new[]{ LabRoles.Admin, LabRoles.Phlebotomist, LabRoles.Technician }, "Замінити пробірку (новий штрихкод)", true),
        new(SampleActions.Delete, new[]{ SampleStatuses.Pending }, null, new[]{ LabRoles.Admin, LabRoles.Registrar, LabRoles.Phlebotomist }, "Видалити пробірку", true),
    };

    // ------------------------------------------------------------------ ORDER TEST / RESULT
    public static readonly ActionRule[] TestRules =
    {
        new(TestActions.StartAnalysis, new[]{ OrderTestStatuses.Pending, OrderTestStatuses.Rerun }, OrderTestStatuses.InAnalysis, AdminDoctorTech, "Передати на аналізатор"),
        new(TestActions.AssignAnalyzer, new[]{ OrderTestStatuses.Pending, OrderTestStatuses.InAnalysis, OrderTestStatuses.Rerun }, null, AdminDoctorTech, "Призначити аналізатор", true),
        new(TestActions.EnterResult, new[]{ OrderTestStatuses.Pending, OrderTestStatuses.InAnalysis, OrderTestStatuses.Rerun, OrderTestStatuses.SentOut }, OrderTestStatuses.Resulted, AdminDoctorTech, "Ввести результат"),
        new(TestActions.SendOut, new[]{ OrderTestStatuses.Pending, OrderTestStatuses.Rerun }, OrderTestStatuses.SentOut, AdminDoctorTech, "Відправити в зовнішню лабораторію"),
        new(TestActions.RecallSendOut, new[]{ OrderTestStatuses.SentOut }, OrderTestStatuses.Pending, AdminDoctorTech, "Відкликати з зовнішньої лабораторії"),
        new(TestActions.EditResult, new[]{ OrderTestStatuses.Resulted, OrderTestStatuses.NeedsReview }, OrderTestStatuses.Resulted, AdminDoctorTech, "Виправити результат (нова версія)"),
        new(TestActions.DeleteResult, new[]{ OrderTestStatuses.Resulted, OrderTestStatuses.NeedsReview }, OrderTestStatuses.Pending, AdminDoctor, "Видалити результат"),
        new(TestActions.Verify, new[]{ OrderTestStatuses.Resulted, OrderTestStatuses.NeedsReview, OrderTestStatuses.AutoVerified }, OrderTestStatuses.Verified, AdminDoctor, "Верифікувати"),
        new(TestActions.Reject, new[]{ OrderTestStatuses.Pending, OrderTestStatuses.InAnalysis, OrderTestStatuses.Resulted, OrderTestStatuses.NeedsReview, OrderTestStatuses.Rerun, OrderTestStatuses.SentOut }, OrderTestStatuses.Rejected, AdminDoctorTech, "Відхилити тест"),
        new(TestActions.Rerun, new[]{ OrderTestStatuses.Resulted, OrderTestStatuses.NeedsReview, OrderTestStatuses.AutoVerified }, OrderTestStatuses.Rerun, AdminDoctorTech, "Повторити дослідження"),
        new(TestActions.Reopen, new[]{ OrderTestStatuses.Verified, OrderTestStatuses.AutoVerified, OrderTestStatuses.Rejected }, OrderTestStatuses.NeedsReview, AdminDoctor, "Відкрити повторно"),
        new(TestActions.Remove, new[]{ OrderTestStatuses.Pending }, null, AdminRegistrarDoctor, "Прибрати тест із замовлення", true),
    };

    // ------------------------------------------------------------------ MANIFEST (логістика)
    public const string ManifestCreated = "CREATED", ManifestDispatched = "DISPATCHED", ManifestReceived = "RECEIVED", ManifestRejected = "REJECTED";
    public static readonly ActionRule[] ManifestRules =
    {
        new(ManifestActions.Edit, new[]{ ManifestCreated, ManifestDispatched }, null, Couriers, "Редагувати маніфест", true),
        new(ManifestActions.Dispatch, new[]{ ManifestCreated }, ManifestDispatched, Couriers, "Відправити"),
        new(ManifestActions.Receive, new[]{ ManifestDispatched }, ManifestReceived, Receivers, "Прийняти в лабораторії"),
        new(ManifestActions.Reject, new[]{ ManifestDispatched }, ManifestRejected, Receivers, "Відхилити (порушення холодового ланцюга)"),
        new(ManifestActions.Delete, new[]{ ManifestCreated, ManifestDispatched }, null, new[]{ LabRoles.Admin, LabRoles.Courier }, "Видалити маніфест", true),
    };

    // ------------------------------------------------------------------ CULTURE (посів)
    public const string CultureIncubating = "INCUBATING", CulturePreliminary = "PRELIMINARY", CultureIsolated = "ISOLATED", CultureCompleted = "COMPLETED";
    public static readonly ActionRule[] CultureRules =
    {
        new(CultureActions.Edit, new[]{ CultureIncubating, CulturePreliminary, CultureIsolated }, null, AdminDoctorTech, "Редагувати посів", true),
        new(CultureActions.Preliminary, new[]{ CultureIncubating }, CulturePreliminary, AdminDoctorTech, "Попередній результат"),
        new(CultureActions.AddIsolate, new[]{ CultureIncubating, CulturePreliminary, CultureIsolated }, null, AdminDoctorTech, "Додати ізолят", true),
        new(CultureActions.Isolate, new[]{ CultureIncubating, CulturePreliminary }, CultureIsolated, AdminDoctorTech, "Ізоляти виділено"),
        new(CultureActions.AddSusceptibility, new[]{ CulturePreliminary, CultureIsolated }, null, AdminDoctorTech, "Антибіотикограма", true),
        new(CultureActions.Complete, new[]{ CulturePreliminary, CultureIsolated }, CultureCompleted, AdminDoctor, "Завершити посів"),
        new(CultureActions.Delete, new[]{ CultureIncubating }, null, AdminDoctor, "Видалити посів", true),
    };

    // ------------------------------------------------------------------ CONNECTOR
    public const string ConnectorPending = "PENDING", ConnectorActive = "ACTIVE", ConnectorOffline = "OFFLINE", ConnectorDisabled = "DISABLED";
    public static readonly ActionRule[] ConnectorRules =
    {
        new(ConnectorActions.Register, new[]{ ConnectorPending }, ConnectorActive, new[]{ LabRoles.System }, "Реєстрація інсталяції"),
        new(ConnectorActions.Heartbeat, new[]{ ConnectorActive, ConnectorOffline }, ConnectorActive, new[]{ LabRoles.System }, "Heartbeat"),
        new(ConnectorActions.MarkOffline, new[]{ ConnectorActive }, ConnectorOffline, new[]{ LabRoles.System, LabRoles.Admin }, "Позначити офлайн"),
        new(ConnectorActions.Disable, new[]{ ConnectorPending, ConnectorActive, ConnectorOffline }, ConnectorDisabled, new[]{ LabRoles.Admin }, "Вимкнути"),
        new(ConnectorActions.Enable, new[]{ ConnectorDisabled }, ConnectorPending, new[]{ LabRoles.Admin }, "Увімкнути (повторна реєстрація)"),
        new(ConnectorActions.RotateKey, new[]{ ConnectorActive, ConnectorOffline, ConnectorDisabled }, null, new[]{ LabRoles.Admin }, "Перевипустити ключ", true),
        new(ConnectorActions.Edit, new[]{ ConnectorPending, ConnectorActive, ConnectorOffline, ConnectorDisabled }, null, new[]{ LabRoles.Admin }, "Редагувати", true),
        new(ConnectorActions.Delete, new[]{ ConnectorPending, ConnectorDisabled }, null, new[]{ LabRoles.Admin }, "Видалити", true),
    };

    public static ActionRule[] RulesFor(string entity) => entity switch
    {
        LisEntities.Order => OrderRules,
        LisEntities.Sample => SampleRules,
        LisEntities.OrderTest => TestRules,
        LisEntities.Manifest => ManifestRules,
        LisEntities.Culture => CultureRules,
        LisEntities.Connector => ConnectorRules,
        _ => throw new ArgumentException($"Невідома сутність машини станів: {entity}")
    };

    public static string EntityLabel(string entity) => entity switch
    {
        LisEntities.Order => "Замовлення",
        LisEntities.Sample => "Пробірка",
        LisEntities.OrderTest => "Тест",
        LisEntities.Manifest => "Маніфест",
        LisEntities.Culture => "Посів",
        LisEntities.Connector => "Коннектор",
        _ => entity
    };

    public static bool RoleAllowed(ActionRule rule, string? role)
    {
        if (role == LabRoles.System || role == LabRoles.Admin) return true;
        return role != null && rule.Roles.Contains(role);
    }

    /// <summary>Дії, доступні для поточного статусу і ролі (для кнопок UI).</summary>
    public static List<AllowedActionDto> AllowedActions(string entity, string status, string? role)
    {
        return RulesFor(entity)
            .Where(r => r.FromStatuses.Contains(status) && RoleAllowed(r, role))
            .Select(r => new AllowedActionDto(r.Action, r.Label, r.ToStatus))
            .ToList();
    }

    /// <summary>Усі переходи сутності (документація машини станів для UI/розробників).</summary>
    public static List<TransitionDto> Describe(string entity) =>
        RulesFor(entity).Select(r => new TransitionDto(r.Action, r.Label, r.FromStatuses, r.ToStatus, r.Roles)).ToList();

    /// <summary>
    /// Перевіряє дію: статус (409) та роль (403). Повертає правило (з цільовим статусом).
    /// </summary>
    public static ActionRule Ensure(string entity, string action, string currentStatus, string? role, string? objectLabel = null)
    {
        var rule = RulesFor(entity).FirstOrDefault(r => r.Action == action)
                   ?? throw new ArgumentException($"Невідома дія '{action}' для {entity}");
        var label = objectLabel ?? EntityLabel(entity);
        if (!rule.FromStatuses.Contains(currentStatus))
            throw new ConflictException($"{label}: дія «{rule.Label}» неможлива у статусі {currentStatus}. Дозволені статуси: {string.Join(", ", rule.FromStatuses)}");
        if (!RoleAllowed(rule, role))
            throw new ForbiddenException($"Дія «{rule.Label}» недоступна для ролі {role ?? "невідомо"}. Потрібна роль: {string.Join(" / ", rule.Roles)}");
        return rule;
    }

    /// <summary>Перевіряє, чи існує прямий перехід from→to хоча б за одним правилом.</summary>
    public static bool CanTransition(string entity, string from, string to) =>
        RulesFor(entity).Any(r => r.ToStatus == to && r.FromStatuses.Contains(from));
}

public sealed record AllowedActionDto(string Action, string Label, string? ToStatus);
public sealed record TransitionDto(string Action, string Label, string[] FromStatuses, string? ToStatus, string[] Roles);
