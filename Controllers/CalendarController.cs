using CollegeActivityCalendar.Infrastructure;
using CollegeActivityCalendar.Models.ViewModels;
using CollegeActivityCalendar.Services;
using Microsoft.AspNetCore.Mvc;

namespace CollegeActivityCalendar.Controllers;

public class CalendarController(IEventService events, ISettingsService settings) : AppController
{
    public async Task<IActionResult> Index() => View(new CalendarIndexViewModel
    {
        Filters = await events.GetFilterOptionsAsync(),
        Form = User.CanManageEvents() ? await events.GetFormOptionsAsync() : null,
        DefaultView = (await settings.GetAsync()).DefaultCalendarView ?? "dayGridMonth"
    });

    /// <summary>แหล่งข้อมูลของ FullCalendar — สิทธิ์การมองเห็นบังคับใน EventService</summary>
    [HttpGet]
    public async Task<IActionResult> GetEvents(DateTime start, DateTime end, [FromQuery] EventFilter filter)
    {
        if (!ModelState.IsValid || end <= start) return ApiModelError("ช่วงวันที่ไม่ถูกต้อง");
        var list = await events.GetCalendarEventsAsync(DateOnly.FromDateTime(start), DateOnly.FromDateTime(end), filter);
        return Json(list);
    }
}
