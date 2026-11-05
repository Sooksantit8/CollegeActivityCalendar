using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Models.ViewModels;
using CollegeActivityCalendar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CollegeActivityCalendar.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class DepartmentsController(IMasterDataService service, ILookupService lookups) : AppController
{
    public async Task<IActionResult> Index() => View(await service.GetDepartmentsAsync());

    /// <summary>ชั้นปี ห้องเรียน นักเรียน และครูผู้ดูแลของแผนก</summary>
    [HttpGet]
    public async Task<IActionResult> Details(int id, int? yearId)
    {
        var vm = await service.GetDepartmentDetailsAsync(id, yearId);
        if (vm is null) return NotFound();
        vm.Years = await lookups.AcademicYearsAsync();
        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Form(int? id)
    {
        if (id is null) return View(new DepartmentInput());
        var input = await service.GetDepartmentAsync(id.Value);
        return input is null ? NotFound() : View(input);
    }

    [HttpPost]
    public async Task<IActionResult> Form(DepartmentInput input)
    {
        if (!ModelState.IsValid) return View(input);
        var result = await service.SaveDepartmentAsync(input);
        if (result.Succeeded) { FlashResult(result); return RedirectToAction(nameof(Index)); }
        ModelState.AddModelError("", result.Message ?? "บันทึกไม่สำเร็จ");
        return View(input);
    }

    [HttpPost]
    public async Task<IActionResult> Delete(int id) => ApiResult(await service.DeleteDepartmentAsync(id));
}
