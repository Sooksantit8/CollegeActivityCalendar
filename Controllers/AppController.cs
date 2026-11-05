using CollegeActivityCalendar.Infrastructure;
using Microsoft.AspNetCore.Mvc;

namespace CollegeActivityCalendar.Controllers;

public abstract class AppController : Controller
{
    protected const string SuccessKey = "SuccessMessage";
    protected const string ErrorKey = "ErrorMessage";

    protected void FlashResult(ServiceResult result)
    {
        TempData[result.Succeeded ? SuccessKey : ErrorKey] = result.Message;
    }

    protected IActionResult ApiResult(ServiceResult result, object? data = null)
    {
        var body = new ApiResponse
        {
            Success = result.Succeeded,
            Message = result.Message,
            Errors = result.FieldErrors,
            Data = data
        };
        return result.Kind switch
        {
            ServiceErrorKind.None => Json(body),
            ServiceErrorKind.NotFound => NotFound(body),
            ServiceErrorKind.Forbidden => StatusCode(StatusCodes.Status403Forbidden, body),
            _ => BadRequest(body)
        };
    }

    protected IActionResult ApiModelError(string message = "กรุณาตรวจสอบข้อมูลอีกครั้ง")
    {
        // JSON ที่แปลงค่าไม่ได้ จะมีข้อความเทคนิคภาษาอังกฤษของ framework จึงไม่ส่งต่อให้ผู้ใช้
        var errors = ModelState
            .Where(x => x.Value?.Errors.Count > 0 && !string.Equals(x.Key, "input", StringComparison.OrdinalIgnoreCase))
            .ToDictionary(
                x => StripPrefix(x.Key),
                x => x.Value!.Errors.Select(e => e.Exception is not null || string.IsNullOrEmpty(e.ErrorMessage) || e.ErrorMessage.Contains("JSON", StringComparison.Ordinal)
                    ? "รูปแบบข้อมูลไม่ถูกต้อง"
                    : e.ErrorMessage).ToArray());
        return BadRequest(new ApiResponse { Success = false, Message = message, Errors = errors });
    }

    /// <summary>JSON ที่ bind ผิดรูปแบบจะมี key นำหน้าด้วย $ หรือชื่อพารามิเตอร์ ตัดออกให้เหลือชื่อฟิลด์</summary>
    private static string StripPrefix(string key)
    {
        var name = key.StartsWith("$.") ? key[2..] : key;
        return name.StartsWith("input.", StringComparison.OrdinalIgnoreCase) ? name[6..] : name;
    }

    /// <summary>ปลายทางของ return URL ต้องอยู่ในระบบเท่านั้น</summary>
    protected IActionResult RedirectLocal(string? url, string fallbackAction, string fallbackController) =>
        !string.IsNullOrEmpty(url) && Url.IsLocalUrl(url) ? LocalRedirect(url) : RedirectToAction(fallbackAction, fallbackController);
}
