using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace CollegeActivityCalendar.Models.Entities;

public class ApplicationUser : IdentityUser
{
    [Required, MaxLength(150)]
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; }

    public StudentProfile? StudentProfile { get; set; }
    public TeacherProfile? TeacherProfile { get; set; }
    public StaffProfile? StaffProfile { get; set; }
}
