// Виробничий календар (FR-GAP-053): робочий тиждень пн–пт + винятки з довідника lab_work_calendar
// (святкові дні в будні, робочі дні на вихідні). Використовується для строків (SLA патоморфології) і термінів TAT.
using MedLink.LIS.Api.Data;
using MedLink.LIS.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace MedLink.LIS.Api.Services;

public static class WorkCalendar
{
    private static volatile HashSet<DateTime> _holidays = new();
    private static volatile HashSet<DateTime> _workdays = new();

    public static async Task RefreshAsync(LisDbContext db)
    {
        var rows = await db.WorkCalendar.AsNoTracking().Where(x => x.IsActive && x.RecordState != RecordStates.Deleted).ToListAsync();
        _holidays = rows.Where(r => r.Kind == CalendarDayKinds.Holiday).Select(r => r.Day.Date).ToHashSet();
        _workdays = rows.Where(r => r.Kind == CalendarDayKinds.Workday).Select(r => r.Day.Date).ToHashSet();
    }

    public static bool IsWorkday(DateTime day)
    {
        var d = day.Date;
        if (_holidays.Contains(d)) return false;
        if (_workdays.Contains(d)) return true;
        return d.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);
    }

    /// <summary>Додає робочі дні (початковий день не рахується); вихідні, свята й перенесення беруться з календаря.</summary>
    public static DateTime AddWorkdays(DateTime from, int days)
    {
        var d = from;
        var guard = 0;
        while (days > 0 && guard++ < 3660) { d = d.AddDays(1); if (IsWorkday(d)) days--; }
        return d;
    }

    /// <summary>Кількість робочих днів між датами (від початку, не включно, до кінця включно).</summary>
    public static int WorkdaysBetween(DateTime from, DateTime to)
    {
        var n = 0; var d = from.Date;
        var guard = 0;
        while (d < to.Date && guard++ < 3660) { d = d.AddDays(1); if (IsWorkday(d)) n++; }
        return n;
    }
}
