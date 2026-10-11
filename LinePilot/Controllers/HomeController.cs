using System.Diagnostics;
using LinePilot.Models;
using LinePilot.Services;
using Microsoft.AspNetCore.Mvc;

namespace LinePilot.Controllers;

public class HomeController(DashboardService dashboard) : Controller
{
    public async Task<IActionResult> Index() => View(await dashboard.BuildAsync());

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error() =>
        View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
}
