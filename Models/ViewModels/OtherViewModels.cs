using System.ComponentModel.DataAnnotations;
using CollegeActivityCalendar.Models.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CollegeActivityCalendar.Models.ViewModels;

public class StatCardViewModel
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-calendar3";
    public string? Hint { get; set; }
    /// <summary>ปลายทางเมื่อคลิกการ์ด ไม่ระบุ = การ์ดคลิกไม่ได้</summary>
    public string? LinkController { get; set; }
    public Dictionary<string, string> LinkValues { get; set; } = [];
}

public class DashboardViewModel
{
    public string HeroTitle { get; set; } = string.Empty;
    public string HeroText { get; set; } = string.Empty;
    public List<StatCardViewModel> Stats { get; set; } = [];
    public List<EventRow> Upcoming { get; set; } = [];
    public List<EventRow> NeedsAttention { get; set; } = [];
    public EventFormOptions? Form { get; set; }
}

// การเลื่อนชั้น
public class PromotionRuleInput
{
    public int GradeLevelId { get; set; }
    public int? NextGradeLevelId { get; set; }
    public bool Graduate { get; set; }
}

public class PromotionStudentsRequest
{
    public int FromYearId { get; set; }
    public int ToYearId { get; set; }
    public int? DepartmentId { get; set; }
    public int? GradeLevelId { get; set; }
    public int? ClassroomId { get; set; }
    public List<PromotionRuleInput> Rules { get; set; } = [];
}

public class PromotionStudentRow
{
    public int StudentProfileId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public string DepartmentName { get; set; } = string.Empty;
    public int? GradeLevelId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public string ClassroomName { get; set; } = string.Empty;
    public string StatusName { get; set; } = string.Empty;
    public PromotionAction SuggestedAction { get; set; }
    public int? SuggestedGradeLevelId { get; set; }
    public int? SuggestedClassroomId { get; set; }
}

public class PromotionStudentsResponse
{
    public List<PromotionStudentRow> Students { get; set; } = [];
    public List<ClassroomOption> TargetClassrooms { get; set; } = [];
}

public class PromotionItemInput
{
    public int StudentProfileId { get; set; }
    public PromotionAction Action { get; set; }
    public int? ToClassroomId { get; set; }
    public string? Note { get; set; }
}

public class PromotionConfirmRequest
{
    public int FromYearId { get; set; }
    public int ToYearId { get; set; }
    public string? Note { get; set; }
    public List<PromotionItemInput> Items { get; set; } = [];
}

public class PromotionPreviewRow
{
    public int StudentProfileId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string ActionText { get; set; } = string.Empty;
    public string FromText { get; set; } = string.Empty;
    public string ToText { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string? Error { get; set; }
}

public class PromotionPreviewResponse
{
    public string FromYear { get; set; } = string.Empty;
    public string ToYear { get; set; } = string.Empty;
    public int Total { get; set; }
    public int ErrorCount { get; set; }
    public List<KeyValuePair<string, int>> Summary { get; set; } = [];
    public List<PromotionPreviewRow> Rows { get; set; } = [];
}

public class PromotionRuleRow
{
    public int GradeLevelId { get; set; }
    public string GradeName { get; set; } = string.Empty;
    public int? NextGradeLevelId { get; set; }
    public bool IsFinal { get; set; }
}

public class PromotionIndexViewModel
{
    public List<SelectListItem> Years { get; set; } = [];
    public int? DefaultFromYearId { get; set; }
    public int? DefaultToYearId { get; set; }
    public List<SelectListItem> Departments { get; set; } = [];
    public List<SelectListItem> GradeLevels { get; set; } = [];
    public List<PromotionRuleRow> Rules { get; set; } = [];
}

public class PromotionHistoryRow
{
    public int Id { get; set; }
    public string FromYear { get; set; } = string.Empty;
    public string ToYear { get; set; } = string.Empty;
    public string ExecutedBy { get; set; } = string.Empty;
    public string ExecutedAtText { get; set; } = string.Empty;
    public int Total { get; set; }
    public string? Note { get; set; }
}

public class PromotionHistoryDetailsViewModel
{
    public PromotionHistoryRow Header { get; set; } = new();
    public List<PromotionPreviewRow> Rows { get; set; } = [];
}

// รายงาน
public class ReportBar
{
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
    public string? Color { get; set; }
}

public class ReportViewModel
{
    [Display(Name = "ตั้งแต่วันที่")] public DateOnly From { get; set; }
    [Display(Name = "ถึงวันที่")] public DateOnly To { get; set; }
    [Display(Name = "แผนก")] public int? DepartmentId { get; set; }
    public List<SelectListItem> Departments { get; set; } = [];
    public List<StatCardViewModel> Stats { get; set; } = [];
    public List<ReportBar> ByMonth { get; set; } = [];
    public List<ReportBar> ByCategory { get; set; } = [];
    public List<ReportBar> ByLevel { get; set; } = [];
    public List<ReportBar> ByStatus { get; set; } = [];
    public List<ReportBar> ByDepartment { get; set; } = [];
    public List<ReportBar> StudentsByDepartment { get; set; } = [];
    public List<PromotionHistoryRow> RecentPromotions { get; set; } = [];
}

public class SettingsInput
{
    [Display(Name = "ชื่อวิทยาลัย"), Required(ErrorMessage = "กรุณากรอกชื่อวิทยาลัย"), StringLength(150, ErrorMessage = "ชื่อต้องไม่เกิน 150 ตัวอักษร")]
    public string? CollegeName { get; set; }
    [Display(Name = "อีเมลติดต่อ"), EmailAddress(ErrorMessage = "รูปแบบอีเมลไม่ถูกต้อง")]
    public string? ContactEmail { get; set; }
    [Display(Name = "เบอร์โทรติดต่อ"), StringLength(30, ErrorMessage = "เบอร์โทรต้องไม่เกิน 30 ตัวอักษร")]
    public string? ContactPhone { get; set; }
    [Display(Name = "มุมมองปฏิทินเริ่มต้น"), Required(ErrorMessage = "กรุณาเลือกมุมมอง")]
    public string? DefaultCalendarView { get; set; } = "dayGridMonth";
    [Display(Name = "จำนวนวันล่วงหน้าที่แสดงในหน้าภาพรวม"), Range(1, 365, ErrorMessage = "ต้องอยู่ระหว่าง 1–365 วัน")]
    public int UpcomingDays { get; set; } = 30;
    [Display(Name = "ปีการศึกษาปัจจุบัน"), Required(ErrorMessage = "กรุณาเลือกปีการศึกษา")]
    public int? CurrentAcademicYearId { get; set; }
}

public class SettingsViewModel
{
    public SettingsInput Input { get; set; } = new();
    public List<SelectListItem> Years { get; set; } = [];
    public List<SelectListItem> Views { get; set; } = [];
}
