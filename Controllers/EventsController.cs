using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Models.ViewModels;
using CollegeActivityCalendar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CollegeActivityCalendar.Controllers;

public class EventsController(IEventService events) : AppController
{
    public async Task<IActionResult> Index([FromQuery] EventFilter filter)
    {
        var canManage = User.IsInRole(AppRoles.Admin) || User.IsInRole(AppRoles.Staff) || User.IsInRole(AppRoles.Teacher);
        var options = await events.GetFilterOptionsAsync(filter);
        options.Current = filter;
        Mark(options.Departments, filter.DepartmentId?.ToString());
        Mark(options.GradeLevels, filter.GradeLevelId?.ToString());
        Mark(options.Classrooms, filter.ClassroomId?.ToString());
        Mark(options.Categories, filter.CategoryId?.ToString());
        Mark(options.Statuses, filter.Status?.ToString());
        return View(new EventsIndexViewModel
        {
            Filters = options,
            Form = canManage ? await events.GetFormOptionsAsync() : null,
            List = await events.GetListAsync(filter)
        });
    }

    private static void Mark(List<Microsoft.AspNetCore.Mvc.Rendering.SelectListItem> items, string? value)
    {
        if (value is null) return;
        foreach (var item in items) item.Selected = item.Value == value;
    }

    /// <summary>คืน Partial ของรายการ สำหรับค้นหาและกรองโดยไม่โหลดหน้าใหม่</summary>
    [HttpGet]
    public async Task<IActionResult> List([FromQuery] EventFilter filter) =>
        PartialView("_EventList", await events.GetListAsync(filter));

    [HttpGet]
    public async Task<IActionResult> GetEvent(int id)
    {
        var result = await events.GetDetailsAsync(id);
        return ApiResult(result, result.Data);
    }

    [HttpGet, Authorize(Roles = AppRoles.EventManagers)]
    public async Task<IActionResult> GetClassrooms(int? yearId, int? levelId, int? gradeId, int? departmentId) =>
        Json(await events.GetTargetClassroomsAsync(yearId, levelId, gradeId, departmentId));

    [HttpGet, Authorize(Roles = AppRoles.EventManagers)]
    public async Task<IActionResult> SearchUsers(string? role, string? q) =>
        Json(await events.SearchTargetUsersAsync(role, q));

    [HttpPost, Authorize(Roles = AppRoles.EventManagers)]
    public async Task<IActionResult> Create([FromBody] EventInput input)
    {
        if (!ModelState.IsValid) return ApiModelError();
        var result = await events.CreateAsync(input);
        return ApiResult(result, result.Data);
    }

    [HttpPost, Authorize(Roles = AppRoles.EventManagers)]
    public async Task<IActionResult> Edit(int id, [FromBody] EventInput input)
    {
        if (!ModelState.IsValid) return ApiModelError();
        var result = await events.UpdateAsync(id, input);
        return ApiResult(result, result.Data);
    }

    [HttpPost, Authorize(Roles = AppRoles.EventManagers)]
    public async Task<IActionResult> Delete(int id) => ApiResult(await events.DeleteAsync(id));

    [HttpPost, Authorize(Roles = AppRoles.EventManagers)]
    public async Task<IActionResult> Publish(int id) => ApiResult(await events.PublishAsync(id));

    [HttpPost, Authorize(Roles = AppRoles.EventManagers)]
    public async Task<IActionResult> Cancel(int id) => ApiResult(await events.CancelAsync(id));
}
