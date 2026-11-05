using System.ComponentModel.DataAnnotations;

namespace CollegeActivityCalendar.Models.Entities;

public class Department
{
    public int Id { get; set; }
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public ICollection<Classroom> Classrooms { get; set; } = [];
}

public class AcademicYear
{
    public int Id { get; set; }
    /// <summary>ปีการศึกษาแบบ พ.ศ. เช่น 2569</summary>
    public int Year { get; set; }
    [Required, MaxLength(50)] public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsCurrent { get; set; }
    public ICollection<Semester> Semesters { get; set; } = [];
    public ICollection<Classroom> Classrooms { get; set; } = [];
}

public class Semester
{
    public int Id { get; set; }
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public int Number { get; set; }
    [Required, MaxLength(50)] public string Name { get; set; } = string.Empty;
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

public class EducationLevel
{
    public int Id { get; set; }
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public ICollection<GradeLevel> GradeLevels { get; set; } = [];
}

public class GradeLevel
{
    public int Id { get; set; }
    public int EducationLevelId { get; set; }
    public EducationLevel EducationLevel { get; set; } = null!;
    public int YearNumber { get; set; }
    [Required, MaxLength(30)] public string Name { get; set; } = string.Empty;
    /// <summary>ชั้นปีถัดไปตามกติกาการเลื่อนชั้นเริ่มต้น</summary>
    public int? NextGradeLevelId { get; set; }
    public GradeLevel? NextGradeLevel { get; set; }
    /// <summary>ชั้นปีสุดท้าย เมื่อเลื่อนชั้นแล้วถือว่าสำเร็จการศึกษา</summary>
    public bool IsFinal { get; set; }
}

public class Classroom
{
    public int Id { get; set; }
    public int AcademicYearId { get; set; }
    public AcademicYear AcademicYear { get; set; } = null!;
    public int EducationLevelId { get; set; }
    public EducationLevel EducationLevel { get; set; } = null!;
    public int GradeLevelId { get; set; }
    public GradeLevel GradeLevel { get; set; } = null!;
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int RoomNumber { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    public int? AdvisorTeacherProfileId { get; set; }
    public TeacherProfile? AdvisorTeacher { get; set; }
}

public class StudentStatus
{
    public int Id { get; set; }
    /// <summary>รหัสระบบ ใช้โดยการเลื่อนชั้น สถานะที่มีรหัสนี้ลบไม่ได้</summary>
    [MaxLength(30)] public string? SystemCode { get; set; }
    [Required, MaxLength(50)] public string Name { get; set; } = string.Empty;
    /// <summary>ยังคงเป็นนักเรียนที่ศึกษาอยู่</summary>
    public bool IsEnrolled { get; set; }
    public int SortOrder { get; set; }
}

public class StudentProfile
{
    public int Id { get; set; }
    [Required] public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    [Required, MaxLength(20)] public string StudentCode { get; set; } = string.Empty;
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int? GradeLevelId { get; set; }
    public GradeLevel? GradeLevel { get; set; }
    public int? ClassroomId { get; set; }
    public Classroom? Classroom { get; set; }
    public int StudentStatusId { get; set; }
    public StudentStatus StudentStatus { get; set; } = null!;
    public DateOnly EnrollDate { get; set; }
    public ICollection<StudentAcademicHistory> Histories { get; set; } = [];
}

public class TeacherProfile
{
    public int Id { get; set; }
    [Required] public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    [Required, MaxLength(20)] public string EmployeeCode { get; set; } = string.Empty;
    public ICollection<TeacherAssignment> Assignments { get; set; } = [];
    public ICollection<Classroom> AdvisedClassrooms { get; set; } = [];
}

/// <summary>
/// ขอบเขตที่ครูดูแล ครูหนึ่งคนมีได้หลายรายการ
/// ระบุแผนกอย่างเดียว = ทั้งแผนก, แผนก + ชั้นปี = ทุกห้องของชั้นปีนั้น, ระบุห้อง = เฉพาะห้องนั้น
/// </summary>
public class TeacherAssignment
{
    public int Id { get; set; }
    public int TeacherProfileId { get; set; }
    public TeacherProfile TeacherProfile { get; set; } = null!;
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int? GradeLevelId { get; set; }
    public GradeLevel? GradeLevel { get; set; }
    public int? ClassroomId { get; set; }
    public Classroom? Classroom { get; set; }
}

public class StaffProfile
{
    public int Id { get; set; }
    [Required] public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
    [Required, MaxLength(20)] public string EmployeeCode { get; set; } = string.Empty;
    [MaxLength(100)] public string? Position { get; set; }
    public int? DepartmentId { get; set; }
    public Department? Department { get; set; }
}

/// <summary>ประวัติการศึกษาแบบเพิ่มอย่างเดียว ไม่แก้ไขทับรายการเดิม</summary>
public class StudentAcademicHistory
{
    public long Id { get; set; }
    public int StudentProfileId { get; set; }
    public StudentProfile StudentProfile { get; set; } = null!;
    public AcademicChangeType ChangeType { get; set; }
    public int? AcademicYearId { get; set; }
    public AcademicYear? AcademicYear { get; set; }
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
    public int? GradeLevelId { get; set; }
    public GradeLevel? GradeLevel { get; set; }
    public int? ClassroomId { get; set; }
    public Classroom? Classroom { get; set; }
    public int StudentStatusId { get; set; }
    public StudentStatus StudentStatus { get; set; } = null!;
    [MaxLength(500)] public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
    [Required] public string ChangedByUserId { get; set; } = string.Empty;
    public ApplicationUser ChangedByUser { get; set; } = null!;
    public long? PromotionHistoryDetailId { get; set; }
    public PromotionHistoryDetail? PromotionHistoryDetail { get; set; }
}

public class PromotionHistory
{
    public int Id { get; set; }
    public int FromAcademicYearId { get; set; }
    public AcademicYear FromAcademicYear { get; set; } = null!;
    public int ToAcademicYearId { get; set; }
    public AcademicYear ToAcademicYear { get; set; } = null!;
    [Required] public string ExecutedByUserId { get; set; } = string.Empty;
    public ApplicationUser ExecutedByUser { get; set; } = null!;
    public DateTime ExecutedAt { get; set; }
    public int TotalStudents { get; set; }
    [MaxLength(500)] public string? Note { get; set; }
    public ICollection<PromotionHistoryDetail> Details { get; set; } = [];
}

public class PromotionHistoryDetail
{
    public long Id { get; set; }
    public int PromotionHistoryId { get; set; }
    public PromotionHistory PromotionHistory { get; set; } = null!;
    public int StudentProfileId { get; set; }
    public StudentProfile StudentProfile { get; set; } = null!;
    public PromotionAction Action { get; set; }

    public int FromDepartmentId { get; set; }
    public Department FromDepartment { get; set; } = null!;
    public int? FromGradeLevelId { get; set; }
    public GradeLevel? FromGradeLevel { get; set; }
    public int? FromClassroomId { get; set; }
    public Classroom? FromClassroom { get; set; }
    public int FromStudentStatusId { get; set; }
    public StudentStatus FromStudentStatus { get; set; } = null!;

    public int ToDepartmentId { get; set; }
    public Department ToDepartment { get; set; } = null!;
    public int? ToGradeLevelId { get; set; }
    public GradeLevel? ToGradeLevel { get; set; }
    public int? ToClassroomId { get; set; }
    public Classroom? ToClassroom { get; set; }
    public int ToStudentStatusId { get; set; }
    public StudentStatus ToStudentStatus { get; set; } = null!;

    [MaxLength(500)] public string? Note { get; set; }
}
