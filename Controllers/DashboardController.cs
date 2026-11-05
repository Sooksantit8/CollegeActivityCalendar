using CollegeActivityCalendar.Services;
using Microsoft.AspNetCore.Mvc;

namespace CollegeActivityCalendar.Controllers;

public class DashboardController(IDashboardService dashboard) : AppController
{
    public async Task<IActionResult> Index() => View(await dashboard.GetAsync());
}
