using LinePilot.Data;
using LinePilot.Models;
using LinePilot.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace LinePilot.Services;

public class WorkOrderService(AppDbContext db, TimeProvider clock)
{
    public const int PageSize = 10;

    /// <summary>Which statuses a work order may move to from its current status.</summary>
    public static readonly IReadOnlyDictionary<WorkOrderStatus, WorkOrderStatus[]> Transitions =
        new Dictionary<WorkOrderStatus, WorkOrderStatus[]>
        {
            [WorkOrderStatus.Planned] = [WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled],
            [WorkOrderStatus.InProgress] = [WorkOrderStatus.OnHold, WorkOrderStatus.Completed],
            [WorkOrderStatus.OnHold] = [WorkOrderStatus.InProgress, WorkOrderStatus.Cancelled],
            [WorkOrderStatus.Completed] = [],
            [WorkOrderStatus.Cancelled] = [],
        };

    private DateTime Now => clock.GetUtcNow().UtcDateTime;
    private DateOnly Today => DateOnly.FromDateTime(Now);

    public async Task<WorkOrderListViewModel> ListAsync(WorkOrderStatus? status, int? lineId, string? search, int page)
    {
        var query = db.WorkOrders.Include(w => w.Product).Include(w => w.Line).AsNoTracking();

        if (status is not null) query = query.Where(w => w.Status == status);
        if (lineId is not null) query = query.Where(w => w.LineId == lineId);
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(w => w.Number.ToLower().Contains(term)
                                     || w.Product.Name.ToLower().Contains(term)
                                     || w.Product.Sku.ToLower().Contains(term));
        }

        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)PageSize));
        page = Math.Clamp(page, 1, totalPages);

        var items = await query
            .OrderBy(w => w.Status == WorkOrderStatus.Completed || w.Status == WorkOrderStatus.Cancelled)
            .ThenBy(w => w.DueDate)
            .Skip((page - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync();

        var counts = await db.WorkOrders.GroupBy(w => w.Status)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        return new WorkOrderListViewModel
        {
            Items = items,
            Status = status,
            LineId = lineId,
            Search = search,
            Page = page,
            TotalPages = totalPages,
            TotalCount = total,
            StatusCounts = counts,
            Lines = await db.Lines.AsNoTracking().OrderBy(l => l.Code).ToListAsync(),
            Today = Today,
        };
    }

    public async Task<WorkOrder> GetAsync(int id) =>
        await db.WorkOrders.Include(w => w.Product).Include(w => w.Line).FirstOrDefaultAsync(w => w.Id == id)
        ?? throw new NotFoundException($"Work order {id} was not found.");

    public async Task<WorkOrder> CreateAsync(WorkOrderForm form)
    {
        if (form.DueDate < Today) throw new DomainException("Due date cannot be in the past.");
        await EnsureReferencesExist(form);

        var next = await db.WorkOrders.CountAsync() + 1001;
        var order = new WorkOrder
        {
            Number = $"WO-{Now:yyMM}-{next}",
            ProductId = form.ProductId,
            LineId = form.LineId,
            QuantityPlanned = form.QuantityPlanned,
            Priority = form.Priority,
            DueDate = form.DueDate,
            Notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim(),
            Status = WorkOrderStatus.Planned,
            CreatedAt = Now,
        };
        db.WorkOrders.Add(order);
        await db.SaveChangesAsync();
        return order;
    }

    public async Task UpdateAsync(int id, WorkOrderForm form)
    {
        var order = await GetAsync(id);
        if (!order.IsOpen) throw new DomainException($"{order.Number} is {order.Status.ToString().ToLower()} and can no longer be edited.");
        if (form.QuantityPlanned < order.QuantityProduced)
            throw new DomainException($"Planned quantity cannot be lower than the {order.QuantityProduced} units already produced.");
        await EnsureReferencesExist(form);

        order.ProductId = form.ProductId;
        order.LineId = form.LineId;
        order.QuantityPlanned = form.QuantityPlanned;
        order.Priority = form.Priority;
        order.DueDate = form.DueDate;
        order.Notes = string.IsNullOrWhiteSpace(form.Notes) ? null : form.Notes.Trim();
        await db.SaveChangesAsync();
    }

    public async Task<WorkOrder> ChangeStatusAsync(int id, WorkOrderStatus target)
    {
        var order = await GetAsync(id);
        if (!Transitions[order.Status].Contains(target))
            throw new DomainException($"A work order that is {Label(order.Status)} cannot be moved to {Label(target)}.");

        if (target == WorkOrderStatus.InProgress) order.StartedAt ??= Now;
        if (target == WorkOrderStatus.Completed)
        {
            if (order.QuantityProduced == 0) throw new DomainException("Record some output before completing the order.");
            order.CompletedAt = Now;
        }
        order.Status = target;
        await db.SaveChangesAsync();
        return order;
    }

    /// <summary>Adds good and scrapped units from a shift to an in-progress order.</summary>
    public async Task<WorkOrder> RecordOutputAsync(int id, int produced, int scrapped)
    {
        var order = await GetAsync(id);
        if (order.Status != WorkOrderStatus.InProgress)
            throw new DomainException("Output can only be recorded while the order is in progress.");
        if (produced == 0 && scrapped == 0) throw new DomainException("Enter produced or scrapped units.");

        var remaining = order.QuantityPlanned - order.QuantityProduced;
        if (produced > remaining)
            throw new DomainException($"Only {remaining} units are left to produce on this order.");

        order.QuantityProduced += produced;
        order.QuantityScrapped += scrapped;
        await db.SaveChangesAsync();
        return order;
    }

    public async Task<WorkOrderForm> BlankFormAsync() => await WithOptionsAsync(new WorkOrderForm
    {
        DueDate = Today.AddDays(7),
        QuantityPlanned = 500,
        Priority = Priority.Normal,
    });

    public async Task<WorkOrderForm> FormForAsync(WorkOrder order) => await WithOptionsAsync(new WorkOrderForm
    {
        Id = order.Id,
        Number = order.Number,
        ProductId = order.ProductId,
        LineId = order.LineId,
        QuantityPlanned = order.QuantityPlanned,
        Priority = order.Priority,
        DueDate = order.DueDate,
        Notes = order.Notes,
    });

    /// <summary>Fills the dropdown options a form needs (also after a failed POST).</summary>
    public async Task<WorkOrderForm> WithOptionsAsync(WorkOrderForm form)
    {
        form.Products = await db.Products.AsNoTracking().OrderBy(p => p.Sku).ToListAsync();
        form.Lines = await db.Lines.AsNoTracking().OrderBy(l => l.Code).ToListAsync();
        return form;
    }

    public static string Label(WorkOrderStatus s) => s switch
    {
        WorkOrderStatus.InProgress => "in progress",
        WorkOrderStatus.OnHold => "on hold",
        _ => s.ToString().ToLower(),
    };

    private async Task EnsureReferencesExist(WorkOrderForm form)
    {
        if (!await db.Products.AnyAsync(p => p.Id == form.ProductId)) throw new DomainException("Choose a valid product.");
        if (!await db.Lines.AnyAsync(l => l.Id == form.LineId)) throw new DomainException("Choose a valid production line.");
    }
}
