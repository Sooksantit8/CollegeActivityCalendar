using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Models.ViewModels;
using CollegeActivityCalendar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CollegeActivityCalendar.Controllers;

[Authorize(Roles = AppRoles.Admin)]
public class SettingsController(ISettingsService settings, ILookupService lookups) : AppController
{
    public async Task<IActionResult> Index() => View(await BuildAsync(await settings.GetAsync()));

    [HttpPost]
    public async Task<IActionResult> Index(SettingsViewModel model)
    {
        if (ModelState.IsValid)
        {
            var result = await settings.SaveAsync(model.Input);
            if (result.Succeeded) { FlashResult(result); return RedirectToAction(nameof(Index)); }
            ModelState.AddModelError("", result.Message ?? "บันทึกไม่สำเร็จ");
        }
        return View(await BuildAsync(model.Input));
    }

    private async Task<SettingsViewModel> BuildAsync(SettingsInput input) => new()
    {
        Input = input,
        Years = await lookups.AcademicYearsAsync(),
        Views =
        [
            new SelectListItem("รายเดือน", "dayGridMonth"),
            new SelectListItem("รายสัปดาห์", "timeGridWeek"),
            new SelectListItem("รายการ", "listMonth")
        ]
    };
}
