using System.ComponentModel.DataAnnotations;
using LinePilot.Models;

namespace LinePilot.ViewModels;

public record DayOutput(DateOnly Date, int Produced, int Target);

public record LineOutput(string Line, int Produced, int Target)
{
    public int Percent => Target == 0 ? 0 : Produced * 100 / Target;
}

public class DashboardViewModel
{
    public DateTime Now { get; init; }
    public DateOnly Today { get; init; }
    public int TodayProduced { get; init; }
    public int TodayTarget { get; init; }
    public int AttainmentPercent => TodayTarget == 0 ? 0 : TodayProduced * 100 / TodayTarget;
    public double ScrapRatePercent { get; init; }
    public int MachinesRunning { get; init; }
    public int MachinesTotal { get; init; }
    public int OpenOrders { get; init; }
    public int OverdueOrders { get; init; }
    public List<DayOutput> Trend { get; init; } = [];
    public List<LineOutput> LinesToday { get; init; } = [];
    public List<Machine> Machines { get; init; } = [];
    public List<DowntimeEvent> ActiveDowntime { get; init; } = [];
    public List<WorkOrder> PriorityOrders { get; init; } = [];
}

public class WorkOrderListViewModel
{
    public List<WorkOrder> Items { get; init; } = [];
    public WorkOrderStatus? Status { get; init; }
    public int? LineId { get; init; }
    public string? Search { get; init; }
    public int Page { get; init; }
    public int TotalPages { get; init; }
    public int TotalCount { get; init; }
    public Dictionary<WorkOrderStatus, int> StatusCounts { get; init; } = [];
    public List<ProductionLine> Lines { get; init; } = [];
    public DateOnly Today { get; init; }
}

/// <summary>Create / edit form. DataAnnotations drive both server-side and client-side validation.</summary>
public class WorkOrderForm
{
    public int? Id { get; set; }
    public string? Number { get; set; }

    [Display(Name = "Product")]
    [Range(1, int.MaxValue, ErrorMessage = "Choose a product.")]
    public int ProductId { get; set; }

    [Display(Name = "Production line")]
    [Range(1, int.MaxValue, ErrorMessage = "Choose a production line.")]
    public int LineId { get; set; }

    [Display(Name = "Planned quantity")]
    [Range(10, 100_000, ErrorMessage = "Planned quantity must be between 10 and 100,000.")]
    public int QuantityPlanned { get; set; }

    public Priority Priority { get; set; }

    [Display(Name = "Due date")]
    [DataType(DataType.Date)]
    public DateOnly DueDate { get; set; }

    [StringLength(200, ErrorMessage = "Notes can be at most 200 characters.")]
    public string? Notes { get; set; }

    // Dropdown options (not posted back)
    public List<Product> Products { get; set; } = [];
    public List<ProductionLine> Lines { get; set; } = [];
}

public class RecordOutputForm
{
    [Display(Name = "Good units")]
    [Range(0, 100_000, ErrorMessage = "Enter 0 or more good units.")]
    public int Produced { get; set; }

    [Display(Name = "Scrapped units")]
    [Range(0, 100_000, ErrorMessage = "Enter 0 or more scrapped units.")]
    public int Scrapped { get; set; }
}

public class MachineStatusForm
{
    public MachineStatus Status { get; set; }

    [StringLength(120, ErrorMessage = "Reason can be at most 120 characters.")]
    public string? Reason { get; set; }
}

public class ReceiveStockForm
{
    [Range(typeof(decimal), "0.01", "1000000", ErrorMessage = "Enter a quantity greater than zero.")]
    public decimal Quantity { get; set; }
}
