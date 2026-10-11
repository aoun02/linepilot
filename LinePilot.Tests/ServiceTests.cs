using LinePilot.Data;
using LinePilot.Models;
using LinePilot.Services;
using LinePilot.ViewModels;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace LinePilot.Tests;

/// <summary>Business-rule tests against a fresh, seeded in-memory SQLite database per test.</summary>
public sealed class ServiceTests : IDisposable
{
    private readonly SqliteConnection _connection = new("Data Source=:memory:");
    private readonly AppDbContext _db;
    private readonly TimeProvider _clock = TimeProvider.System;

    public ServiceTests()
    {
        _connection.Open();
        _db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseSqlite(_connection).Options);
        _db.Database.EnsureCreated();
        SeedData.Populate(_db, _clock.GetUtcNow().UtcDateTime);
    }

    public void Dispose()
    {
        _db.Dispose();
        _connection.Dispose();
    }

    private WorkOrderService Orders => new(_db, _clock);
    private DateOnly Today => DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);

    private WorkOrder First(WorkOrderStatus status) => _db.WorkOrders.First(w => w.Status == status);

    [Fact]
    public async Task Dashboard_reports_today_and_fourteen_day_trend()
    {
        var vm = await new DashboardService(_db, _clock).BuildAsync();

        Assert.Equal(14, vm.Trend.Count);
        Assert.Equal(3, vm.LinesToday.Count);
        Assert.Equal(12, vm.MachinesTotal);
        Assert.Equal(vm.LinesToday.Sum(l => l.Produced), vm.TodayProduced);
        Assert.Equal(2, vm.ActiveDowntime.Count); // one machine down, one in maintenance
    }

    [Fact]
    public async Task Create_assigns_number_and_starts_as_planned()
    {
        var form = new WorkOrderForm { ProductId = 1, LineId = 1, QuantityPlanned = 300, Priority = Priority.High, DueDate = Today.AddDays(5) };

        var order = await Orders.CreateAsync(form);

        Assert.StartsWith("WO-", order.Number);
        Assert.Equal(WorkOrderStatus.Planned, order.Status);
    }

    [Fact]
    public async Task Create_rejects_due_date_in_the_past()
    {
        var form = new WorkOrderForm { ProductId = 1, LineId = 1, QuantityPlanned = 300, DueDate = Today.AddDays(-1) };

        var ex = await Assert.ThrowsAsync<DomainException>(() => Orders.CreateAsync(form));
        Assert.Contains("past", ex.Message);
    }

    [Fact]
    public async Task Planned_order_can_start_but_not_complete()
    {
        var planned = First(WorkOrderStatus.Planned);

        await Assert.ThrowsAsync<DomainException>(() => Orders.ChangeStatusAsync(planned.Id, WorkOrderStatus.Completed));

        var started = await Orders.ChangeStatusAsync(planned.Id, WorkOrderStatus.InProgress);
        Assert.Equal(WorkOrderStatus.InProgress, started.Status);
        Assert.NotNull(started.StartedAt);
    }

    [Fact]
    public async Task Output_cannot_exceed_remaining_quantity()
    {
        var order = First(WorkOrderStatus.InProgress);
        var remaining = order.QuantityPlanned - order.QuantityProduced;

        await Assert.ThrowsAsync<DomainException>(() => Orders.RecordOutputAsync(order.Id, remaining + 1, 0));

        var updated = await Orders.RecordOutputAsync(order.Id, remaining, 5);
        Assert.Equal(100, updated.ProgressPercent);
    }

    [Fact]
    public async Task Closed_orders_cannot_be_edited()
    {
        var done = First(WorkOrderStatus.Completed);
        var form = new WorkOrderForm { ProductId = 1, LineId = 1, QuantityPlanned = 999, DueDate = Today.AddDays(3) };

        await Assert.ThrowsAsync<DomainException>(() => Orders.UpdateAsync(done.Id, form));
    }

    [Fact]
    public async Task Stopping_a_machine_opens_downtime_and_restarting_closes_it()
    {
        var service = new MachineService(_db, _clock);
        var machine = _db.Machines.First(m => m.Status == MachineStatus.Running);

        await Assert.ThrowsAsync<DomainException>(() => service.ChangeStatusAsync(machine.Id, MachineStatus.Down, reason: null));

        await service.ChangeStatusAsync(machine.Id, MachineStatus.Down, "Coolant leak");
        Assert.Single(_db.DowntimeEvents.Where(e => e.MachineId == machine.Id && e.EndedAt == null));

        await service.ChangeStatusAsync(machine.Id, MachineStatus.Running, null);
        Assert.Empty(_db.DowntimeEvents.Where(e => e.MachineId == machine.Id && e.EndedAt == null));
    }

    [Fact]
    public async Task Receiving_stock_increases_on_hand_and_rejects_zero()
    {
        var service = new InventoryService(_db);
        var material = _db.Materials.First();
        var before = material.OnHand;

        await Assert.ThrowsAsync<DomainException>(() => service.ReceiveAsync(material.Id, 0));
        var updated = await service.ReceiveAsync(material.Id, 250);

        Assert.Equal(before + 250, updated.OnHand);
    }

    [Fact]
    public async Task Low_stock_filter_returns_only_items_at_or_below_reorder_level()
    {
        var low = await new InventoryService(_db).ListAsync(lowOnly: true);

        Assert.NotEmpty(low);
        Assert.All(low, m => Assert.True(m.OnHand <= m.ReorderLevel));
    }
}
