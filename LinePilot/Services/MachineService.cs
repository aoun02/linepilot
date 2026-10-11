using LinePilot.Data;
using LinePilot.Models;
using Microsoft.EntityFrameworkCore;

namespace LinePilot.Services;

public class MachineService(AppDbContext db, TimeProvider clock)
{
    private DateTime Now => clock.GetUtcNow().UtcDateTime;

    public Task<List<ProductionLine>> LinesWithMachinesAsync() =>
        db.Lines.Include(l => l.Machines).AsNoTracking().OrderBy(l => l.Code).ToListAsync();

    public async Task<Machine> GetAsync(int id) =>
        await db.Machines
            .Include(m => m.Line)
            .Include(m => m.DowntimeEvents.OrderByDescending(e => e.StartedAt))
            .FirstOrDefaultAsync(m => m.Id == id)
        ?? throw new NotFoundException($"Machine {id} was not found.");

    /// <summary>
    /// Changes a machine's status. Stopping it (Down / Maintenance) opens a downtime event with the
    /// given reason; starting it again closes any open event, so downtime is always accounted for.
    /// </summary>
    public async Task<Machine> ChangeStatusAsync(int id, MachineStatus status, string? reason)
    {
        var machine = await GetAsync(id);
        if (machine.Status == status) throw new DomainException($"{machine.Code} is already {status.ToString().ToLower()}.");

        var stopping = status is MachineStatus.Down or MachineStatus.Maintenance;
        if (stopping && string.IsNullOrWhiteSpace(reason))
            throw new DomainException("Enter a reason when stopping a machine.");

        foreach (var open in machine.DowntimeEvents.Where(e => e.EndedAt is null))
        {
            open.EndedAt = Now;
        }
        if (stopping)
        {
            machine.DowntimeEvents.Add(new DowntimeEvent { Kind = status, Reason = reason!.Trim(), StartedAt = Now });
        }
        if (status == MachineStatus.Maintenance)
        {
            machine.LastMaintenance = DateOnly.FromDateTime(Now);
        }

        machine.Status = status;
        machine.StatusSince = Now;
        await db.SaveChangesAsync();
        return machine;
    }
}
