using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace CollegeActivityCalendar.Models.ViewModels;

public abstract class DateRangeInput : IValidatableObject
{
    [Display(Name = "วันที่เริ่ม"), DataType(DataType.Date), Required(ErrorMessage = "กรุณาเลือกวันที่เริ่ม")]
    public DateOnly? StartDate { get; set; }
    [Display(Name = "วันที่สิ้นสุด"), DataType(DataType.Date), Required(ErrorMessage = "กรุณาเลือกวันที่สิ้นสุด")]
    public DateOnly? EndDate { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (StartDate > EndDate) yield return new("วันที่เริ่มต้องไม่เกินวันที่สิ้นสุด", [nameof(EndDate)]);
    }
}

public class DepartmentInput
{
    public int? Id { get; set; }
    [Display(Name = "รหัสแผนก"), Required(ErrorMessage = "กรุณากรอกรหัสแผนก"), StringLength(20, ErrorMessage = "รหัสต้องไม่เกิน 20 ตัวอักษร")]
    public string? Code { get; set; }
    [Display(Name = "ชื่อแผนก/สาขา"), Required(ErrorMessage = "กรุณากรอกชื่อแผนก"), StringLength(150, ErrorMessage = "ชื่อต้องไม่เกิน 150 ตัวอักษร")]
    public string? Name { get; set; }
    [Display(Name = "เปิดใช้งาน")] public bool IsActive { get; set; } = true;
}

public class DepartmentRow
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public int ClassroomCount { get; set; }
    public int StudentCount { get; set; }
    public int TeacherCount { get; set; }
    public int EventCount { get; set; }
}

public class DepartmentTeacherTag
{
    public string UserId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    /// <summary>เหตุที่ครูดูแล เช่น ที่ปรึกษา หรือทั้งแผนก</summary>
    public string Scope { get; set; } = string.Empty;
}

public class DepartmentStudentRow
{
    public string UserId { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public class DepartmentRoomView
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<DepartmentTeacherTag> Teachers { get; set; } = [];
    public List<DepartmentStudentRow> Students { get; set; } = [];
}

public class DepartmentGradeView
{
    public string Name { get; set; } = string.Empty;
    public List<DepartmentRoomView> Rooms { get; set; } = [];
    public int StudentCount => Rooms.Sum(r => r.Students.Count);
}

public class DepartmentDetailsViewModel
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int? YearId { get; set; }
    public List<SelectListItem> Years { get; set; } = [];
    public List<StatCardViewModel> Stats { get; set; } = [];
    /// <summary>ครูทั้งหมดของแผนกในปีนี้ พร้อมขอบเขต (หนึ่งแถวต่อหนึ่งคน)</summary>
    public List<DepartmentTeacherTag> Teachers { get; set; } = [];
    public List<DepartmentGradeView> Grades { get; set; } = [];
    /// <summary>นักเรียนที่ยังศึกษาอยู่ในแผนกนี้แต่ยังไม่มีห้อง</summary>
    public List<DepartmentStudentRow> UnassignedStudents { get; set; } = [];
}

public class AcademicYearInput : DateRangeInput
{
    public int? Id { get; set; }
    [Display(Name = "ปีการศึกษา (พ.ศ.)"), Required(ErrorMessage = "กรุณากรอกปีการศึกษา"), Range(2500, 2700, ErrorMessage = "ปีการศึกษาต้องอยู่ระหว่าง 2500–2700")]
    public int? Year { get; set; }
    [Display(Name = "ปีการศึกษาปัจจุบัน")] public bool IsCurrent { get; set; }
}

public class SemesterInput : DateRangeInput
{
    public int? Id { get; set; }
    [Display(Name = "ปีการศึกษา"), Required(ErrorMessage = "กรุณาเลือกปีการศึกษา")]
    public int? AcademicYearId { get; set; }
    [Display(Name = "ภาคเรียนที่"), Required(ErrorMessage = "กรุณากรอกภาคเรียน"), Range(1, 3, ErrorMessage = "ภาคเรียนต้องอยู่ระหว่าง 1–3")]
    public int? Number { get; set; }
}

public class SemesterFormViewModel
{
    public SemesterInput Input { get; set; } = new();
    public List<SelectListItem> AcademicYears { get; set; } = [];
}

public class EducationLevelInput
{
    public int? Id { get; set; }
    [Display(Name = "รหัส"), Required(ErrorMessage = "กรุณากรอกรหัส"), StringLength(20, ErrorMessage = "รหัสต้องไม่เกิน 20 ตัวอักษร")]
    public string? Code { get; set; }
    [Display(Name = "ชื่อระดับการศึกษา"), Required(ErrorMessage = "กรุณากรอกชื่อ"), StringLength(100, ErrorMessage = "ชื่อต้องไม่เกิน 100 ตัวอักษร")]
    public string? Name { get; set; }
    [Display(Name = "ลำดับ"), Range(0, 99, ErrorMessage = "ลำดับต้องอยู่ระหว่าง 0–99")]
    public int SortOrder { get; set; }
}

public class GradeLevelInput : IValidatableObject
{
    public int? Id { get; set; }
    [Display(Name = "ระดับการศึกษา"), Required(ErrorMessage = "กรุณาเลือกระดับการศึกษา")]
    public int? EducationLevelId { get; set; }
    [Display(Name = "ชั้นปีที่"), Required(ErrorMessage = "กรุณากรอกชั้นปี"), Range(1, 9, ErrorMessage = "ชั้นปีต้องอยู่ระหว่าง 1–9")]
    public int? YearNumber { get; set; }
    [Display(Name = "ชื่อชั้นปี"), Required(ErrorMessage = "กรุณากรอกชื่อชั้นปี"), StringLength(30, ErrorMessage = "ชื่อต้องไม่เกิน 30 ตัวอักษร")]
    public string? Name { get; set; }
    [Display(Name = "เลื่อนชั้นไปเป็น")] public int? NextGradeLevelId { get; set; }
    [Display(Name = "ชั้นปีสุดท้าย (เลื่อนแล้วสำเร็จการศึกษา)")] public bool IsFinal { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext context)
    {
        if (IsFinal && NextGradeLevelId is not null)
            yield return new("ชั้นปีสุดท้ายไม่ต้องกำหนดชั้นปีถัดไป", [nameof(NextGradeLevelId)]);
        if (Id is not null && Id == NextGradeLevelId)
            yield return new("ชั้นปีถัดไปต้องไม่ใช่ชั้นปีเดิม", [nameof(NextGradeLevelId)]);
    }
}

public class GradeLevelFormViewModel
{
    public GradeLevelInput Input { get; set; } = new();
    public List<SelectListItem> EducationLevels { get; set; } = [];
    public List<SelectListItem> GradeLevels { get; set; } = [];
}

public class ClassroomInput
{
    public int? Id { get; set; }
    [Display(Name = "ปีการศึกษา"), Required(ErrorMessage = "กรุณาเลือกปีการศึกษา")]
    public int? AcademicYearId { get; set; }
    [Display(Name = "ชั้นปี"), Required(ErrorMessage = "กรุณาเลือกชั้นปี")]
    public int? GradeLevelId { get; set; }
    [Display(Name = "แผนก"), Required(ErrorMessage = "กรุณาเลือกแผนก")]
    public int? DepartmentId { get; set; }
    [Display(Name = "ห้องที่"), Required(ErrorMessage = "กรุณากรอกหมายเลขห้อง"), Range(1, 99, ErrorMessage = "ห้องต้องอยู่ระหว่าง 1–99")]
    public int? RoomNumber { get; set; }
    [Display(Name = "ครูที่ปรึกษา")] public int? AdvisorTeacherProfileId { get; set; }
}

public class ClassroomFormViewModel
{
    public ClassroomInput Input { get; set; } = new();
    public List<SelectListItem> AcademicYears { get; set; } = [];
    public List<SelectListItem> GradeLevels { get; set; } = [];
    public List<SelectListItem> Departments { get; set; } = [];
    public List<SelectListItem> Teachers { get; set; } = [];
}

public class AcademicYearRow { public int Id; public string Name = ""; public string RangeText = ""; public bool IsCurrent; public int ClassroomCount; }
public class SemesterRow { public int Id; public string YearName = ""; public string Name = ""; public string RangeText = ""; }
public class EducationLevelRow { public int Id; public string Code = ""; public string Name = ""; public int SortOrder; public int GradeCount; }
public class GradeLevelRow { public int Id; public string LevelName = ""; public string Name = ""; public int YearNumber; public string NextText = ""; }
public class ClassroomRow { public int Id; public string Name = ""; public string YearName = ""; public string GradeName = ""; public string DepartmentName = ""; public string AdvisorName = ""; public int StudentCount; }

public class AcademicStructureViewModel
{
    public string Tab { get; set; } = "years";
    public List<AcademicYearRow> Years { get; set; } = [];
    public List<SemesterRow> Semesters { get; set; } = [];
    public List<EducationLevelRow> Levels { get; set; } = [];
    public List<GradeLevelRow> Grades { get; set; } = [];
    public List<ClassroomRow> Classrooms { get; set; } = [];
    public int? YearFilter { get; set; }
    public List<SelectListItem> YearOptions { get; set; } = [];
}

// Master data
public class LocationInput
{
    public int? Id { get; set; }
    [Display(Name = "ชื่อสถานที่"), Required(ErrorMessage = "กรุณากรอกชื่อสถานที่"), StringLength(150, ErrorMessage = "ชื่อต้องไม่เกิน 150 ตัวอักษร")]
    public string? Name { get; set; }
    [Display(Name = "อาคาร"), StringLength(100, ErrorMessage = "อาคารต้องไม่เกิน 100 ตัวอักษร")]
    public string? Building { get; set; }
    [Display(Name = "ความจุ (คน)"), Range(0, 100000, ErrorMessage = "ความจุไม่ถูกต้อง")]
    public int? Capacity { get; set; }
    [Display(Name = "เปิดใช้งาน")] public bool IsActive { get; set; } = true;
}

public class CategoryInput
{
    public int? Id { get; set; }
    [Display(Name = "ชื่อประเภทกิจกรรม"), Required(ErrorMessage = "กรุณากรอกชื่อประเภท"), StringLength(100, ErrorMessage = "ชื่อต้องไม่เกิน 100 ตัวอักษร")]
    public string? Name { get; set; }
    [Display(Name = "สีประจำประเภท"), Required(ErrorMessage = "กรุณาเลือกสี"), RegularExpression("^#[0-9a-fA-F]{6}$", ErrorMessage = "รูปแบบสีไม่ถูกต้อง")]
    public string? Color { get; set; } = "#c7633e";
    [Display(Name = "เปิดใช้งาน")] public bool IsActive { get; set; } = true;
}

public class StudentStatusInput
{
    public int? Id { get; set; }
    [Display(Name = "ชื่อสถานะ"), Required(ErrorMessage = "กรุณากรอกชื่อสถานะ"), StringLength(50, ErrorMessage = "ชื่อต้องไม่เกิน 50 ตัวอักษร")]
    public string? Name { get; set; }
    [Display(Name = "ยังศึกษาอยู่")] public bool IsEnrolled { get; set; }
    [Display(Name = "ลำดับ"), Range(0, 99, ErrorMessage = "ลำดับต้องอยู่ระหว่าง 0–99")]
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }
}

public class LocationRow { public int Id; public string Name = ""; public string Building = ""; public int? Capacity; public bool IsActive; }
public class CategoryRow { public int Id; public string Name = ""; public string Color = ""; public bool IsActive; public int EventCount; }
public class StudentStatusRow { public int Id; public string Name = ""; public bool IsEnrolled; public bool IsSystem; public int StudentCount; }

public class MasterDataViewModel
{
    public string Tab { get; set; } = "locations";
    public List<LocationRow> Locations { get; set; } = [];
    public List<CategoryRow> Categories { get; set; } = [];
    public List<StudentStatusRow> Statuses { get; set; } = [];
}
