using LinePilot.Models;
using LinePilot.Services;
using LinePilot.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace LinePilot.Controllers;

public class WorkOrdersController(WorkOrderService orders) : Controller
{
    public async Task<IActionResult> Index(WorkOrderStatus? status, int? lineId, string? search, int page = 1) =>
        View(await orders.ListAsync(status, lineId, search, page));

    public async Task<IActionResult> Details(int id) => View(await orders.GetAsync(id));

    public async Task<IActionResult> Create() => View("Form", await orders.BlankFormAsync());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(WorkOrderForm form)
    {
        if (!ModelState.IsValid) return View("Form", await orders.WithOptionsAsync(form));
        try
        {
            var order = await orders.CreateAsync(form);
            TempData["Success"] = $"Work order {order.Number} created.";
            return RedirectToAction(nameof(Details), new { id = order.Id });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Form", await orders.WithOptionsAsync(form));
        }
    }

    public async Task<IActionResult> Edit(int id)
    {
        var order = await orders.GetAsync(id);
        if (!order.IsOpen)
        {
            TempData["Error"] = $"{order.Number} is closed and can no longer be edited.";
            return RedirectToAction(nameof(Details), new { id });
        }
        return View("Form", await orders.FormForAsync(order));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, WorkOrderForm form)
    {
        form.Id = id;
        if (!ModelState.IsValid) return View("Form", await orders.WithOptionsAsync(form));
        try
        {
            await orders.UpdateAsync(id, form);
            TempData["Success"] = "Work order updated.";
            return RedirectToAction(nameof(Details), new { id });
        }
        catch (DomainException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            return View("Form", await orders.WithOptionsAsync(form));
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangeStatus(int id, WorkOrderStatus status)
    {
        try
        {
            var order = await orders.ChangeStatusAsync(id, status);
            TempData["Success"] = $"{order.Number} is now {WorkOrderService.Label(order.Status)}.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RecordOutput(int id, RecordOutputForm form)
    {
        if (!ModelState.IsValid)
        {
            TempData["Error"] = ModelState.Values.SelectMany(v => v.Errors).First().ErrorMessage;
            return RedirectToAction(nameof(Details), new { id });
        }
        try
        {
            var order = await orders.RecordOutputAsync(id, form.Produced, form.Scrapped);
            TempData["Success"] = $"Recorded {form.Produced} good and {form.Scrapped} scrapped units on {order.Number}.";
        }
        catch (DomainException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Details), new { id });
    }
}
