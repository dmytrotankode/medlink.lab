// =============================================================================
// MedLink LIS 4.0 — результат розбору вхідного повідомлення аналізатора
// (незалежно від протоколу) та контракт парсера.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using MedLink.LIS.Core.Contracts;

namespace MedLink.LIS.Core.Protocols;

/// <summary>Вид вхідного повідомлення аналізатора.</summary>
public enum InboundMessageKind
{
    /// <summary>Невідоме / службове повідомлення (без результатів і запитів).</summary>
    Other = 0,
    /// <summary>Запит замовлення за штрихкодом (Q-запис, QRD/QRY^Q02, SMP_NEW_AV тощо).</summary>
    Query = 1,
    /// <summary>Результати вимірювань.</summary>
    Results = 2,
    /// <summary>Підтвердження (ACK/MSA) від приладу.</summary>
    Ack = 3,
    /// <summary>Повідомлення, що потребує лише протокольної відповіді (напр. RAPID SYS_READY → ACK-кадр, PAT_DEMOG_REQ).</summary>
    ProtocolReply = 4,
}

/// <summary>Дані пацієнта, витягнуті з повідомлення (P-запис / PID), якщо є.</summary>
public sealed class InboundPatientInfo
{
    public string? Id { get; set; }
    public string? LastName { get; set; }
    public string? FirstName { get; set; }
    public string? MiddleName { get; set; }
    public DateTime? BirthDate { get; set; }
    /// <summary>M | F | U</summary>
    public string? Gender { get; set; }
}

/// <summary>Уніфікований результат розбору повідомлення приладу.</summary>
public sealed class AnalyzerInboundMessage
{
    public InboundMessageKind Kind { get; set; } = InboundMessageKind.Other;
    /// <summary>Штрихкоди проб, згадані у повідомленні (для Query — ті, за якими треба знайти замовлення).</summary>
    public List<string> Barcodes { get; set; } = new();
    /// <summary>Перший штрихкод або порожній рядок.</summary>
    public string Barcode => Barcodes.Count > 0 ? Barcodes[0] : "";
    /// <summary>«Сирий» токен штрихкоду із запиту (напр. Cobas "^^S1^SC" або Sysmex "^^^^barcode^"), потрібен побудовникам замовлень.</summary>
    public string? RawQueryToken { get; set; }
    /// <summary>Позиція проби (стійка/позиція) із запиту, якщо прилад її передає (Mindray BS-240 HL7: MSH-10; Cobas: rack^pos).</summary>
    public string? SamplePosition { get; set; }
    public InboundPatientInfo? Patient { get; set; }
    public List<AnalyzerResultItemDto> Results { get; set; } = new();
    /// <summary>Результати контролю якості (прилад позначив пробу як QC).</summary>
    public bool IsQc { get; set; }
    /// <summary>Готова відповідь протоколу (байти як рядок Latin-1), яку треба негайно відправити приладу (RAPID ACK-кадр, PAT_DEMOG_DATA, HL7 ACK…).</summary>
    public string? ImmediateReply { get; set; }
    /// <summary>Попередження парсера (не фатальні).</summary>
    public List<string> Warnings { get; set; } = new();
    /// <summary>Повідомлення у вигляді тексту (після зняття кадрування).</summary>
    public string Raw { get; set; } = "";
    /// <summary>Ідентифікатор повідомлення (MSH-10 для HL7), якщо є.</summary>
    public string? MessageControlId { get; set; }

    public static AnalyzerInboundMessage Other(string raw, string? warning = null)
    {
        var m = new AnalyzerInboundMessage { Kind = InboundMessageKind.Other, Raw = raw };
        if (warning != null) m.Warnings.Add(warning);
        return m;
    }

    public void AddResult(string barcode, string code, string value, string? unit = null, string? flags = null, string? referenceText = null, DateTime? measuredAt = null)
    {
        code = (code ?? "").Trim();
        if (code.Length == 0) return;
        Results.Add(new AnalyzerResultItemDto
        {
            Barcode = (barcode ?? "").Trim(),
            AnalyzerCode = code,
            Value = (value ?? "").Trim(),
            Unit = string.IsNullOrWhiteSpace(unit) ? null : unit.Trim(),
            Flags = string.IsNullOrWhiteSpace(flags) ? null : flags.Trim(),
            ReferenceText = string.IsNullOrWhiteSpace(referenceText) ? null : referenceText.Trim(),
            MeasuredAt = measuredAt,
        });
    }
}

/// <summary>Парсер повідомлень конкретного сімейства приладів (parserKind профілю).</summary>
public interface IAnalyzerMessageParser
{
    /// <summary>Код виду парсера (ASTM_GENERIC, ASTM_COBAS, HL7_ORU, TEXT_RAPID …).</summary>
    string Kind { get; }

    /// <summary>Розбирає повідомлення. Не кидає виключень: проблеми — у Warnings, Kind = Other.</summary>
    AnalyzerInboundMessage Parse(AnalyzerConfigDto cfg, string raw);
}
