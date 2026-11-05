using CollegeActivityCalendar.Infrastructure;
using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Models.ViewModels;
using CollegeActivityCalendar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CollegeActivityCalendar.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class StudentPromotionController(IStudentPromotionService service, IAcademicStructureService structure, ILookupService lookups) : AppController
{
    public async Task<IActionResult> Index() => View(await service.GetIndexAsync());

    [HttpPost]
    public async Task<IActionResult> GetStudents([FromBody] PromotionStudentsRequest request)
    {
        if (!ModelState.IsValid) return ApiModelError();
        var result = await service.GetStudentsAsync(request);
        return ApiResult(result, result.Data);
    }

    [HttpGet]
    public async Task<IActionResult> GetClassrooms(int yearId) => Json((await lookups.ClassroomsAsync(yearId)).Select(x => new { id = x.Value, name = x.Text }));

    [HttpPost]
    public async Task<IActionResult> CloneClassrooms(int fromYearId, int toYearId) =>
        ApiResult(await structure.CloneClassroomsAsync(fromYearId, toYearId));

    [HttpPost]
    public async Task<IActionResult> Preview([FromBody] PromotionConfirmRequest request)
    {
        if (!ModelState.IsValid) return ApiModelError();
        var result = await service.PreviewAsync(request);
        return ApiResult(result, result.Data);
    }

    [HttpPost]
    public async Task<IActionResult> Confirm([FromBody] PromotionConfirmRequest request)
    {
        if (!ModelState.IsValid) return ApiModelError();
        var result = await service.ConfirmAsync(request, User.GetUserId());
        return ApiResult(result, result.Data);
    }

    public async Task<IActionResult> History() => View(await service.GetHistoryAsync());

    public async Task<IActionResult> HistoryDetails(int id)
    {
        var vm = await service.GetHistoryDetailsAsync(id);
        return vm is null ? NotFound() : View(vm);
    }
}
