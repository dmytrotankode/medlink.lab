// =============================================================================
// MedLink LIS Analyzer Connector — локальна конфігурація (appsettings.json +
// data/appsettings.local.json, що створюється командою setup).
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using MedLink.LIS.Core.Contracts;

namespace MedLink.LabConnector.Configuration;

public sealed class ConnectorOptions
{
    public const string SectionName = "";

    public ServerOptions Server { get; set; } = new();
    public LocalOptions Local { get; set; } = new();
    /// <summary>Налаштування локального агента друку етикеток (ZPL).</summary>
    public PrintingOptions Printing { get; set; } = new();
    /// <summary>Повністю локальні визначення аналізаторів (тієї ж форми, що AnalyzerConfigDto). Перекривають серверні за Code/AnalyzerId.</summary>
    public List<AnalyzerConfigDto> Analyzers { get; set; } = new();
}

public sealed class ServerOptions
{
    /// <summary>Базова адреса сервера ЛІС, напр. https://lis.example.ua (суфікс /api/v1/lab додається автоматично).</summary>
    public string? BaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? ConnectorId { get; set; }
    public int TimeoutSec { get; set; } = 30;
    /// <summary>Ігнорувати помилки TLS-сертифіката (лише для внутрішніх мереж із самопідписаними сертифікатами).</summary>
    public bool AllowInvalidCertificate { get; set; }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(ApiKey);

    /// <summary>Нормалізує адресу: додає /api/v1/lab, якщо шлях не містить /api/.</summary>
    public static string NormalizeBaseUrl(string url)
    {
        var u = url.Trim().TrimEnd('/');
        if (!u.StartsWith("http://", StringComparison.OrdinalIgnoreCase) && !u.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            u = "https://" + u;
        if (!u.Contains("/api/", StringComparison.OrdinalIgnoreCase)) u += "/api/v1/lab";
        return u + "/";
    }
}

public sealed class PrintingOptions
{
    /// <summary>Локальний принтер за замовчуванням (ім'я черги ОС), напр. "ZDesigner GK420t".</summary>
    public string? DefaultLabelPrinter { get; set; }
    /// <summary>Мережевий принтер за замовчуванням (якщо задано — має пріоритет над DefaultLabelPrinter).</summary>
    public string? Host { get; set; }
    public int? Port { get; set; } = 9100;
}

public sealed class LocalOptions
{
    /// <summary>Назва інсталяції (показується у веб-інтерфейсі ЛІС).</summary>
    public string? ConnectorName { get; set; }
    /// <summary>Порт локальної сторінки статусу (http://localhost:{StatusPort}/).</summary>
    public int StatusPort { get; set; } = 5088;
    /// <summary>Каталог даних (кеш конфігурації, офлайн-буфер, журнали). Відносний — від каталогу програми.</summary>
    public string DataDir { get; set; } = "data";
    public int HeartbeatIntervalSec { get; set; } = 30;
    /// <summary>Інтервал опитування /connector/orders/pending для приладів без режиму запиту (0 — вимкнено).</summary>
    public int PendingOrdersPollSec { get; set; } = 60;
    /// <summary>Початковий інтервал повтору відправки з офлайн-буфера.</summary>
    public int ResultRetrySec { get; set; } = 15;
    /// <summary>Максимальний інтервал повтору (експоненційне зростання).</summary>
    public int ResultRetryMaxSec { get; set; } = 300;
    public int LogRetentionDays { get; set; } = 14;
    /// <summary>Записувати «сирий» трафік приладів у data/logs/raw-*.log.</summary>
    public bool LogRawTraffic { get; set; } = true;
    /// <summary>Таймаут очікування ACK/кадру в ASTM-сесії, с.</summary>
    public int AstmTimeoutSec { get; set; } = 15;
    /// <summary>Максимум спроб повтору кадру/ENQ.</summary>
    public int AstmMaxRetries { get; set; } = 6;
    /// <summary>Таймер «кінця пакета» для протоколів без eop (HL7 без MLLP, CYAN, ASTM2), мс.</summary>
    public int PacketIdleMs { get; set; } = 700;
    /// <summary>Keep-alive NUL для TCP-клієнта кожні N секунд тиші (як у legacy; 0 — вимкнено).</summary>
    public int TcpKeepAliveSec { get; set; } = 60;
    /// <summary>Інтервал повторного підключення TCP-клієнта, с.</summary>
    public int TcpReconnectSec { get; set; } = 5;
    /// <summary>Відповідати приладу «замовлення відсутнє» (O-26=Y), якщо сервер не знайшов пробу. Legacy не відповідав.</summary>
    public bool SendNoOrderReply { get; set; }
}
