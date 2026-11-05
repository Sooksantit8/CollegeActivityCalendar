using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CollegeActivityCalendar.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class ReportsController(IReportService reports) : AppController
{
    public async Task<IActionResult> Index(DateOnly? from, DateOnly? to, int? departmentId) =>
        View(await reports.GetAsync(from, to, departmentId));

    public async Task<IActionResult> ExportCsv(DateOnly? from, DateOnly? to, int? departmentId)
    {
        var bytes = await reports.ExportEventsCsvAsync(from, to, departmentId);
        return File(bytes, "text/csv; charset=utf-8", "college-events.csv");
    }
}
