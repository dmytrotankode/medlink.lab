// Додаток E ТЗ, юніт-тести U-*: маска нумерації, QR-токен, межі TAT та SLA, граничні випадки правил
using MedLink.LIS.Api.Services;
using MedLink.LIS.Core.Clinical;
using Microsoft.Extensions.Configuration;

namespace MedLink.LIS.Tests.Clinical;

public class AppendixEUnitTests
{
    [Theory, Trait("TZ", "U-OrderNumberMask")]
    [InlineData("{yyMM}-{000000}", "2026-03-05", 42, "2603-000042")]
    [InlineData("{yyMM}-{000000}", "2026-12-31", 1, "2612-000001")]
    [InlineData("MAN-{yyMMdd}-{0000}", "2026-03-05", 7, "MAN-260305-0007")]
    [InlineData("WL-{yyMMdd}-{000}", "2026-11-09", 1234, "WL-261109-1234")]   // лічильник ширший за маску не обрізається
    [InlineData("{yyyy}/{00000}", "2027-01-01", 99, "2027/00099")]
    [InlineData("LIS-{000000}", "2026-03-05", 5, "LIS-000005")]
    public void Mask_formats_date_parts_and_zero_padded_counter(string mask, string date, long value, string expected)
        => Assert.Equal(expected, NumeratorService.ApplyMask(mask, DateTime.Parse(date, System.Globalization.CultureInfo.InvariantCulture), value));

    private static OrderStateService State(string? secret)
    {
        var cfg = new ConfigurationBuilder().AddInMemoryCollection(secret == null ? new Dictionary<string, string?>() : new() { ["Lab:VerifySecret"] = secret }).Build();
        return new OrderStateService(cfg);
    }

    [Fact, Trait("TZ", "U-QrToken-hmac")]
    public void Verify_token_is_deterministic_urlsafe_and_bound_to_order_time_and_secret()
    {
        var at = new DateTime(2026, 3, 5, 10, 0, 0, DateTimeKind.Utc);
        var s = State("secret-A");
        var t = s.VerifyToken("order-1", at);
        Assert.Equal(t, s.VerifyToken("order-1", at));                              // детермінований
        Assert.Matches("^[A-Za-z0-9_-]+$", t);                                      // без + / =
        Assert.Equal(43, t.Length);                                                 // SHA-256 у base64url без доповнення
        Assert.NotEqual(t, s.VerifyToken("order-2", at));                           // залежить від замовлення
        Assert.NotEqual(t, s.VerifyToken("order-1", at.AddSeconds(1)));             // і від часу видачі (повторна видача → новий токен)
        Assert.NotEqual(t, State("secret-B").VerifyToken("order-1", at));           // і від секрету
    }
}
