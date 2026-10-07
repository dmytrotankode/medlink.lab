// =============================================================================
// MedLink LIS 4.0 — модель повідомлення ASTM E1394 (записи H,P,O,Q,R,C,L,M)
// з доступом до полів / компонентів / повторів та парсер тексту у модель.
// Copyright (c) 2026 ТОВ "МедЛінк" (MedLink LLC)
// =============================================================================
using System.Text;

namespace MedLink.LIS.Core.Protocols;

/// <summary>Роздільники ASTM (беруться із запису H, за замовчуванням |\^&amp;).</summary>
public sealed record AstmDelimiters(char Field = '|', char Repeat = '\\', char Component = '^', char Escape = '&')
{
    public static AstmDelimiters Default { get; } = new();

    /// <summary>Визначає роздільники із заголовка «H|\^&amp;…».</summary>
    public static AstmDelimiters FromHeader(string headerRecord)
    {
        if (headerRecord.Length >= 5 && headerRecord[0] == 'H')
            return new AstmDelimiters(headerRecord[1], headerRecord[2], headerRecord[3], headerRecord[4]);
        return Default;
    }
}

/// <summary>Один запис ASTM: тип (H, P, O, Q, R, C, L, M …) та поля (індексація як у стандарті: поле 1 = тип).</summary>
public sealed class AstmRecord
{
    public AstmRecord(string type, IReadOnlyList<string> fields, AstmDelimiters delimiters, string rawText)
    {
        Type = type;
        Fields = fields;
        Delimiters = delimiters;
        RawText = rawText;
    }

    /// <summary>Тип запису: "H", "P", "O", "Q", "R", "C", "L", "M" або інший.</summary>
    public string Type { get; }
    /// <summary>Поля запису; Fields[0] — тип запису, Fields[1] — порядковий номер тощо (ASTM-нумерація мінус 1).</summary>
    public IReadOnlyList<string> Fields { get; }
    public AstmDelimiters Delimiters { get; }
    public string RawText { get; }

    /// <summary>Поле за ASTM-номером (1 = тип запису, 2 = seq, 3 = …). Порожній рядок, якщо поля немає.</summary>
    public string Field(int astmFieldNumber)
    {
        int idx = astmFieldNumber - 1;
        return idx >= 0 && idx < Fields.Count ? Fields[idx] : "";
    }

    /// <summary>Компонент (1-based) заданого поля; повтори не враховуються (береться перший повтор).</summary>
    public string Component(int astmFieldNumber, int component)
    {
        var f = Field(astmFieldNumber);
        var firstRepeat = f.Split(Delimiters.Repeat)[0];
        var parts = firstRepeat.Split(Delimiters.Component);
        return component >= 1 && component <= parts.Length ? parts[component - 1] : "";
    }

    /// <summary>Повтори поля (розділені \).</summary>
    public string[] Repeats(int astmFieldNumber)
    {
        var f = Field(astmFieldNumber);
        return f.Length == 0 ? Array.Empty<string>() : f.Split(Delimiters.Repeat);
    }

    /// <summary>Компоненти поля (перший повтор), розділені ^.</summary>
    public string[] Components(int astmFieldNumber)
    {
        var f = Field(astmFieldNumber).Split(Delimiters.Repeat)[0];
        return f.Split(Delimiters.Component);
    }

    /// <summary>Порядковий номер запису (поле 2) або 0.</summary>
    public int SequenceNumber => int.TryParse(Field(2), out var n) ? n : 0;

    public override string ToString() => RawText;
}

/// <summary>Повідомлення ASTM — упорядкований список записів.</summary>
public sealed class AstmMessage
{
    public AstmMessage(IReadOnlyList<AstmRecord> records, string rawText)
    {
        Records = records;
        RawText = rawText;
    }

    public IReadOnlyList<AstmRecord> Records { get; }
    public string RawText { get; }

    public AstmRecord? Header => Records.FirstOrDefault(r => r.Type == "H");
    public IEnumerable<AstmRecord> OfType(string type) => Records.Where(r => string.Equals(r.Type, type, StringComparison.OrdinalIgnoreCase));
    public AstmRecord? First(string type) => OfType(type).FirstOrDefault();
    public bool Has(string type) => OfType(type).Any();

    /// <summary>
    /// Групує записи за «пацієнтськими блоками»: кожен запис P відкриває новий блок; O/R/C належать останньому P.
    /// Якщо P відсутній — усі записи в одному блоці.
    /// </summary>
    public IReadOnlyList<IReadOnlyList<AstmRecord>> PatientBlocks()
    {
        var blocks = new List<List<AstmRecord>>();
        List<AstmRecord>? current = null;
        foreach (var r in Records)
        {
            if (r.Type == "H" || r.Type == "L") continue;
            if (r.Type == "P" || current == null)
            {
                current = new List<AstmRecord>();
                blocks.Add(current);
            }
            current.Add(r);
        }
        return blocks;
    }

    /// <summary>Групує за замовленнями: кожен O відкриває групу, R/C належать останньому O.</summary>
    public IReadOnlyList<IReadOnlyList<AstmRecord>> OrderBlocks()
    {
        var blocks = new List<List<AstmRecord>>();
        List<AstmRecord>? current = null;
        foreach (var r in Records)
        {
            if (r.Type is "H" or "L" or "P" or "Q") continue;
            if (r.Type == "O" || current == null)
            {
                current = new List<AstmRecord>();
                blocks.Add(current);
            }
            current.Add(r);
        }
        return blocks;
    }
}

/// <summary>Парсер тексту ASTM (із кадруванням або без) у модель <see cref="AstmMessage"/>.</summary>
public static class AstmParser
{
    /// <summary>
    /// Розбирає повідомлення. Приймає як чистий текст записів (розділених CR / CR LF / LF),
    /// так і «сирий» потік із STX/ETX/ETB/контрольними сумами — кадрування буде зчищене.
    /// Ніколи не кидає виключень.
    /// </summary>
    public static AstmMessage Parse(string raw)
    {
        raw ??= "";
        var text = raw.IndexOfAny(new[] { AstmControl.StxChar, AstmControl.EtxChar, AstmControl.EtbChar }) >= 0
            ? AstmFrameCodec.StripFraming(raw)
            : raw;
        var delimiters = AstmDelimiters.Default;
        var lines = text.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        var records = new List<AstmRecord>(lines.Length);
        foreach (var lineRaw in lines)
        {
            var line = lineRaw.Trim('\0', ' ');
            if (line.Length == 0) continue;
            // Залишок номера кадру перед типом запису ("2P|1||…") — прибираємо
            if (line.Length >= 2 && char.IsDigit(line[0]) && char.IsLetter(line[1]) && line.Length > 2 && (line[2] == '|' || line[2] == delimiters.Field))
                line = line[1..];
            if (line.StartsWith("H") && line.Length >= 5 && !char.IsLetterOrDigit(line[1]))
                delimiters = AstmDelimiters.FromHeader(line);
            var fields = SplitFields(line, delimiters);
            var type = fields.Count > 0 ? fields[0].Trim() : "";
            if (type.Length > 1) type = type[..1];
            records.Add(new AstmRecord(type.ToUpperInvariant(), fields, delimiters, line));
        }
        return new AstmMessage(records, raw);
    }

    private static List<string> SplitFields(string line, AstmDelimiters d)
    {
        var result = new List<string>();
        if (line.StartsWith("H") && line.Length >= 5 && line[1] == d.Field)
        {
            // Запис H: поле 2 — роздільники (без поля-роздільника всередині)
            result.Add("H");
            result.Add(line.Substring(2, 3));
            var rest = line.Length > 5 ? line[6..] : "";
            if (line.Length > 5) result.AddRange(rest.Split(d.Field));
            return result;
        }
        result.AddRange(line.Split(d.Field));
        return result;
    }

    /// <summary>Склеює записи у текст ASTM (CR між записами, без кадрування).</summary>
    public static string Join(IEnumerable<string> records)
    {
        var sb = new StringBuilder();
        foreach (var r in records) sb.Append(r.TrimEnd(AstmControl.CrChar, AstmControl.LfChar)).Append(AstmControl.CrChar);
        return sb.ToString();
    }
}
