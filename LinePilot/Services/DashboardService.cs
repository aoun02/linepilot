using LinePilot.Data;
using LinePilot.Models;
using LinePilot.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace LinePilot.Services;

public class DashboardService(AppDbContext db, TimeProvider clock)
{
    public async Task<DashboardViewModel> BuildAsync()
    {
        var now = clock.GetUtcNow().UtcDateTime;
        var today = DateOnly.FromDateTime(now);
        var from = today.AddDays(-13);

        var records = await db.ProductionRecords.Include(r => r.Line).AsNoTracking()
            .Where(r => r.Date >= from).ToListAsync();
        var todays = records.Where(r => r.Date == today).ToList();

        var trend = records.GroupBy(r => r.Date).OrderBy(g => g.Key)
            .Select(g => new DayOutput(g.Key, g.Sum(r => r.Produced), g.Sum(r => r.Target)))
            .ToList();

        var machines = await db.Machines.Include(m => m.Line).AsNoTracking().OrderBy(m => m.Code).ToListAsync();
        var openOrders = await db.WorkOrders.Include(w => w.Product).Include(w => w.Line).AsNoTracking()
            .Where(w => w.Status == WorkOrderStatus.Planned || w.Status == WorkOrderStatus.InProgress || w.Status == WorkOrderStatus.OnHold)
            .ToListAsync();
        var activeDowntime = await db.DowntimeEvents.Include(d => d.Machine).AsNoTracking()
            .Where(d => d.EndedAt == null).OrderBy(d => d.StartedAt).ToListAsync();

        var produced = todays.Sum(r => r.Produced);
        var target = todays.Sum(r => r.Target);
        var scrapped = todays.Sum(r => r.Scrapped);

        return new DashboardViewModel
        {
            Now = now,
            TodayProduced = produced,
            TodayTarget = target,
            ScrapRatePercent = produced + scrapped == 0 ? 0 : Math.Round(scrapped * 100.0 / (produced + scrapped), 1),
            MachinesRunning = machines.Count(m => m.Status == MachineStatus.Running),
            MachinesTotal = machines.Count,
            OpenOrders = openOrders.Count,
            OverdueOrders = openOrders.Count(o => o.IsOverdue(today)),
            Trend = trend,
            LinesToday = todays.OrderBy(r => r.Line.Code)
                .Select(r => new LineOutput(r.Line.Name, r.Produced, r.Target)).ToList(),
            Machines = machines,
            ActiveDowntime = activeDowntime,
            PriorityOrders = openOrders
                .OrderByDescending(o => o.IsOverdue(today))
                .ThenByDescending(o => o.Priority)
                .ThenBy(o => o.DueDate)
                .Take(5).ToList(),
            Today = today,
        };
    }
}
