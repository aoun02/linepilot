using LinePilot.Services;
using LinePilot.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LinePilot.Controllers;

public class InventoryController(InventoryService inventory) : Controller
{
    public async Task<IActionResult> Index(bool low = false)
    {
        ViewData["LowOnly"] = low;
        return View(await inventory.ListAsync(low));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(int id, ReceiveStockForm form, bool low = false)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
        }
        else
        {
            try
            {
                var material = await inventory.ReceiveAsync(id, form.Quantity);
                TempData["Success"] = $"Received {form.Quantity:0.##} {material.Unit} of {material.Name}.";
            }
            catch (DomainException ex)
            {
                TempData["Error"] = ex.Message;
            }
        }
        return RedirectToAction(nameof(Index), new { low });
    }
}
