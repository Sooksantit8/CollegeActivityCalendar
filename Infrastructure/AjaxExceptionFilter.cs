using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace CollegeActivityCalendar.Infrastructure;

/// <summary>AJAX ได้รับข้อความข้อผิดพลาดเป็น JSON โดยไม่เปิดเผยรายละเอียดภายในระบบ</summary>
public class AjaxExceptionFilter(ILogger<AjaxExceptionFilter> log) : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.HttpContext.Request.Headers.XRequestedWith != "XMLHttpRequest") return;
        log.LogError(context.Exception, "เกิดข้อผิดพลาดระหว่างประมวลผล AJAX {Path}", context.HttpContext.Request.Path);
        context.Result = new JsonResult(new ApiResponse { Success = false, Message = "เกิดข้อผิดพลาดในระบบ กรุณาลองใหม่อีกครั้ง" })
        {
            StatusCode = StatusCodes.Status500InternalServerError
        };
        context.ExceptionHandled = true;
    }
}
