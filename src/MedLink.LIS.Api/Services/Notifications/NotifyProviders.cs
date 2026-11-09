// Провайдери каналів сповіщень: Email (SMTP, System.Net.Mail) та HTTP-шлюз для SMS/Viber/Telegram. Без налаштувань канал не відправляє (IsConfigured = false).
using System.Net;
using System.Net.Http.Headers;
using System.Net.Mail;
using System.Text;
using System.Text.Json;

namespace MedLink.LIS.Api.Services.Notifications;

public sealed record NotifyMessage(string Id, string Channel, string Recipient, string Subject, string Body);

/// <param name="Transient">true — помилка тимчасова (повторити з паузою); false — остаточна.</param>
public sealed record NotifySendResult(bool Ok, bool Transient = false, string? Error = null, string? ProviderMessageId = null)
{
    public static NotifySendResult Success(string? id = null) => new(true, false, null, id);
    public static NotifySendResult Temporary(string error) => new(false, true, error);
    public static NotifySendResult Permanent(string error) => new(false, false, error);
}

public interface INotifyChannelProvider
{
    string Channel { get; }
    bool IsConfigured { get; }
    Task<NotifySendResult> SendAsync(NotifyMessage message, CancellationToken ct = default);
}

/// <summary>Транспорт SMTP (відокремлено від провайдера для підміни в тестах).</summary>
public interface ISmtpTransport
{
    Task SendAsync(NotifyEmailOptions options, MailMessage message, CancellationToken ct);
}

public sealed class SystemSmtpTransport : ISmtpTransport
{
    public async Task SendAsync(NotifyEmailOptions o, MailMessage message, CancellationToken ct)
    {
        using var client = new SmtpClient(o.Host, o.Port) { EnableSsl = o.UseSsl, Timeout = Math.Max(1, o.TimeoutSeconds) * 1000, DeliveryMethod = SmtpDeliveryMethod.Network };
        if (!string.IsNullOrWhiteSpace(o.User)) client.Credentials = new NetworkCredential(o.User, o.Password ?? "");
        await client.SendMailAsync(message, ct);
    }
}

public sealed class SmtpEmailProvider : INotifyChannelProvider
{
    private readonly NotifyOptions _options;
    private readonly ISmtpTransport _transport;
    public SmtpEmailProvider(NotifyOptions options, ISmtpTransport transport) { _options = options; _transport = transport; }

    public string Channel => NotifyChannels.Email;
    public bool IsConfigured => _options.Email.IsConfigured;

    public async Task<NotifySendResult> SendAsync(NotifyMessage m, CancellationToken ct = default)
    {
        if (!IsConfigured) return NotifySendResult.Permanent("Канал електронної пошти не налаштовано");
        try
        {
            using var mail = new MailMessage
            {
                From = new MailAddress(_options.Email.From!, _options.Email.FromName ?? "", Encoding.UTF8),
                Subject = m.Subject, SubjectEncoding = Encoding.UTF8, Body = m.Body, BodyEncoding = Encoding.UTF8, IsBodyHtml = false
            };
            mail.To.Add(new MailAddress(m.Recipient));
            mail.Headers.Add("X-MedLink-Message-Id", m.Id);
            await _transport.SendAsync(_options.Email, mail, ct);
            return NotifySendResult.Success();
        }
        catch (FormatException) { return NotifySendResult.Permanent("Некоректна адреса електронної пошти"); }
        catch (OperationCanceledException) { throw; }
        catch (SmtpException ex) { return Classify(ex); }
        catch (Exception ex) { return NotifySendResult.Temporary(Short("Помилка відправки пошти: " + ex.Message)); }
    }

    private static NotifySendResult Classify(SmtpException ex)
    {
        var permanent = ex.StatusCode is SmtpStatusCode.MailboxUnavailable or SmtpStatusCode.MailboxNameNotAllowed or SmtpStatusCode.ClientNotPermitted
            or SmtpStatusCode.UserNotLocalTryAlternatePath or SmtpStatusCode.SyntaxError or SmtpStatusCode.CommandNotImplemented;
        return permanent ? NotifySendResult.Permanent(Short("Пошту відхилено: " + ex.Message)) : NotifySendResult.Temporary(Short("Тимчасова помилка пошти: " + ex.Message));
    }

    private static string Short(string s) => s.Length > 300 ? s[..300] : s;
}

/// <summary>HTTP-шлюз каналу (SMS/Viber/Telegram): POST JSON {messageId, to, text, sender} на налаштовану адресу, Authorization: Bearer {ApiKey}.</summary>
public sealed class HttpNotifyProvider : INotifyChannelProvider
{
    private readonly string _channel;
    private readonly NotifyHttpChannelOptions _options;
    private readonly IHttpClientFactory _factory;
    public HttpNotifyProvider(string channel, NotifyHttpChannelOptions options, IHttpClientFactory factory) { _channel = channel; _options = options; _factory = factory; }

    public string Channel => _channel;
    public bool IsConfigured => _options.IsConfigured;

    public async Task<NotifySendResult> SendAsync(NotifyMessage m, CancellationToken ct = default)
    {
        if (!IsConfigured) return NotifySendResult.Permanent("Канал не налаштовано");
        try
        {
            using var http = _factory.CreateClient("notify");
            http.Timeout = TimeSpan.FromSeconds(Math.Max(1, _options.TimeoutSeconds));
            using var req = new HttpRequestMessage(HttpMethod.Post, _options.Url)
            {
                Content = new StringContent(JsonSerializer.Serialize(new { messageId = m.Id, channel = m.Channel, to = m.Recipient, text = m.Body, sender = _options.Sender }), Encoding.UTF8, "application/json")
            };
            if (!string.IsNullOrWhiteSpace(_options.ApiKey)) req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ApiKey);
            req.Headers.TryAddWithoutValidation("Idempotency-Key", m.Id);
            using var resp = await http.SendAsync(req, ct);
            var code = (int)resp.StatusCode;
            if (resp.IsSuccessStatusCode) return NotifySendResult.Success(await ReadIdAsync(resp, ct));
            var err = $"Шлюз повернув помилку {code}";
            return code is 408 or 425 or 429 || code >= 500 ? NotifySendResult.Temporary(err) : NotifySendResult.Permanent(err);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { return NotifySendResult.Temporary(ex is TaskCanceledException ? "Шлюз не відповів вчасно" : "Шлюз недоступний: " + (ex.Message.Length > 200 ? ex.Message[..200] : ex.Message)); }
    }

    private static async Task<string?> ReadIdAsync(HttpResponseMessage resp, CancellationToken ct)
    {
        try
        {
            using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
            foreach (var name in new[] { "id", "messageId" })
                if (doc.RootElement.ValueKind == JsonValueKind.Object && doc.RootElement.TryGetProperty(name, out var v) && v.ValueKind is JsonValueKind.String or JsonValueKind.Number) return v.ToString();
        }
        catch { /* тіло відповіді необов'язкове */ }
        return null;
    }
}
