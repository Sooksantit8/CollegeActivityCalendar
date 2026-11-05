using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using CollegeActivityCalendar.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;

namespace CollegeActivityCalendar.Infrastructure;

public interface IClock
{
    DateTime Now { get; }
    DateOnly Today { get; }
}

/// <summary>เวลาทั้งระบบอิงเขตเวลาของวิทยาลัย ไม่ขึ้นกับเขตเวลาของเซิร์ฟเวอร์</summary>
public class CollegeClock : IClock
{
    private readonly TimeZoneInfo _zone;
    public CollegeClock(IConfiguration config)
    {
        var id = config["App:TimeZone"] ?? "Asia/Bangkok";
        _zone = TimeZoneInfo.FindSystemTimeZoneById(id);
    }
    public DateTime Now => DateTime.SpecifyKind(TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, _zone), DateTimeKind.Unspecified);
    public DateOnly Today => DateOnly.FromDateTime(Now);
}

/// <summary>ผลลัพธ์จาก Service โดยไม่ใช้ exception ควบคุม flow</summary>
public enum ServiceErrorKind { None, Validation, NotFound, Forbidden }

public class ServiceResult
{
    public bool Succeeded => Kind == ServiceErrorKind.None;
    public ServiceErrorKind Kind { get; init; }
    public string? Message { get; init; }
    public Dictionary<string, string[]>? FieldErrors { get; init; }

    public static ServiceResult Ok(string? message = null) => new() { Message = message };
    public static ServiceResult Invalid(string message, Dictionary<string, string[]>? fields = null) =>
        new() { Kind = ServiceErrorKind.Validation, Message = message, FieldErrors = fields };
    public static ServiceResult NotFound(string message = "ไม่พบข้อมูลที่ต้องการ") =>
        new() { Kind = ServiceErrorKind.NotFound, Message = message };
    public static ServiceResult Forbidden(string message = "คุณไม่มีสิทธิ์ดำเนินการกับข้อมูลนี้") =>
        new() { Kind = ServiceErrorKind.Forbidden, Message = message };
}

public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; init; }
    public static ServiceResult<T> Ok(T data, string? message = null) => new() { Data = data, Message = message };
    public static ServiceResult<T> From(ServiceResult r) =>
        new() { Kind = r.Kind, Message = r.Message, FieldErrors = r.FieldErrors };
}

public class ApiResponse
{
    public bool Success { get; init; }
    public string? Message { get; init; }
    public Dictionary<string, string[]>? Errors { get; init; }
    public object? Data { get; init; }
}

public class TimeOnlyJsonConverter : JsonConverter<TimeOnly>
{
    public override TimeOnly Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
        TimeOnly.TryParse(reader.GetString(), CultureInfo.InvariantCulture, out var t)
            ? t
            : throw new JsonException("รูปแบบเวลาไม่ถูกต้อง");
    public override void Write(Utf8JsonWriter writer, TimeOnly value, JsonSerializerOptions options) =>
        writer.WriteStringValue(value.ToString("HH:mm", CultureInfo.InvariantCulture));
}

/// <summary>แสดงผลวันที่แบบไทย (พ.ศ.) ส่วน culture ของระบบเป็น invariant เพื่อให้ binding รูปแบบ ISO ทำงานถูกต้อง</summary>
public static class ThaiDate
{
    private static readonly string[] MonthsShort =
        ["ม.ค.", "ก.พ.", "มี.ค.", "เม.ย.", "พ.ค.", "มิ.ย.", "ก.ค.", "ส.ค.", "ก.ย.", "ต.ค.", "พ.ย.", "ธ.ค."];
    private static readonly string[] MonthsLong =
        ["มกราคม", "กุมภาพันธ์", "มีนาคม", "เมษายน", "พฤษภาคม", "มิถุนายน", "กรกฎาคม", "สิงหาคม", "กันยายน", "ตุลาคม", "พฤศจิกายน", "ธันวาคม"];

    public static string MonthShort(int month) => MonthsShort[month - 1];
    public static string MonthYear(int year, int month) => $"{MonthsLong[month - 1]} {year + 543}";
    public static string Format(DateOnly d) => $"{d.Day} {MonthsShort[d.Month - 1]} {d.Year + 543}";
    public static string Format(DateOnly? d) => d is null ? "-" : Format(d.Value);
    public static string Format(DateTime d) => $"{Format(DateOnly.FromDateTime(d))} {d:HH:mm} น.";
    public static string Format(DateTime? d) => d is null ? "-" : Format(d.Value);

    public static string Range(DateOnly start, DateOnly end, TimeOnly? startTime, TimeOnly? endTime, bool allDay)
    {
        var date = start == end ? Format(start) : $"{Format(start)} – {Format(end)}";
        if (allDay || startTime is null) return $"{date} · ทั้งวัน";
        return $"{date} · {startTime:HH:mm}–{endTime:HH:mm} น.";
    }
}

public static class DisplayText
{
    public static string Status(EventStatus s) => s switch
    {
        EventStatus.Draft => "แบบร่าง",
        EventStatus.Scheduled => "ตั้งเวลาเผยแพร่",
        EventStatus.Published => "เผยแพร่แล้ว",
        EventStatus.Cancelled => "ยกเลิก",
        _ => s.ToString()
    };
    public static string Level(EventTargetType t) => t switch
    {
        EventTargetType.College => "วิทยาลัย",
        EventTargetType.Department => "แผนก",
        EventTargetType.Classroom => "ห้องเรียน",
        EventTargetType.Individual => "บุคคล",
        _ => t.ToString()
    };
    public static string Role(string? r) => r switch
    {
        AppRoles.Admin => "ผู้ดูแลระบบ",
        AppRoles.Staff => "เจ้าหน้าที่",
        AppRoles.Teacher => "ครู",
        AppRoles.Student => "นักเรียน",
        _ => r ?? "-"
    };
    /// <summary>ข้อความขอบเขตที่ครูดูแล</summary>
    public static string Assignment(string department, string? grade, string? classroom) =>
        classroom is not null ? $"{department} · ห้อง {classroom}"
        : grade is not null ? $"{department} · {grade} ทุกห้อง"
        : $"{department} · ทั้งแผนก";

    public static string Change(AcademicChangeType c) => c switch
    {
        AcademicChangeType.Enrolled => "ขึ้นทะเบียน",
        AcademicChangeType.Promoted => "เลื่อนชั้น",
        AcademicChangeType.Retained => "ซ้ำชั้น",
        AcademicChangeType.ClassroomTransfer => "ย้ายห้อง",
        AcademicChangeType.DepartmentTransfer => "ย้ายแผนก",
        AcademicChangeType.StatusChange => "เปลี่ยนสถานะ",
        AcademicChangeType.Suspended => "พักการเรียน",
        AcademicChangeType.TransferredOut => "ย้ายสถานศึกษา",
        AcademicChangeType.Graduated => "สำเร็จการศึกษา",
        _ => c.ToString()
    };
    public static string Promotion(PromotionAction a) => a switch
    {
        PromotionAction.Promote => "เลื่อนชั้น",
        PromotionAction.Retain => "ซ้ำชั้น",
        PromotionAction.TransferDepartment => "ย้ายแผนก",
        PromotionAction.Suspend => "พักการเรียน",
        PromotionAction.TransferOut => "ย้ายสถานศึกษา",
        PromotionAction.Graduate => "สำเร็จการศึกษา",
        _ => a.ToString()
    };
}

public static class ClaimsExtensions
{
    public const string FullNameClaim = "full_name";
    public static string GetUserId(this ClaimsPrincipal user) => user.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
    public static string GetFullName(this ClaimsPrincipal user) => user.FindFirstValue(FullNameClaim) ?? user.Identity?.Name ?? string.Empty;
    public static string GetPrimaryRole(this ClaimsPrincipal user) =>
        AppRoles.All.FirstOrDefault(user.IsInRole) ?? string.Empty;
    public static bool CanManageEvents(this ClaimsPrincipal user) =>
        user.IsInRole(AppRoles.Admin) || user.IsInRole(AppRoles.Staff) || user.IsInRole(AppRoles.Teacher);
}

public class AppClaimsPrincipalFactory(
    UserManager<ApplicationUser> users, RoleManager<IdentityRole> roles, IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>(users, roles, options)
{
    protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        identity.AddClaim(new Claim(ClaimsExtensions.FullNameClaim, user.FullName));
        return identity;
    }
}
