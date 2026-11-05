using CollegeActivityCalendar.Infrastructure;
using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Models.ViewModels;
using CollegeActivityCalendar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CollegeActivityCalendar.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class AcademicStructureController(IAcademicStructureService service, ILookupService lookups) : AppController
{
    public async Task<IActionResult> Index(string tab = "years", int? yearId = null) =>
        View(await service.GetIndexAsync(tab, yearId));

    private IActionResult BackTo(string tab, ServiceResult result)
    {
        FlashResult(result);
        return RedirectToAction(nameof(Index), new { tab });
    }

    // ปีการศึกษา
    [HttpGet]
    public async Task<IActionResult> YearForm(int? id)
    {
        if (id is null) return View(new AcademicYearInput());
        var input = await service.GetYearAsync(id.Value);
        return input is null ? NotFound() : View(input);
    }

    [HttpPost]
    public async Task<IActionResult> YearForm(AcademicYearInput input)
    {
        if (!ModelState.IsValid) return View(input);
        var result = await service.SaveYearAsync(input);
        if (result.Succeeded) return BackTo("years", result);
        ModelState.AddModelError("", result.Message ?? "บันทึกไม่สำเร็จ");
        return View(input);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteYear(int id) => ApiResult(await service.DeleteYearAsync(id));

    // ภาคเรียน
    [HttpGet]
    public async Task<IActionResult> SemesterForm(int? id)
    {
        var input = id is null ? new SemesterInput() : await service.GetSemesterAsync(id.Value);
        if (input is null) return NotFound();
        return View(new SemesterFormViewModel { Input = input, AcademicYears = await lookups.AcademicYearsAsync() });
    }

    [HttpPost]
    public async Task<IActionResult> SemesterForm(SemesterFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await service.SaveSemesterAsync(model.Input);
            if (result.Succeeded) return BackTo("semesters", result);
            ModelState.AddModelError("", result.Message ?? "บันทึกไม่สำเร็จ");
        }
        model.AcademicYears = await lookups.AcademicYearsAsync();
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteSemester(int id) => ApiResult(await service.DeleteSemesterAsync(id));

    // ระดับการศึกษา
    [HttpGet]
    public async Task<IActionResult> LevelForm(int? id)
    {
        if (id is null) return View(new EducationLevelInput());
        var input = await service.GetLevelAsync(id.Value);
        return input is null ? NotFound() : View(input);
    }

    [HttpPost]
    public async Task<IActionResult> LevelForm(EducationLevelInput input)
    {
        if (!ModelState.IsValid) return View(input);
        var result = await service.SaveLevelAsync(input);
        if (result.Succeeded) return BackTo("levels", result);
        ModelState.AddModelError("", result.Message ?? "บันทึกไม่สำเร็จ");
        return View(input);
    }

    [HttpPost]
    public async Task<IActionResult> DeleteLevel(int id) => ApiResult(await service.DeleteLevelAsync(id));

    // ชั้นปี
    [HttpGet]
    public async Task<IActionResult> GradeForm(int? id)
    {
        var input = id is null ? new GradeLevelInput() : await service.GetGradeAsync(id.Value);
        if (input is null) return NotFound();
        return View(await BuildGradeFormAsync(input));
    }

    [HttpPost]
    public async Task<IActionResult> GradeForm(GradeLevelFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await service.SaveGradeAsync(model.Input);
            if (result.Succeeded) return BackTo("grades", result);
            ModelState.AddModelError("", result.Message ?? "บันทึกไม่สำเร็จ");
        }
        return View(await BuildGradeFormAsync(model.Input));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteGrade(int id) => ApiResult(await service.DeleteGradeAsync(id));

    private async Task<GradeLevelFormViewModel> BuildGradeFormAsync(GradeLevelInput input) => new()
    {
        Input = input,
        EducationLevels = await lookups.EducationLevelsAsync(),
        GradeLevels = await lookups.GradeLevelsAsync()
    };

    // ห้องเรียน
    [HttpGet]
    public async Task<IActionResult> ClassroomForm(int? id)
    {
        var input = id is null ? new ClassroomInput() : await service.GetClassroomAsync(id.Value);
        if (input is null) return NotFound();
        return View(await BuildClassroomFormAsync(input));
    }

    [HttpPost]
    public async Task<IActionResult> ClassroomForm(ClassroomFormViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await service.SaveClassroomAsync(model.Input);
            if (result.Succeeded) return BackTo("classrooms", result);
            ModelState.AddModelError("", result.Message ?? "บันทึกไม่สำเร็จ");
        }
        return View(await BuildClassroomFormAsync(model.Input));
    }

    [HttpPost]
    public async Task<IActionResult> DeleteClassroom(int id) => ApiResult(await service.DeleteClassroomAsync(id));

    private async Task<ClassroomFormViewModel> BuildClassroomFormAsync(ClassroomInput input) => new()
    {
        Input = input,
        AcademicYears = await lookups.AcademicYearsAsync(selectCurrent: input.AcademicYearId is null),
        GradeLevels = await lookups.GradeLevelsAsync(),
        Departments = await lookups.DepartmentsAsync(),
        Teachers = await lookups.TeachersAsync()
    };
}
