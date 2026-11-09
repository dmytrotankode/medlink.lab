namespace MedLink.LIS.Api.Infrastructure;

/// <summary>Час лабораторії (Київ), а не сервера: бланки й тексти для користувача мають збігатися з інтерфейсом.</summary>
public static class LabClock
{
    private static readonly TimeZoneInfo Tz = Resolve();

    private static TimeZoneInfo Resolve()
    {
        foreach (var id in new[] { "Europe/Kyiv", "Europe/Kiev", "FLE Standard Time" })
        {
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch (Exception) { /* немає такого ідентифікатора в системі */ }
        }
        return TimeZoneInfo.Local;
    }

    /// <summary>Значення з БД (UTC) → київський час.</summary>
    public static DateTime ToLab(this DateTime d) =>
        TimeZoneInfo.ConvertTimeFromUtc(d.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(d, DateTimeKind.Utc) : d.ToUniversalTime(), Tz);
}
