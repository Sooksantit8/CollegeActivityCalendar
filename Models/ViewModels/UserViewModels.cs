using System.ComponentModel.DataAnnotations;
using CollegeActivityCalendar.Models.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CollegeActivityCalendar.Models.ViewModels;

public class LoginViewModel
{
    [Display(Name = "อีเมล"), Required(ErrorMessage = "กรุณากรอกอีเมล"), EmailAddress(ErrorMessage = "รูปแบบอีเมลไม่ถูกต้อง")]
    public string? Email { get; set; }
    [Display(Name = "รหัสผ่าน"), Required(ErrorMessage = "กรุณากรอกรหัสผ่าน"), DataType(DataType.Password)]
    public string? Password { get; set; }
    [Display(Name = "จดจำการเข้าสู่ระบบ")] public bool RememberMe { get; set; }
    public string? ReturnUrl { get; set; }
    public bool ShowDemoAccounts { get; set; }
}

public class UserFilter
{
    public string? Q { get; set; }
    public string? Role { get; set; }
    public int? DepartmentId { get; set; }
    public int? GradeLevelId { get; set; }
    public int? ClassroomId { get; set; }
    public bool? IsActive { get; set; }
    public int Page { get; set; } = 1;
}

public class UserRow
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public bool IsActive { get; set; }
}

public class UserListViewModel
{
    public List<UserRow> Items { get; set; } = [];
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int TotalCount { get; set; }
}

public class UsersIndexViewModel
{
    public UserListViewModel List { get; set; } = new();
    public string? ActiveRole { get; set; }
    public bool? IsActive { get; set; }
    public List<SelectListItem> Departments { get; set; } = [];
    public List<SelectListItem> Grades { get; set; } = [];
}

/// <summary>หนึ่งแถวของขอบเขตที่ครูดูแล</summary>
public class TeacherAssignmentInput
{
    public int? DepartmentId { get; set; }
    public int? GradeLevelId { get; set; }
    public int? ClassroomId { get; set; }
}

public class UserInput : IValidatableObject
{
    public string? Id { get; set; }
    [Display(Name = "ชื่อ-นามสกุล"), Required(ErrorMessage = "กรุณากรอกชื่อ-นามสกุล"), StringLength(150, ErrorMessage = "ชื่อต้องไม่เกิน 150 ตัวอักษร")]
    public string? FullName { get; set; }
    [Display(Name = "อีเมล (ใช้เข้าสู่ระบบ)"), Required(ErrorMessage = "กรุณากรอกอีเมล"), EmailAddress(ErrorMessage = "รูปแบบอีเมลไม่ถูกต้อง")]
    public string? Email { get; set; }
    [Display(Name = "เบอร์โทรศัพท์"), StringLength(20, ErrorMessage = "เบอร์โทรต้องไม่เกิน 20 ตัวอักษร")]
    public string? PhoneNumber { get; set; }
    [Display(Name = "บทบาท"), Required(ErrorMessage = "กรุณาเลือกบทบาท")]
    public string? Role { get; set; }
    [Display(Name = "รหัสผ่านเริ่มต้น"), DataType(DataType.Password)]
    public string? Password { get; set; }
    [Display(Name = "เปิดใช้งาน")] public bool IsActive { get; set; } = true;

    [Display(Name = "รหัสนักเรียน / รหัสบุคลากร"), StringLength(20, ErrorMessage = "รหัสต้องไม่เกิน 20 ตัวอักษร")]
    public string? Code { get; set; }

    // นักเรียน
    [Display(Name = "แผนก")] public int? DepartmentId { get; set; }
    [Display(Name = "ชั้นปี")] public int? GradeLevelId { get; set; }
    [Display(Name = "ห้องเรียน")] public int? ClassroomId { get; set; }
    [Display(Name = "สถานะนักเรียน")] public int? StudentStatusId { get; set; }

    // ครู
    public List<TeacherAssignmentInput> Assignments { get; set; } = [];

    // เจ้าหน้าที่
    [Display(Name = "ตำแหน่ง"), StringLength(100, ErrorMessage = "ตำแหน่งต้องไม่เกิน 100 ตัวอักษร")]
    public string? Position { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (Id is null && string.IsNullOrWhiteSpace(Password))
            yield return new("กรุณากำหนดรหัสผ่านเริ่มต้น", [nameof(Password)]);
        if (Role != AppRoles.Admin && string.IsNullOrWhiteSpace(Code))
            yield return new("กรุณากรอกรหัสนักเรียนหรือรหัสบุคลากร", [nameof(Code)]);
        if (Id is null && Role == AppRoles.Student)
        {
            if (DepartmentId is null) yield return new("กรุณาเลือกแผนก", [nameof(DepartmentId)]);
            if (StudentStatusId is null) yield return new("กรุณาเลือกสถานะนักเรียน", [nameof(StudentStatusId)]);
        }
        if (Role == AppRoles.Teacher)
        {
            if (Assignments.Count == 0)
                yield return new("ครูต้องมีขอบเขตที่ดูแลอย่างน้อยหนึ่งรายการ", [nameof(Assignments)]);
            else if (Assignments.Any(a => a.DepartmentId is null))
                yield return new("กรุณาเลือกแผนกให้ครบทุกแถว", [nameof(Assignments)]);
        }
    }
}

public class UserFormViewModel
{
    public UserInput Input { get; set; } = new();
    public bool IsEdit => Input.Id is not null;
    public List<SelectListItem> Roles { get; set; } = [];
    public List<SelectListItem> Departments { get; set; } = [];
    public List<SelectListItem> Grades { get; set; } = [];
    public List<SelectListItem> Statuses { get; set; } = [];
}

public class AcademicHistoryRow
{
    public string ChangeText { get; set; } = string.Empty;
    public string YearName { get; set; } = "-";
    public string DepartmentName { get; set; } = string.Empty;
    public string GradeName { get; set; } = "-";
    public string ClassroomName { get; set; } = "-";
    public string StatusName { get; set; } = string.Empty;
    public string? Note { get; set; }
    public string CreatedAtText { get; set; } = string.Empty;
    public string ChangedByName { get; set; } = string.Empty;
}

public class StudentTransferInput
{
    [Required] public string? UserId { get; set; }
    [Display(Name = "แผนก"), Required(ErrorMessage = "กรุณาเลือกแผนก")] public int? DepartmentId { get; set; }
    [Display(Name = "ชั้นปี")] public int? GradeLevelId { get; set; }
    [Display(Name = "ห้องเรียน")] public int? ClassroomId { get; set; }
    [Display(Name = "สถานะนักเรียน"), Required(ErrorMessage = "กรุณาเลือกสถานะ")] public int? StudentStatusId { get; set; }
    [Display(Name = "หมายเหตุ"), StringLength(500, ErrorMessage = "หมายเหตุต้องไม่เกิน 500 ตัวอักษร")] public string? Note { get; set; }
}

public class UserDetailsViewModel
{
    public string Id { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? PhoneNumber { get; set; }
    public string Role { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? Code { get; set; }
    public string? Position { get; set; }
    public string? DepartmentName { get; set; }
    /// <summary>ครู: ขอบเขตที่ดูแล</summary>
    public List<string> TeacherAssignments { get; set; } = [];
    /// <summary>ครู: ห้องที่เป็นครูที่ปรึกษา</summary>
    public List<string> AdvisedClassrooms { get; set; } = [];
    /// <summary>ครู: จำนวนนักเรียนที่ยังศึกษาอยู่ในขอบเขตที่ดูแล</summary>
    public int? ResponsibleStudentCount { get; set; }
    /// <summary>นักเรียน: ครูที่ดูแล</summary>
    public List<string> ResponsibleTeachers { get; set; } = [];
    public string? GradeName { get; set; }
    public string? ClassroomName { get; set; }
    public string? StatusName { get; set; }
    public string? EnrollDateText { get; set; }
    public List<AcademicHistoryRow> History { get; set; } = [];
    public StudentTransferInput? Transfer { get; set; }
    public List<SelectListItem> Departments { get; set; } = [];
    public List<SelectListItem> Grades { get; set; } = [];
    public List<SelectListItem> Statuses { get; set; } = [];
}

public class ProfileEditInput
{
    [Display(Name = "ชื่อ-นามสกุล"), Required(ErrorMessage = "กรุณากรอกชื่อ-นามสกุล"), StringLength(150, ErrorMessage = "ชื่อต้องไม่เกิน 150 ตัวอักษร")]
    public string? FullName { get; set; }
    [Display(Name = "เบอร์โทรศัพท์"), StringLength(20, ErrorMessage = "เบอร์โทรต้องไม่เกิน 20 ตัวอักษร")]
    public string? PhoneNumber { get; set; }
}

public class ChangePasswordInput
{
    [Display(Name = "รหัสผ่านปัจจุบัน"), Required(ErrorMessage = "กรุณากรอกรหัสผ่านปัจจุบัน"), DataType(DataType.Password)]
    public string? CurrentPassword { get; set; }
    [Display(Name = "รหัสผ่านใหม่"), Required(ErrorMessage = "กรุณากรอกรหัสผ่านใหม่"), DataType(DataType.Password), StringLength(100, MinimumLength = 8, ErrorMessage = "รหัสผ่านต้องมีอย่างน้อย 8 ตัวอักษร")]
    public string? NewPassword { get; set; }
    [Display(Name = "ยืนยันรหัสผ่านใหม่"), Required(ErrorMessage = "กรุณายืนยันรหัสผ่านใหม่"), DataType(DataType.Password), Compare(nameof(NewPassword), ErrorMessage = "รหัสผ่านใหม่ไม่ตรงกัน")]
    public string? ConfirmPassword { get; set; }
}

public class ProfileViewModel
{
    public UserDetailsViewModel Details { get; set; } = new();
    public ProfileEditInput Edit { get; set; } = new();
    public ChangePasswordInput Password { get; set; } = new();
}
