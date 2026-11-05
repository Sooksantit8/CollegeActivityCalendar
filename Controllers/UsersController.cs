using CollegeActivityCalendar.Infrastructure;
using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Models.ViewModels;
using CollegeActivityCalendar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CollegeActivityCalendar.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class UsersController(IUserManagementService service, ILookupService lookups, IUserImportService import) : AppController
{
    private const long MaxImportBytes = 5 * 1024 * 1024;
    public async Task<IActionResult> Index([FromQuery] UserFilter filter) => View(new UsersIndexViewModel
    {
        List = await service.GetListAsync(filter),
        ActiveRole = filter.Role,
        IsActive = filter.IsActive,
        Departments = await lookups.DepartmentsAsync(activeOnly: false),
        Grades = await lookups.GradeLevelsAsync()
    });

    [HttpGet]
    public async Task<IActionResult> List([FromQuery] UserFilter filter) =>
        PartialView("_UserList", await service.GetListAsync(filter));

    /// <summary>รายการห้องตามแผนกและชั้นปี สำหรับดรอปดาวน์แบบต่อเนื่อง</summary>
    [HttpGet]
    public async Task<IActionResult> GetClassrooms(int? departmentId, int? gradeId, int? includeId) =>
        Json(await service.GetAssignableClassroomsAsync(departmentId, gradeId, includeId));

    [HttpGet]
    public async Task<IActionResult> Create(string? role) =>
        View("Form", await BuildFormAsync(new UserInput { Role = AppRoles.All.Contains(role) ? role : AppRoles.Student }));

    [HttpPost]
    public async Task<IActionResult> Create(UserFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await service.CreateAsync(model.Input);
            if (result.Succeeded) { FlashResult(result); return RedirectToAction(nameof(Index)); }
            ModelState.AddModelError("", result.Message ?? "บันทึกไม่สำเร็จ");
        }
        return View("Form", await BuildFormAsync(model.Input));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(string id)
    {
        var input = await service.GetInputAsync(id);
        return input is null ? NotFound() : View("Form", await BuildFormAsync(input));
    }

    [HttpPost]
    public async Task<IActionResult> Edit(string id, UserFormViewModel model)
    {
        var existing = await service.GetInputAsync(id);
        if (existing is null) return NotFound();
        // บทบาทเปลี่ยนไม่ได้ และรหัสผ่านเปลี่ยนผ่านการตั้งรหัสใหม่เท่านั้น
        model.Input.Id = id;
        model.Input.Role = existing.Role;
        ModelState.Remove("Input.Role");
        if (ModelState.IsValid)
        {
            var result = await service.UpdateAsync(model.Input);
            if (result.Succeeded) { FlashResult(result); return RedirectToAction(nameof(Details), new { id }); }
            ModelState.AddModelError("", result.Message ?? "บันทึกไม่สำเร็จ");
        }
        return View("Form", await BuildFormAsync(model.Input));
    }

    [HttpGet]
    public async Task<IActionResult> Details(string id)
    {
        var vm = await service.GetDetailsAsync(id);
        if (vm is null) return NotFound();
        if (vm.Transfer is not null)
        {
            vm.Departments = await lookups.DepartmentsAsync(activeOnly: false);
            vm.Grades = await lookups.GradeLevelsAsync();
            vm.Statuses = await lookups.StudentStatusesAsync();
        }
        return View(vm);
    }

    /// <summary>ย้ายห้อง ย้ายแผนก หรือเปลี่ยนสถานะ พร้อมบันทึกประวัติ</summary>
    [HttpPost]
    public async Task<IActionResult> Transfer(StudentTransferInput transfer)
    {
        if (!ModelState.IsValid)
        {
            TempData[ErrorKey] = "กรุณาตรวจสอบข้อมูลการย้ายอีกครั้ง";
            return RedirectToAction(nameof(Details), new { id = transfer.UserId });
        }
        FlashResult(await service.TransferStudentAsync(transfer, User.GetUserId()));
        return RedirectToAction(nameof(Details), new { id = transfer.UserId });
    }

    [HttpPost]
    public async Task<IActionResult> SetActive(string id, bool active) =>
        ApiResult(await service.SetActiveAsync(id, active, User.GetUserId()));

    [HttpPost]
    public async Task<IActionResult> ResetPassword(string id, string newPassword)
    {
        if (string.IsNullOrWhiteSpace(newPassword))
        {
            TempData[ErrorKey] = "กรุณากรอกรหัสผ่านใหม่";
            return RedirectToAction(nameof(Details), new { id });
        }
        FlashResult(await service.ResetPasswordAsync(id, newPassword));
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    public async Task<IActionResult> Delete(string id) => ApiResult(await service.DeleteAsync(id, User.GetUserId()));

    // นำเข้าจาก Excel
    [HttpGet]
    public IActionResult Import(string? role) => View(new UserImportPageViewModel
    {
        Role = role == AppRoles.Teacher ? AppRoles.Teacher : AppRoles.Student
    });

    [HttpGet]
    public async Task<IActionResult> ImportTemplate(string role)
    {
        if (role is not (AppRoles.Student or AppRoles.Teacher)) return NotFound();
        var bytes = await import.BuildTemplateAsync(role);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            role == AppRoles.Teacher ? "teacher-import-template.xlsx" : "student-import-template.xlsx");
    }

    [HttpPost, RequestSizeLimit(MaxImportBytes + 64 * 1024)]
    public async Task<IActionResult> ImportPreview(IFormFile? file, string role)
    {
        if (CheckImportFile(file) is { } error) return ApiResult(error);
        await using var stream = file!.OpenReadStream();
        var result = await import.PreviewAsync(stream, role);
        return ApiResult(result, result.Data);
    }

    [HttpPost, RequestSizeLimit(MaxImportBytes + 64 * 1024)]
    public async Task<IActionResult> ImportConfirm(IFormFile? file, string role)
    {
        if (CheckImportFile(file) is { } error) return ApiResult(error);
        await using var stream = file!.OpenReadStream();
        var result = await import.ConfirmAsync(stream, role, User.GetUserId());
        return ApiResult(result, result.Data);
    }

    private static ServiceResult? CheckImportFile(IFormFile? file)
    {
        if (file is null || file.Length == 0) return ServiceResult.Invalid("กรุณาเลือกไฟล์ Excel");
        if (file.Length > MaxImportBytes) return ServiceResult.Invalid("ไฟล์ต้องมีขนาดไม่เกิน 5 MB");
        if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            return ServiceResult.Invalid("รองรับเฉพาะไฟล์ .xlsx");
        return null;
    }

    private async Task<UserFormViewModel> BuildFormAsync(UserInput input) => new()
    {
        Input = input,
        Roles = lookups.Roles(),
        Departments = await lookups.DepartmentsAsync(),
        Grades = await lookups.GradeLevelsAsync(),
        Statuses = await lookups.StudentStatusesAsync()
    };
}
