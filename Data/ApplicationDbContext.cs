using CollegeActivityCalendar.Models.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CollegeActivityCalendar.Data;

public class ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
    : IdentityDbContext<ApplicationUser>(options)
{
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<AcademicYear> AcademicYears => Set<AcademicYear>();
    public DbSet<Semester> Semesters => Set<Semester>();
    public DbSet<EducationLevel> EducationLevels => Set<EducationLevel>();
    public DbSet<GradeLevel> GradeLevels => Set<GradeLevel>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<StudentStatus> StudentStatuses => Set<StudentStatus>();
    public DbSet<StudentProfile> StudentProfiles => Set<StudentProfile>();
    public DbSet<TeacherProfile> TeacherProfiles => Set<TeacherProfile>();
    public DbSet<TeacherAssignment> TeacherAssignments => Set<TeacherAssignment>();
    public DbSet<StaffProfile> StaffProfiles => Set<StaffProfile>();
    public DbSet<StudentAcademicHistory> StudentAcademicHistories => Set<StudentAcademicHistory>();
    public DbSet<Location> Locations => Set<Location>();
    public DbSet<ActivityCategory> ActivityCategories => Set<ActivityCategory>();
    public DbSet<Event> Events => Set<Event>();
    public DbSet<EventTarget> EventTargets => Set<EventTarget>();
    public DbSet<EventTargetDepartment> EventTargetDepartments => Set<EventTargetDepartment>();
    public DbSet<EventTargetClassroom> EventTargetClassrooms => Set<EventTargetClassroom>();
    public DbSet<EventTargetUser> EventTargetUsers => Set<EventTargetUser>();
    public DbSet<PromotionHistory> PromotionHistories => Set<PromotionHistory>();
    public DbSet<PromotionHistoryDetail> PromotionHistoryDetails => Set<PromotionHistoryDetail>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        // เรียงลำดับและค้นหาภาษาไทยได้ถูกต้อง
        b.UseCollation("Thai_CI_AS");

        b.Entity<Department>().HasIndex(x => x.Code).IsUnique();
        b.Entity<Department>().HasIndex(x => x.Name).IsUnique();

        b.Entity<AcademicYear>().HasIndex(x => x.Year).IsUnique();
        b.Entity<Semester>().HasIndex(x => new { x.AcademicYearId, x.Number }).IsUnique();
        b.Entity<EducationLevel>().HasIndex(x => x.Code).IsUnique();
        b.Entity<GradeLevel>(e =>
        {
            e.HasIndex(x => new { x.EducationLevelId, x.YearNumber }).IsUnique();
            e.HasOne(x => x.NextGradeLevel).WithMany().HasForeignKey(x => x.NextGradeLevelId);
        });

        b.Entity<Classroom>(e =>
        {
            e.HasIndex(x => new { x.AcademicYearId, x.GradeLevelId, x.DepartmentId, x.RoomNumber }).IsUnique();
            e.HasIndex(x => x.DepartmentId);
            e.HasOne(x => x.AdvisorTeacher).WithMany(x => x.AdvisedClassrooms).HasForeignKey(x => x.AdvisorTeacherProfileId);
        });

        b.Entity<StudentStatus>().HasIndex(x => x.SystemCode).IsUnique().HasFilter("[SystemCode] IS NOT NULL");

        b.Entity<StudentProfile>(e =>
        {
            e.HasIndex(x => x.StudentCode).IsUnique();
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasIndex(x => x.ClassroomId);
            e.HasIndex(x => x.DepartmentId);
            e.HasOne(x => x.User).WithOne(x => x.StudentProfile).HasForeignKey<StudentProfile>(x => x.UserId);
        });
        b.Entity<TeacherProfile>(e =>
        {
            e.HasIndex(x => x.EmployeeCode).IsUnique();
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne(x => x.User).WithOne(x => x.TeacherProfile).HasForeignKey<TeacherProfile>(x => x.UserId);
        });
        b.Entity<TeacherAssignment>(e =>
        {
            // SQL Server นับ NULL เป็นค่าเท่ากันใน unique index จึงกันแถว "ทั้งแผนก" ซ้ำได้โดยไม่ต้องมี filter
            e.HasIndex(x => new { x.TeacherProfileId, x.DepartmentId, x.GradeLevelId, x.ClassroomId }).IsUnique().HasFilter(null);
            e.HasIndex(x => new { x.DepartmentId, x.GradeLevelId });
            e.HasIndex(x => x.ClassroomId);
            e.HasOne(x => x.TeacherProfile).WithMany(x => x.Assignments).HasForeignKey(x => x.TeacherProfileId);
        });
        b.Entity<StaffProfile>(e =>
        {
            e.HasIndex(x => x.EmployeeCode).IsUnique();
            e.HasIndex(x => x.UserId).IsUnique();
            e.HasOne(x => x.User).WithOne(x => x.StaffProfile).HasForeignKey<StaffProfile>(x => x.UserId);
        });

        b.Entity<StudentAcademicHistory>(e =>
        {
            e.HasIndex(x => new { x.StudentProfileId, x.CreatedAt });
            e.HasOne(x => x.StudentProfile).WithMany(x => x.Histories).HasForeignKey(x => x.StudentProfileId);
            e.Property(x => x.ChangeType).HasConversion<string>().HasMaxLength(30);
        });

        b.Entity<ActivityCategory>().HasIndex(x => x.Name).IsUnique();
        b.Entity<Location>().HasIndex(x => x.Name).IsUnique();

        b.Entity<Event>(e =>
        {
            e.HasIndex(x => new { x.StartDate, x.EndDate });
            e.HasIndex(x => new { x.Status, x.PublishAt });
            e.HasIndex(x => x.ResponsibleUserId);
            e.HasIndex(x => x.CreatedByUserId);
            e.HasIndex(x => x.ActivityCategoryId);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.ResponsibleUser).WithMany().HasForeignKey(x => x.ResponsibleUserId);
            e.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId);
        });

        b.Entity<EventTarget>(e =>
        {
            e.HasIndex(x => new { x.EventId, x.TargetType }).IsUnique();
            e.HasIndex(x => x.TargetType);
            e.Property(x => x.TargetType).HasConversion<string>().HasMaxLength(20);
            e.HasOne(x => x.Event).WithMany(x => x.Targets).HasForeignKey(x => x.EventId);
        });
        b.Entity<EventTargetDepartment>(e =>
        {
            e.HasKey(x => new { x.EventTargetId, x.DepartmentId });
            e.HasIndex(x => x.DepartmentId);
            e.HasOne(x => x.EventTarget).WithMany(x => x.Departments).HasForeignKey(x => x.EventTargetId);
        });
        b.Entity<EventTargetClassroom>(e =>
        {
            e.HasKey(x => new { x.EventTargetId, x.ClassroomId });
            e.HasIndex(x => x.ClassroomId);
            e.HasOne(x => x.EventTarget).WithMany(x => x.Classrooms).HasForeignKey(x => x.EventTargetId);
        });
        b.Entity<EventTargetUser>(e =>
        {
            e.HasKey(x => new { x.EventTargetId, x.UserId });
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.EventTarget).WithMany(x => x.Users).HasForeignKey(x => x.EventTargetId);
        });

        b.Entity<PromotionHistory>(e =>
        {
            e.HasIndex(x => x.ExecutedAt);
            e.HasOne(x => x.FromAcademicYear).WithMany().HasForeignKey(x => x.FromAcademicYearId);
            e.HasOne(x => x.ToAcademicYear).WithMany().HasForeignKey(x => x.ToAcademicYearId);
        });
        b.Entity<PromotionHistoryDetail>(e =>
        {
            e.HasIndex(x => x.StudentProfileId);
            e.HasIndex(x => x.PromotionHistoryId);
            e.Property(x => x.Action).HasConversion<string>().HasMaxLength(30);
            e.HasOne(x => x.PromotionHistory).WithMany(x => x.Details).HasForeignKey(x => x.PromotionHistoryId);
            e.HasOne(x => x.FromDepartment).WithMany().HasForeignKey(x => x.FromDepartmentId);
            e.HasOne(x => x.ToDepartment).WithMany().HasForeignKey(x => x.ToDepartmentId);
            e.HasOne(x => x.FromGradeLevel).WithMany().HasForeignKey(x => x.FromGradeLevelId);
            e.HasOne(x => x.ToGradeLevel).WithMany().HasForeignKey(x => x.ToGradeLevelId);
            e.HasOne(x => x.FromClassroom).WithMany().HasForeignKey(x => x.FromClassroomId);
            e.HasOne(x => x.ToClassroom).WithMany().HasForeignKey(x => x.ToClassroomId);
            e.HasOne(x => x.FromStudentStatus).WithMany().HasForeignKey(x => x.FromStudentStatusId);
            e.HasOne(x => x.ToStudentStatus).WithMany().HasForeignKey(x => x.ToStudentStatusId);
        });

        // ป้องกัน multiple cascade path ของ SQL Server: cascade เฉพาะข้อมูลลูกของกิจกรรมและขอบเขตงานของครู
        var cascading = new HashSet<(Type Child, Type Parent)>
        {
            (typeof(EventTarget), typeof(Event)),
            (typeof(EventTargetDepartment), typeof(EventTarget)),
            (typeof(EventTargetClassroom), typeof(EventTarget)),
            (typeof(EventTargetUser), typeof(EventTarget)),
            (typeof(TeacherAssignment), typeof(TeacherProfile)),
            (typeof(PromotionHistoryDetail), typeof(PromotionHistory)),
        };
        foreach (var entity in b.Model.GetEntityTypes())
        {
            var clr = entity.ClrType;
            if (clr.IsGenericType && clr.GetGenericTypeDefinition().Namespace == typeof(IdentityUserRole<>).Namespace) continue;
            foreach (var fk in entity.GetForeignKeys())
            {
                fk.DeleteBehavior = cascading.Contains((clr, fk.PrincipalEntityType.ClrType))
                    ? DeleteBehavior.Cascade
                    : DeleteBehavior.Restrict;
            }
        }
    }
}
