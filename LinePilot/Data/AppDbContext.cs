using LinePilot.Models;
using Microsoft.EntityFrameworkCore;

namespace LinePilot.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<ProductionLine> Lines => Set<ProductionLine>();
    public DbSet<Machine> Machines => Set<Machine>();
    public DbSet<DowntimeEvent> DowntimeEvents => Set<DowntimeEvent>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<WorkOrder> WorkOrders => Set<WorkOrder>();
    public DbSet<Material> Materials => Set<Material>();
    public DbSet<ProductionRecord> ProductionRecords => Set<ProductionRecord>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        // Store enums as readable text instead of numbers.
        b.Entity<Machine>().Property(m => m.Status).HasConversion<string>();
        b.Entity<DowntimeEvent>().Property(d => d.Kind).HasConversion<string>();
        b.Entity<WorkOrder>().Property(w => w.Status).HasConversion<string>();
        b.Entity<WorkOrder>().Property(w => w.Priority).HasConversion<string>();

        b.Entity<WorkOrder>().HasIndex(w => w.Number).IsUnique();
        b.Entity<Machine>().HasIndex(m => m.Code).IsUnique();
        b.Entity<Material>().HasIndex(m => m.Code).IsUnique();

        // SQLite has no native decimal type; store as REAL-backed double for sorting/comparison.
        b.Entity<Material>().Property(m => m.OnHand).HasConversion<double>();
        b.Entity<Material>().Property(m => m.ReorderLevel).HasConversion<double>();
        b.Entity<Material>().Property(m => m.UnitCost).HasConversion<double>();
    }
}
