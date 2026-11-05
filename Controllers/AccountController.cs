using CollegeActivityCalendar.Infrastructure;
using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace CollegeActivityCalendar.Controllers;

public class AccountController(
    SignInManager<ApplicationUser> signIn,
    UserManager<ApplicationUser> users,
    IConfiguration config) : AppController
{
    [AllowAnonymous, HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        if (User.Identity?.IsAuthenticated == true) return RedirectLocal(returnUrl, "Index", "Dashboard");
        return View(new LoginViewModel { ReturnUrl = returnUrl, ShowDemoAccounts = config.GetValue("Seed:DemoData", false) });
    }

    [AllowAnonymous, HttpPost]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        model.ShowDemoAccounts = config.GetValue("Seed:DemoData", false);
        if (!ModelState.IsValid) return View(model);

        var user = await users.FindByEmailAsync(model.Email!);
        if (user is { IsActive: false })
        {
            ModelState.AddModelError("", "บัญชีนี้ถูกปิดการใช้งาน กรุณาติดต่อผู้ดูแลระบบ");
            return View(model);
        }
        if (user is not null)
        {
            var result = await signIn.PasswordSignInAsync(user, model.Password!, model.RememberMe, lockoutOnFailure: true);
            if (result.Succeeded) return RedirectLocal(model.ReturnUrl, "Index", "Dashboard");
            if (result.IsLockedOut)
            {
                ModelState.AddModelError("", "บัญชีถูกล็อกชั่วคราวเนื่องจากกรอกรหัสผ่านผิดหลายครั้ง กรุณาลองใหม่ใน 10 นาที");
                return View(model);
            }
        }
        ModelState.AddModelError("", "อีเมลหรือรหัสผ่านไม่ถูกต้อง");
        return View(model);
    }

    [HttpPost]
    public async Task<IActionResult> Logout()
    {
        await signIn.SignOutAsync();
        return RedirectToAction(nameof(Login));
    }

    [AllowAnonymous]
    public IActionResult AccessDenied() => View();
}
