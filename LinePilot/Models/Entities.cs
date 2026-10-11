using System.ComponentModel.DataAnnotations;

namespace LinePilot.Models;

public enum MachineStatus { Running, Idle, Maintenance, Down }

public enum WorkOrderStatus
{
    Planned,
    [Display(Name = "In progress")] InProgress,
    [Display(Name = "On hold")] OnHold,
    Completed,
    Cancelled
}

public enum Priority { Low, Normal, High, Urgent }

public class ProductionLine
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public int DailyTarget { get; set; }

    public List<Machine> Machines { get; set; } = [];
}

public class Machine
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Type { get; set; } = "";
    public MachineStatus Status { get; set; }
    public DateTime StatusSince { get; set; }
    public DateOnly LastMaintenance { get; set; }

    /// <summary>Share of today's shift the machine has been running, 0-100.</summary>
    public int UtilizationToday { get; set; }

    public int LineId { get; set; }
    public ProductionLine Line { get; set; } = null!;
    public List<DowntimeEvent> DowntimeEvents { get; set; } = [];
}

public class DowntimeEvent
{
    public int Id { get; set; }
    public string Reason { get; set; } = "";
    public MachineStatus Kind { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    public int MachineId { get; set; }
    public Machine Machine { get; set; } = null!;

    public TimeSpan Duration(DateTime now) => (EndedAt ?? now) - StartedAt;
}

public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = "";
    public string Name { get; set; } = "";
}

public class WorkOrder
{
    public int Id { get; set; }
    public string Number { get; set; } = "";
    public int QuantityPlanned { get; set; }
    public int QuantityProduced { get; set; }
    public int QuantityScrapped { get; set; }
    public Priority Priority { get; set; }
    public WorkOrderStatus Status { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string? Notes { get; set; }

    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    public int LineId { get; set; }
    public ProductionLine Line { get; set; } = null!;

    public int ProgressPercent => QuantityPlanned == 0 ? 0 : Math.Min(100, QuantityProduced * 100 / QuantityPlanned);

    public bool IsOpen => Status is WorkOrderStatus.Planned or WorkOrderStatus.InProgress or WorkOrderStatus.OnHold;

    public bool IsOverdue(DateOnly today) => IsOpen && DueDate < today;
}

public class Material
{
    public int Id { get; set; }
    public string Code { get; set; } = "";
    public string Name { get; set; } = "";
    public string Unit { get; set; } = "";
    public decimal OnHand { get; set; }
    public decimal ReorderLevel { get; set; }
    public decimal UnitCost { get; set; }

    public bool IsLow => OnHand <= ReorderLevel;
}

/// <summary>One line's output for one day; drives the dashboard trend chart.</summary>
public class ProductionRecord
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public int Produced { get; set; }
    public int Scrapped { get; set; }
    public int Target { get; set; }

    public int LineId { get; set; }
    public ProductionLine Line { get; set; } = null!;
}
