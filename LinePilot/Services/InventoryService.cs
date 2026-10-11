using LinePilot.Data;
using LinePilot.Models;
using Microsoft.EntityFrameworkCore;

namespace LinePilot.Services;

public class InventoryService(AppDbContext db)
{
    public async Task<List<Material>> ListAsync(bool lowOnly)
    {
        var materials = await db.Materials.AsNoTracking().OrderBy(m => m.Code).ToListAsync();
        // Low-stock first so planners see what to reorder.
        return materials.Where(m => !lowOnly || m.IsLow).OrderByDescending(m => m.IsLow).ThenBy(m => m.Code).ToList();
    }

    public async Task<Material> ReceiveAsync(int id, decimal quantity)
    {
        if (quantity <= 0) throw new DomainException("Quantity must be greater than zero.");
        var material = await db.Materials.FindAsync(id) ?? throw new NotFoundException($"Material {id} was not found.");
        material.OnHand += quantity;
        await db.SaveChangesAsync();
        return material;
    }
}
