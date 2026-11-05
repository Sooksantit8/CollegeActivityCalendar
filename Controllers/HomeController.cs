using System.Diagnostics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CollegeActivityCalendar.Controllers;

[AllowAnonymous]
public class HomeController : AppController
{
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        ViewData["RequestId"] = Activity.Current?.Id ?? HttpContext.TraceIdentifier;
        return View();
    }

    [IgnoreAntiforgeryToken]
    public new IActionResult StatusCode(int code)
    {
        ViewData["Code"] = code;
        return View("StatusCode");
    }
}
