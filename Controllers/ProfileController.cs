using CollegeActivityCalendar.Infrastructure;
using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Models.ViewModels;
using CollegeActivityCalendar.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CollegeActivityCalendar.Controllers;

public class ProfileController(
    IUserManagementService service,
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn) : AppController
{
    public async Task<IActionResult> Index()
    {
        var details = await service.GetDetailsAsync(User.GetUserId());
        if (details is null) return NotFound();
        details.Transfer = null;
        return View(new ProfileViewModel
        {
            Details = details,
            Edit = new ProfileEditInput { FullName = details.FullName, PhoneNumber = details.PhoneNumber }
        });
    }

    [HttpPost]
    public async Task<IActionResult> Update([Bind(Prefix = "Edit")] ProfileEditInput input)
    {
        // นักเรียนแก้ชื่อเองไม่ได้ ต้องให้ผู้ดูแลระบบแก้ จึงไม่อ่านค่าชื่อจากฟอร์มแม้จะถูกส่งมา
        var canEditName = !User.IsInRole(AppRoles.Student);
        if (!canEditName) ModelState.Remove("Edit.FullName");

        if (!ModelState.IsValid)
        {
            TempData[ErrorKey] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "ข้อมูลไม่ถูกต้อง";
            return RedirectToAction(nameof(Index));
        }
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        if (canEditName) user.FullName = input.FullName!.Trim();
        user.PhoneNumber = input.PhoneNumber;
        await users.UpdateAsync(user);
        await signIn.RefreshSignInAsync(user);
        TempData[SuccessKey] = "บันทึกข้อมูลส่วนตัวเรียบร้อยแล้ว";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    public async Task<IActionResult> ChangePassword([Bind(Prefix = "Password")] ChangePasswordInput input)
    {
        if (!ModelState.IsValid)
        {
            TempData[ErrorKey] = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage ?? "ข้อมูลไม่ถูกต้อง";
            return RedirectToAction(nameof(Index));
        }
        var user = await users.GetUserAsync(User);
        if (user is null) return Challenge();
        var result = await users.ChangePasswordAsync(user, input.CurrentPassword!, input.NewPassword!);
        if (!result.Succeeded)
        {
            TempData[ErrorKey] = UserManagementService.IdentityMessage(result);
            return RedirectToAction(nameof(Index));
        }
        await signIn.RefreshSignInAsync(user);
        TempData[SuccessKey] = "เปลี่ยนรหัสผ่านเรียบร้อยแล้ว";
        return RedirectToAction(nameof(Index));
    }
}
