using System.ComponentModel.DataAnnotations;
using CollegeActivityCalendar.Models.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CollegeActivityCalendar.Models.ViewModels;

public class EventTargetInput
{
    public bool College { get; set; }
    public List<int> DepartmentIds { get; set; } = [];
    public List<int> ClassroomIds { get; set; } = [];
    public List<string> UserIds { get; set; } = [];
    public bool IsEmpty => !College && DepartmentIds.Count == 0 && ClassroomIds.Count == 0 && UserIds.Count == 0;
}

public class EventInput : IValidatableObject
{
    public int? Id { get; set; }

    [Display(Name = "ชื่อกิจกรรม")]
    [Required(ErrorMessage = "กรุณากรอกชื่อกิจกรรม")]
    [StringLength(200, ErrorMessage = "ชื่อกิจกรรมต้องไม่เกิน 200 ตัวอักษร")]
    public string? Title { get; set; }

    [Display(Name = "รายละเอียด")]
    [StringLength(4000, ErrorMessage = "รายละเอียดต้องไม่เกิน 4,000 ตัวอักษร")]
    public string? Description { get; set; }

    [Display(Name = "ประเภทกิจกรรม")]
    [Required(ErrorMessage = "กรุณาเลือกประเภทกิจกรรม")]
    public int? ActivityCategoryId { get; set; }

    [Display(Name = "วันที่เริ่ม"), DataType(DataType.Date)]
    [Required(ErrorMessage = "กรุณาเลือกวันที่เริ่ม")]
    public DateOnly? StartDate { get; set; }

    [Display(Name = "วันที่สิ้นสุด"), DataType(DataType.Date)]
    [Required(ErrorMessage = "กรุณาเลือกวันที่สิ้นสุด")]
    public DateOnly? EndDate { get; set; }

    [Display(Name = "เวลาเริ่ม"), DataType(DataType.Time)] public TimeOnly? StartTime { get; set; }
    [Display(Name = "เวลาสิ้นสุด"), DataType(DataType.Time)] public TimeOnly? EndTime { get; set; }
    [Display(Name = "กิจกรรมทั้งวัน")] public bool IsAllDay { get; set; }

    [Display(Name = "สถานที่ในวิทยาลัย")] public int? LocationId { get; set; }

    [Display(Name = "สถานที่อื่น / รายละเอียดสถานที่")]
    [StringLength(200, ErrorMessage = "สถานที่ต้องไม่เกิน 200 ตัวอักษร")]
    public string? LocationText { get; set; }

    [Display(Name = "จัดแบบออนไลน์")] public bool IsOnline { get; set; }

    [Display(Name = "ลิงก์ออนไลน์")]
    [Url(ErrorMessage = "ลิงก์ไม่ถูกต้อง ต้องขึ้นต้นด้วย http:// หรือ https://")]
    [StringLength(500, ErrorMessage = "ลิงก์ต้องไม่เกิน 500 ตัวอักษร")]
    public string? OnlineUrl { get; set; }

    [Display(Name = "ผู้รับผิดชอบ")]
    [Required(ErrorMessage = "กรุณาเลือกผู้รับผิดชอบ")]
    public string? ResponsibleUserId { get; set; }

    [Display(Name = "วันเวลาเผยแพร่")] public DateTime? PublishAt { get; set; }
    [Display(Name = "สิ้นสุดการแสดงผล"), DataType(DataType.Date)] public DateOnly? DisplayUntil { get; set; }

    [Display(Name = "สถานะ")]
    [Required(ErrorMessage = "กรุณาเลือกสถานะ")]
    public EventStatus? Status { get; set; } = EventStatus.Draft;

    public EventTargetInput Targets { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (StartDate is not null && EndDate is not null && StartDate > EndDate)
            yield return new("วันเริ่มต้องไม่เกินวันสิ้นสุด", [nameof(EndDate)]);

        if (!IsAllDay)
        {
            if (StartTime is null) yield return new("กรุณาระบุเวลาเริ่ม", [nameof(StartTime)]);
            if (EndTime is null) yield return new("กรุณาระบุเวลาสิ้นสุด", [nameof(EndTime)]);
            // กิจกรรมหลายวันเวลาสิ้นสุดของวันสุดท้ายอาจน้อยกว่าเวลาเริ่มของวันแรกได้
            if (StartTime is not null && EndTime is not null && StartDate == EndDate && StartTime > EndTime)
                yield return new("เวลาเริ่มต้องไม่เกินเวลาสิ้นสุด", [nameof(EndTime)]);
        }

        if (!IsOnline && LocationId is null && string.IsNullOrWhiteSpace(LocationText))
            yield return new("กรุณาระบุสถานที่ หรือเลือกว่าจัดแบบออนไลน์", [nameof(LocationId)]);

        if (Targets is null || Targets.IsEmpty)
            yield return new("กรุณาเลือกกลุ่มเป้าหมายอย่างน้อยหนึ่งรายการ", [nameof(Targets)]);

        if (Status == EventStatus.Scheduled && PublishAt is null)
            yield return new("สถานะตั้งเวลาเผยแพร่ต้องระบุวันเวลาเผยแพร่", [nameof(PublishAt)]);

        if (DisplayUntil is not null && PublishAt is not null && DisplayUntil < DateOnly.FromDateTime(PublishAt.Value))
            yield return new("วันที่สิ้นสุดการแสดงผลต้องไม่ก่อนวันเผยแพร่", [nameof(DisplayUntil)]);
    }
}

public class EventFormOptions
{
    public EventInput Input { get; set; } = new();
    public List<SelectListItem> Categories { get; set; } = [];
    public List<SelectListItem> Locations { get; set; } = [];
    public List<SelectListItem> ResponsibleUsers { get; set; } = [];
    public List<SelectListItem> Statuses { get; set; } = [];
    /// <summary>แผนกที่เลือกเป็นเป้าหมายทั้งแผนกได้</summary>
    public List<SelectListItem> Departments { get; set; } = [];
    /// <summary>แผนกสำหรับกรองรายการห้องเรียน</summary>
    public List<SelectListItem> RoomDepartments { get; set; } = [];
    public List<SelectListItem> AcademicYears { get; set; } = [];
    public List<SelectListItem> EducationLevels { get; set; } = [];
    public List<SelectListItem> GradeLevels { get; set; } = [];
    public bool CanTargetCollege { get; set; }
    public string CurrentUserId { get; set; } = string.Empty;
}

public class EventFilter
{
    public string? Q { get; set; }
    public int? DepartmentId { get; set; }
    public int? GradeLevelId { get; set; }
    public int? ClassroomId { get; set; }
    public int? CategoryId { get; set; }
    public EventStatus? Status { get; set; }
    public EventTargetType? Level { get; set; }
    public DateOnly? From { get; set; }
    public DateOnly? To { get; set; }
    /// <summary>เริ่มหลังวันนี้ ไม่นับกิจกรรมที่เริ่มไปแล้ว</summary>
    public DateOnly? StartsAfter { get; set; }
    /// <summary>ไม่รวมกิจกรรมที่ยกเลิก</summary>
    public bool ActiveOnly { get; set; }
    /// <summary>เฉพาะกิจกรรมที่ผู้ใช้จัดการได้</summary>
    public bool ManageableOnly { get; set; }
    /// <summary>เริ่มตั้งแต่วันนี้เป็นต้นไป ต่างจาก From ที่นับกิจกรรมที่คาบเกี่ยวช่วงเวลา</summary>
    public DateOnly? StartFrom { get; set; }
    public DateOnly? StartTo { get; set; }
    /// <summary>กำหนดเป้าหมายเป็นแผนกนี้โดยตรง ไม่นับกิจกรรมที่เลือกเป็นรายห้อง</summary>
    public int? TargetDepartmentId { get; set; }
    /// <summary>เฉพาะแบบร่างและที่ตั้งเวลาเผยแพร่</summary>
    public bool PendingOnly { get; set; }
    /// <summary>ชื่อการ์ดในหน้าภาพรวมที่พามาหน้านี้ ใช้แสดงข้อความเท่านั้น</summary>
    public string? Preset { get; set; }
    public int Page { get; set; } = 1;
}

public class EventFilterOptions
{
    public List<SelectListItem> Departments { get; set; } = [];
    public List<SelectListItem> GradeLevels { get; set; } = [];
    public List<SelectListItem> Classrooms { get; set; } = [];
    public List<SelectListItem> Categories { get; set; } = [];
    public List<SelectListItem> Statuses { get; set; } = [];
    public bool ShowStatus { get; set; }
    /// <summary>ค่าที่เลือกไว้ตอนเปิดหน้า</summary>
    public EventFilter Current { get; set; } = new();
}

public class EventRow
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryColor { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public string DateText { get; set; } = string.Empty;
    public string LocationText { get; set; } = string.Empty;
    public string ResponsibleName { get; set; } = string.Empty;
    public EventStatus Status { get; set; }
    public List<EventTargetType> Levels { get; set; } = [];
    public bool CanManage { get; set; }
}

public class EventListViewModel
{
    public List<EventRow> Items { get; set; } = [];
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

public class EventsIndexViewModel
{
    public EventFilterOptions Filters { get; set; } = new();
    public EventFormOptions? Form { get; set; }
    public EventListViewModel List { get; set; } = new();
}

public class CalendarIndexViewModel
{
    public EventFilterOptions Filters { get; set; } = new();
    public EventFormOptions? Form { get; set; }
    public string DefaultView { get; set; } = "dayGridMonth";
}

public record IdName(string Id, string Name);

public class EventTargetsDto
{
    public bool College { get; set; }
    public List<IdName> Departments { get; set; } = [];
    public List<IdName> Classrooms { get; set; } = [];
    public List<IdName> Users { get; set; } = [];
    /// <summary>จำนวนบุคคลเป้าหมาย สำหรับผู้ไม่มีสิทธิ์เห็นรายชื่อ</summary>
    public int UserCount { get; set; }
}

public class EventDetailsDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int ActivityCategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string CategoryColor { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public bool IsAllDay { get; set; }
    public string DateText { get; set; } = string.Empty;
    public int? LocationId { get; set; }
    public string? LocationText { get; set; }
    public string LocationDisplay { get; set; } = string.Empty;
    public bool IsOnline { get; set; }
    public string? OnlineUrl { get; set; }
    public string? ResponsibleUserId { get; set; }
    public string ResponsibleName { get; set; } = string.Empty;
    public DateTime? PublishAt { get; set; }
    public string PublishAtText { get; set; } = string.Empty;
    public DateOnly? DisplayUntil { get; set; }
    public string DisplayUntilText { get; set; } = string.Empty;
    public EventStatus Status { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public List<string> Levels { get; set; } = [];
    public EventTargetsDto Targets { get; set; } = new();
    public bool CanManage { get; set; }
}

public class CalendarEventDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Start { get; set; } = string.Empty;
    public string End { get; set; } = string.Empty;
    public bool AllDay { get; set; }
    public string ClassName { get; set; } = string.Empty;
    public string Level { get; set; } = string.Empty;
    public string LevelText { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string StatusText { get; set; } = string.Empty;
    public string CategoryName { get; set; } = string.Empty;
    public string LocationText { get; set; } = string.Empty;
}

public class ClassroomOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string GradeName { get; set; } = string.Empty;
    public string DepartmentName { get; set; } = string.Empty;
    public int GradeLevelId { get; set; }
    public int DepartmentId { get; set; }
    public int RoomNumber { get; set; }
}

public class TargetUserSearch
{
    public string? Role { get; set; }
    public string? Q { get; set; }
    public int? DepartmentId { get; set; }
    public int? GradeLevelId { get; set; }
    public int? ClassroomId { get; set; }
    public int Page { get; set; } = 1;
}

public class TargetUserPage
{
    public List<UserOption> Items { get; set; } = [];
    public int Total { get; set; }
    public int Page { get; set; }
    public bool HasMore { get; set; }
    /// <summary>จำนวนสูงสุดที่กดเลือกทั้งหมดได้ในครั้งเดียว</summary>
    public int SelectAllLimit { get; set; }
}

public class UserOption
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string RoleText { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
}
