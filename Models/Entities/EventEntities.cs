using System.ComponentModel.DataAnnotations;

namespace CollegeActivityCalendar.Models.Entities;

public class Location
{
    public int Id { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [MaxLength(100)] public string? Building { get; set; }
    public int? Capacity { get; set; }
    public bool IsActive { get; set; } = true;
}

public class ActivityCategory
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(7)] public string Color { get; set; } = "#c7633e";
    public bool IsActive { get; set; } = true;
}

public class Event
{
    public int Id { get; set; }
    [Required, MaxLength(200)] public string Title { get; set; } = string.Empty;
    [MaxLength(4000)] public string? Description { get; set; }
    public int ActivityCategoryId { get; set; }
    public ActivityCategory ActivityCategory { get; set; } = null!;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public TimeOnly? StartTime { get; set; }
    public TimeOnly? EndTime { get; set; }
    public bool IsAllDay { get; set; }

    public int? LocationId { get; set; }
    public Location? Location { get; set; }
    /// <summary>สถานที่นอกทะเบียน หรือรายละเอียดเพิ่มเติม</summary>
    [MaxLength(200)] public string? LocationText { get; set; }
    public bool IsOnline { get; set; }
    [MaxLength(500)] public string? OnlineUrl { get; set; }

    [Required] public string ResponsibleUserId { get; set; } = string.Empty;
    public ApplicationUser ResponsibleUser { get; set; } = null!;

    public DateTime? PublishAt { get; set; }
    public DateOnly? DisplayUntil { get; set; }
    public EventStatus Status { get; set; }

    [Required] public string CreatedByUserId { get; set; } = string.Empty;
    public ApplicationUser CreatedByUser { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public ICollection<EventTarget> Targets { get; set; } = [];
}

/// <summary>กิจกรรมหนึ่งรายการมีได้ 1 แถวต่อระดับกลุ่มเป้าหมาย และแต่ละแถวเลือกได้หลายรายการ</summary>
public class EventTarget
{
    public int Id { get; set; }
    public int EventId { get; set; }
    public Event Event { get; set; } = null!;
    public EventTargetType TargetType { get; set; }
    public ICollection<EventTargetDepartment> Departments { get; set; } = [];
    public ICollection<EventTargetClassroom> Classrooms { get; set; } = [];
    public ICollection<EventTargetUser> Users { get; set; } = [];
}

public class EventTargetDepartment
{
    public int EventTargetId { get; set; }
    public EventTarget EventTarget { get; set; } = null!;
    public int DepartmentId { get; set; }
    public Department Department { get; set; } = null!;
}

public class EventTargetClassroom
{
    public int EventTargetId { get; set; }
    public EventTarget EventTarget { get; set; } = null!;
    public int ClassroomId { get; set; }
    public Classroom Classroom { get; set; } = null!;
}

public class EventTargetUser
{
    public int EventTargetId { get; set; }
    public EventTarget EventTarget { get; set; } = null!;
    [Required] public string UserId { get; set; } = string.Empty;
    public ApplicationUser User { get; set; } = null!;
}

public class SystemSetting
{
    [Key, MaxLength(100)] public string Key { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Value { get; set; }
}
