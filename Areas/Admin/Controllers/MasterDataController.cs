using CollegeActivityCalendar.Controllers;
using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Models.ViewModels;
using CollegeActivityCalendar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CollegeActivityCalendar.Areas.Admin.Controllers;

[Area("Admin")]
[Authorize(Roles = AppRoles.Admin)]
public class MasterDataController(IMasterDataService service) : AppController
{
    public async Task<IActionResult> Index(string tab = "locations") => View(await service.GetIndexAsync(tab));

    [HttpGet]
    public async Task<IActionResult> LocationForm(int? id)
    {
        if (id is null) return View(new LocationInput());
        var input = await service.GetLocationAsync(id.Value);
        return input is null ? NotFound() : View(input);
    }

    [HttpPost]
    public async Task<IActionResult> LocationForm(LocationInput input)
    {
        if (!ModelState.IsValid) return View(input);
        var result = await service.SaveLocationAsync(input);
        if (!result.Succeeded) { ModelState.AddModelError("", result.Message ?? "บันทึกไม่สำเร็จ"); return View(input); }
        FlashResult(result);
        return RedirectToAction(nameof(Index), new { tab = "locations" });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteLocation(int id) => ApiResult(await service.DeleteLocationAsync(id));

    [HttpGet]
    public async Task<IActionResult> CategoryForm(int? id)
    {
        if (id is null) return View(new CategoryInput());
        var input = await service.GetCategoryAsync(id.Value);
        return input is null ? NotFound() : View(input);
    }

    [HttpPost]
    public async Task<IActionResult> CategoryForm(CategoryInput input)
    {
        if (!ModelState.IsValid) return View(input);
        var result = await service.SaveCategoryAsync(input);
        if (!result.Succeeded) { ModelState.AddModelError("", result.Message ?? "บันทึกไม่สำเร็จ"); return View(input); }
        FlashResult(result);
        return RedirectToAction(nameof(Index), new { tab = "categories" });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteCategory(int id) => ApiResult(await service.DeleteCategoryAsync(id));

    [HttpGet]
    public async Task<IActionResult> StatusForm(int? id)
    {
        if (id is null) return View(new StudentStatusInput());
        var input = await service.GetStatusAsync(id.Value);
        return input is null ? NotFound() : View(input);
    }

    [HttpPost]
    public async Task<IActionResult> StatusForm(StudentStatusInput input)
    {
        if (!ModelState.IsValid) return View(input);
        var result = await service.SaveStatusAsync(input);
        if (!result.Succeeded) { ModelState.AddModelError("", result.Message ?? "บันทึกไม่สำเร็จ"); return View(input); }
        FlashResult(result);
        return RedirectToAction(nameof(Index), new { tab = "statuses" });
    }

    [HttpPost]
    public async Task<IActionResult> DeleteStatus(int id) => ApiResult(await service.DeleteStatusAsync(id));
}
