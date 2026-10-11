using LinePilot.Models;

namespace LinePilot.Data;

/// <summary>
/// Fills the in-memory database with a realistic factory on startup. Dates are relative to "now",
/// so the dashboard always shows fresh data. A fixed random seed keeps every start identical.
/// </summary>
public static class SeedData
{
    public static void Populate(AppDbContext db, DateTime now)
    {
        if (db.Lines.Any()) return;

        var random = new Random(7);
        var today = DateOnly.FromDateTime(now);

        var assembly = new ProductionLine { Code = "L1", Name = "Assembly Line 1", DailyTarget = 1200 };
        var machining = new ProductionLine { Code = "L2", Name = "Machining Line 2", DailyTarget = 800 };
        var packaging = new ProductionLine { Code = "L3", Name = "Packaging Line 3", DailyTarget = 1500 };
        var lines = new[] { assembly, machining, packaging };
        db.Lines.AddRange(lines);

        // ---- machines ----
        Machine M(ProductionLine line, string code, string name, string type, MachineStatus status, int hoursInStatus) => new()
        {
            Line = line,
            Code = code,
            Name = name,
            Type = type,
            Status = status,
            StatusSince = now.AddHours(-hoursInStatus).AddMinutes(-random.Next(60)),
            LastMaintenance = today.AddDays(-random.Next(5, 60)),
            UtilizationToday = status switch
            {
                MachineStatus.Running => random.Next(72, 97),
                MachineStatus.Idle => random.Next(35, 60),
                _ => random.Next(5, 30),
            },
        };

        var machines = new List<Machine>
        {
            M(assembly, "ROB-101", "Welding Robot A", "Robot", MachineStatus.Running, 6),
            M(assembly, "ROB-102", "Welding Robot B", "Robot", MachineStatus.Running, 5),
            M(assembly, "CNV-103", "Main Conveyor", "Conveyor", MachineStatus.Running, 9),
            M(assembly, "PRS-104", "Hydraulic Press", "Press", MachineStatus.Down, 2),
            M(machining, "CNC-201", "CNC Lathe 1", "CNC", MachineStatus.Running, 7),
            M(machining, "CNC-202", "CNC Lathe 2", "CNC", MachineStatus.Idle, 1),
            M(machining, "MIL-203", "5-Axis Mill", "Mill", MachineStatus.Running, 4),
            M(machining, "GRD-204", "Surface Grinder", "Grinder", MachineStatus.Maintenance, 3),
            M(packaging, "PKG-301", "Carton Erector", "Packaging", MachineStatus.Running, 8),
            M(packaging, "PKG-302", "Filler & Capper", "Packaging", MachineStatus.Running, 8),
            M(packaging, "LBL-303", "Labeler", "Labeling", MachineStatus.Idle, 1),
            M(packaging, "PAL-304", "Palletizer Robot", "Robot", MachineStatus.Running, 10),
        };
        db.Machines.AddRange(machines);

        // ---- downtime history (closed events) + the currently open ones ----
        string[] stopReasons = ["Jammed feeder", "Sensor fault", "Material shortage", "Tool change", "Power dip", "Operator break-in", "Quality check hold"];
        for (var i = 0; i < 14; i++)
        {
            var machine = machines[random.Next(machines.Count)];
            var start = now.AddDays(-random.Next(1, 8)).AddHours(-random.Next(0, 12));
            var kind = random.Next(4) == 0 ? MachineStatus.Maintenance : MachineStatus.Down;
            db.DowntimeEvents.Add(new DowntimeEvent
            {
                Machine = machine,
                Kind = kind,
                Reason = kind == MachineStatus.Maintenance ? "Planned preventive maintenance" : stopReasons[random.Next(stopReasons.Length)],
                StartedAt = start,
                EndedAt = start.AddMinutes(random.Next(15, 180)),
            });
        }
        foreach (var m in machines.Where(m => m.Status is MachineStatus.Down or MachineStatus.Maintenance))
        {
            db.DowntimeEvents.Add(new DowntimeEvent
            {
                Machine = m,
                Kind = m.Status,
                Reason = m.Status == MachineStatus.Down ? "Hydraulic pressure loss" : "Spindle bearing replacement",
                StartedAt = m.StatusSince,
            });
        }

        // ---- products & work orders ----
        var products = new[]
        {
            new Product { Sku = "BRK-220", Name = "Brake Caliper" },
            new Product { Sku = "GBX-110", Name = "Gearbox Housing" },
            new Product { Sku = "SHF-075", Name = "Drive Shaft" },
            new Product { Sku = "VLV-310", Name = "Hydraulic Valve" },
            new Product { Sku = "PMP-450", Name = "Water Pump" },
            new Product { Sku = "FLT-600", Name = "Oil Filter Pack" },
        };
        db.Products.AddRange(products);

        WorkOrderStatus[] statusPlan =
        [
            .. Enumerable.Repeat(WorkOrderStatus.Completed, 14),
            .. Enumerable.Repeat(WorkOrderStatus.InProgress, 8),
            .. Enumerable.Repeat(WorkOrderStatus.Planned, 9),
            .. Enumerable.Repeat(WorkOrderStatus.OnHold, 3),
            .. Enumerable.Repeat(WorkOrderStatus.Cancelled, 2),
        ];
        string[] notes = ["Customer: AutoParts GmbH", "Rush order for export batch", "Use new fixture v2", "Quality hold on previous batch released", "", ""];

        for (var i = 0; i < statusPlan.Length; i++)
        {
            var status = statusPlan[i];
            var planned = random.Next(4, 40) * 50;
            var created = now.AddDays(-random.Next(3, 30));
            var order = new WorkOrder
            {
                Number = $"WO-{now:yyMM}-{1001 + i}",
                Product = products[random.Next(products.Length)],
                Line = lines[random.Next(lines.Length)],
                QuantityPlanned = planned,
                Priority = (Priority)random.Next(0, 4),
                Status = status,
                CreatedAt = created,
                Notes = notes[random.Next(notes.Length)] is { Length: > 0 } n ? n : null,
            };

            switch (status)
            {
                case WorkOrderStatus.Completed:
                    order.StartedAt = created.AddDays(1);
                    order.CompletedAt = order.StartedAt.Value.AddDays(random.Next(1, 4));
                    order.QuantityProduced = planned;
                    order.QuantityScrapped = (int)(planned * random.Next(1, 5) / 100.0);
                    order.DueDate = DateOnly.FromDateTime(order.CompletedAt.Value).AddDays(random.Next(-1, 4));
                    break;
                case WorkOrderStatus.InProgress:
                case WorkOrderStatus.OnHold:
                    order.StartedAt = now.AddDays(-random.Next(1, 4));
                    order.QuantityProduced = (int)(planned * random.Next(10, 85) / 100.0);
                    order.QuantityScrapped = (int)(order.QuantityProduced * random.Next(1, 5) / 100.0);
                    order.DueDate = today.AddDays(random.Next(-3, 8)); // a few run late
                    break;
                case WorkOrderStatus.Planned:
                    order.DueDate = today.AddDays(random.Next(2, 21));
                    break;
                default:
                    order.DueDate = today.AddDays(random.Next(-10, 5));
                    break;
            }
            db.WorkOrders.Add(order);
        }

        // ---- materials ----
        db.Materials.AddRange(
            new Material { Code = "RM-STL-02", Name = "Steel sheet 2 mm", Unit = "kg", OnHand = 4250, ReorderLevel = 1500, UnitCost = 1.12m },
            new Material { Code = "RM-ALU-60", Name = "Aluminium billet 6061", Unit = "kg", OnHand = 860, ReorderLevel = 1000, UnitCost = 3.45m },
            new Material { Code = "RM-OIL-46", Name = "Hydraulic oil ISO 46", Unit = "L", OnHand = 320, ReorderLevel = 200, UnitCost = 2.80m },
            new Material { Code = "CP-BLT-M8", Name = "Bolt M8 × 30", Unit = "pcs", OnHand = 18400, ReorderLevel = 5000, UnitCost = 0.06m },
            new Material { Code = "CP-BRG-62", Name = "Bearing 6204-2RS", Unit = "pcs", OnHand = 140, ReorderLevel = 300, UnitCost = 1.95m },
            new Material { Code = "CP-GSK-11", Name = "Rubber gasket 110 mm", Unit = "pcs", OnHand = 2600, ReorderLevel = 1200, UnitCost = 0.22m },
            new Material { Code = "PK-CTN-40", Name = "Carton box 40 × 30", Unit = "pcs", OnHand = 3100, ReorderLevel = 2500, UnitCost = 0.48m },
            new Material { Code = "PK-WRP-50", Name = "Shrink wrap roll", Unit = "rolls", OnHand = 18, ReorderLevel = 25, UnitCost = 14.50m },
            new Material { Code = "PK-LBL-01", Name = "Thermal labels", Unit = "rolls", OnHand = 95, ReorderLevel = 40, UnitCost = 6.20m },
            new Material { Code = "CH-PRM-GR", Name = "Primer paint, grey", Unit = "L", OnHand = 410, ReorderLevel = 150, UnitCost = 5.75m });

        // ---- daily output for the trend chart (13 full days + today so far) ----
        for (var back = 13; back >= 0; back--)
        {
            var date = today.AddDays(-back);
            foreach (var line in lines)
            {
                var factor = back == 0 ? random.Next(52, 70) / 100.0 : random.Next(80, 107) / 100.0;
                var produced = (int)(line.DailyTarget * factor);
                db.ProductionRecords.Add(new ProductionRecord
                {
                    Line = line,
                    Date = date,
                    Target = line.DailyTarget,
                    Produced = produced,
                    Scrapped = (int)(produced * random.Next(8, 45) / 1000.0),
                });
            }
        }

        db.SaveChanges();
    }
}
