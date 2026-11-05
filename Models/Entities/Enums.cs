namespace CollegeActivityCalendar.Models.Entities;

public enum EventStatus { Draft = 0, Scheduled = 1, Published = 2, Cancelled = 3 }

/// <summary>ลำดับค่าน้อย = ขอบเขตกว้างกว่า ใช้เลือกสีหลักบนปฏิทิน</summary>
public enum EventTargetType { College = 0, Department = 1, Classroom = 2, Individual = 3 }

public enum AcademicChangeType
{
    Enrolled = 0,
    Promoted = 1,
    Retained = 2,
    ClassroomTransfer = 3,
    DepartmentTransfer = 4,
    StatusChange = 5,
    Suspended = 6,
    TransferredOut = 7,
    Graduated = 8
}

public enum PromotionAction
{
    Promote = 0,
    Retain = 1,
    TransferDepartment = 2,
    Suspend = 3,
    TransferOut = 4,
    Graduate = 5
}

public static class AppRoles
{
    public const string Admin = "Admin";
    public const string Staff = "Staff";
    public const string Teacher = "Teacher";
    public const string Student = "Student";
    public const string EventManagers = "Admin,Staff,Teacher";
    public static readonly string[] All = [Admin, Staff, Teacher, Student];
}

public static class StudentStatusCodes
{
    public const string Studying = "STUDYING";
    public const string Suspended = "SUSPENDED";
    public const string Transferred = "TRANSFERRED";
    public const string Graduated = "GRADUATED";
    public const string Withdrawn = "WITHDRAWN";
}
