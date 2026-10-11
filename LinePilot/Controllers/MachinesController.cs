using LinePilot.Services;
using LinePilot.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LinePilot.Controllers;

public class MachinesController(MachineService machines) : Controller
{
    public async Task<IActionResult> Index() => View(await machines.LinesWithMachinesAsync());

    public async Task<IActionResult> Details(int id) => View(await machines.GetAsync(id));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, MachineStatusForm form)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
            return RedirectToAction(nameof(Details), new { id });
        }
        try
        {
            var machine = await machines.ChangeStatusAsync(id, form.Status, form.Reason);
            TempData["Success"] = $"{machine.Code} is now {machine.Status.ToString().ToLower()}.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}
