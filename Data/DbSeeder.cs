using CollegeActivityCalendar.Infrastructure;
using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CollegeActivityCalendar.Data;

/// <summary>
/// ข้อมูลตั้งต้น รันซ้ำได้โดยไม่สร้างข้อมูลซ้ำ
/// - ข้อมูลหลัก (Role, ระดับชั้น, สถานะ, ปีการศึกษา ฯลฯ) ใส่เสมอเมื่อยังไม่มี
/// - ข้อมูลตัวอย่าง (บัญชีตัวอย่าง นักเรียน ห้องเรียน กิจกรรม) ใส่เมื่อเปิด Seed:DemoData เท่านั้น
/// </summary>
public class DbSeeder(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    RoleManager<IdentityRole> roles,
    IConfiguration config,
    IClock clock,
    ILogger<DbSeeder> log)
{
    private const string DemoPassword = "College@2569";

    private static readonly (string Code, string Name)[] DepartmentSeed =
    [
        ("AUTO", "ช่างยนต์"), ("EPOW", "ช่างไฟฟ้ากำลัง"), ("ELEC", "ช่างอิเล็กทรอนิกส์"), ("MECH", "ช่างกลโรงงาน"),
        ("WELD", "ช่างเชื่อมโลหะ"), ("CONS", "ช่างก่อสร้าง"), ("IT", "เทคโนโลยีสารสนเทศ"), ("DBIZ", "คอมพิวเตอร์ธุรกิจ"),
        ("ACC", "การบัญชี"), ("MKT", "การตลาด"), ("HTL", "การโรงแรม"), ("TOUR", "การท่องเที่ยว")
    ];

    public async Task SeedAsync()
    {
        await SeedRolesAsync();
        await SeedReferenceDataAsync();
        await SeedAdminAsync();
        if (config.GetValue("Seed:DemoData", false)) await SeedDemoAsync();
    }

    private async Task SeedRolesAsync()
    {
        foreach (var role in AppRoles.All)
            if (!await roles.RoleExistsAsync(role)) await roles.CreateAsync(new IdentityRole(role));
    }

    private async Task SeedReferenceDataAsync()
    {
        if (!await db.StudentStatuses.AnyAsync())
        {
            db.StudentStatuses.AddRange(
                new StudentStatus { SystemCode = StudentStatusCodes.Studying, Name = "กำลังศึกษา", IsEnrolled = true, SortOrder = 1 },
                new StudentStatus { SystemCode = StudentStatusCodes.Suspended, Name = "พักการเรียน", IsEnrolled = true, SortOrder = 2 },
                new StudentStatus { SystemCode = StudentStatusCodes.Transferred, Name = "ย้ายสถานศึกษา", IsEnrolled = false, SortOrder = 3 },
                new StudentStatus { SystemCode = StudentStatusCodes.Graduated, Name = "สำเร็จการศึกษา", IsEnrolled = false, SortOrder = 4 },
                new StudentStatus { SystemCode = StudentStatusCodes.Withdrawn, Name = "ลาออก", IsEnrolled = false, SortOrder = 5 });
        }

        if (!await db.EducationLevels.AnyAsync())
        {
            var pvc = new EducationLevel { Code = "PVC", Name = "ปวช.", SortOrder = 1 };
            var pvs = new EducationLevel { Code = "PVS", Name = "ปวส.", SortOrder = 2 };
            db.EducationLevels.AddRange(pvc, pvs);
            var g1 = new GradeLevel { EducationLevel = pvc, YearNumber = 1, Name = "ปวช.1" };
            var g2 = new GradeLevel { EducationLevel = pvc, YearNumber = 2, Name = "ปวช.2" };
            var g3 = new GradeLevel { EducationLevel = pvc, YearNumber = 3, Name = "ปวช.3", IsFinal = true };
            var h1 = new GradeLevel { EducationLevel = pvs, YearNumber = 1, Name = "ปวส.1" };
            var h2 = new GradeLevel { EducationLevel = pvs, YearNumber = 2, Name = "ปวส.2", IsFinal = true };
            db.GradeLevels.AddRange(g1, g2, g3, h1, h2);
            await db.SaveChangesAsync();
            g1.NextGradeLevelId = g2.Id;
            g2.NextGradeLevelId = g3.Id;
            h1.NextGradeLevelId = h2.Id;
        }

        if (!await db.Departments.AnyAsync())
            db.Departments.AddRange(DepartmentSeed.Select(d => new Department { Code = d.Code, Name = d.Name }));

        if (!await db.AcademicYears.AnyAsync())
        {
            // ปีการศึกษาไทยเริ่มเดือนพฤษภาคม
            var today = clock.Today;
            var gregorian = today.Month >= 5 ? today.Year : today.Year - 1;
            foreach (var offset in new[] { 0, 1 })
            {
                var g = gregorian + offset;
                var be = g + 543;
                db.AcademicYears.Add(new AcademicYear
                {
                    Year = be,
                    Name = $"ปีการศึกษา {be}",
                    StartDate = new DateOnly(g, 5, 16),
                    EndDate = new DateOnly(g + 1, 3, 31),
                    IsCurrent = offset == 0,
                    Semesters =
                    [
                        new Semester { Number = 1, Name = $"ภาคเรียนที่ 1/{be}", StartDate = new DateOnly(g, 5, 16), EndDate = new DateOnly(g, 10, 10) },
                        new Semester { Number = 2, Name = $"ภาคเรียนที่ 2/{be}", StartDate = new DateOnly(g, 11, 1), EndDate = new DateOnly(g + 1, 3, 31) }
                    ]
                });
            }
        }

        if (!await db.ActivityCategories.AnyAsync())
        {
            db.ActivityCategories.AddRange(
                new ActivityCategory { Name = "วิชาการ", Color = "#1f6f8b" },
                new ActivityCategory { Name = "จิตอาสา", Color = "#2f855a" },
                new ActivityCategory { Name = "กีฬาและนันทนาการ", Color = "#c7633e" },
                new ActivityCategory { Name = "ประชุม", Color = "#5a4fcf" },
                new ActivityCategory { Name = "วันสำคัญ", Color = "#b7791f" },
                new ActivityCategory { Name = "แนะแนวและอาชีพ", Color = "#b83280" });
        }

        if (!await db.Locations.AnyAsync())
        {
            db.Locations.AddRange(
                new Location { Name = "หอประชุมใหญ่", Building = "อาคารอำนวยการ", Capacity = 800 },
                new Location { Name = "ลานกิจกรรมกลาง", Capacity = 1500 },
                new Location { Name = "ห้องประชุม A", Building = "อาคาร 1", Capacity = 60 },
                new Location { Name = "ห้องคอมพิวเตอร์ 2", Building = "อาคาร 3", Capacity = 40 },
                new Location { Name = "สนามกีฬา", Capacity = 2000 });
        }

        if (!await db.SystemSettings.AnyAsync())
        {
            db.SystemSettings.AddRange(
                new SystemSetting { Key = "CollegeName", Value = config["Seed:CollegeName"] ?? "วิทยาลัยตัวอย่าง" },
                new SystemSetting { Key = "DefaultCalendarView", Value = "dayGridMonth" },
                new SystemSetting { Key = "UpcomingDays", Value = "30" });
        }
        await db.SaveChangesAsync();
    }

    private async Task SeedAdminAsync()
    {
        var demo = config.GetValue("Seed:DemoData", false);
        var email = config["Seed:AdminEmail"] ?? "admin@college.example";
        var password = config["Seed:AdminPassword"];
        if (string.IsNullOrEmpty(password) && demo) password = DemoPassword;
        if (await users.FindByEmailAsync(email) is not null) return;
        if (string.IsNullOrEmpty(password))
        {
            log.LogWarning("ยังไม่มีบัญชีผู้ดูแลระบบ กรุณากำหนด Seed:AdminPassword แล้วเริ่มระบบใหม่");
            return;
        }
        await CreateUserAsync(email, "ผู้ดูแลระบบ", AppRoles.Admin, password);
    }

    private async Task SeedDemoAsync()
    {
        if (await users.FindByEmailAsync("teacher@college.example") is not null) return;

        // ทั้งชุดสำเร็จหรือไม่มีผลเลย เพื่อให้รันซ้ำได้เมื่อเกิดข้อผิดพลาดกลางทาง
        await using var tx = await db.Database.BeginTransactionAsync();

        var departments = await db.Departments.ToDictionaryAsync(d => d.Code);
        var grades = await db.GradeLevels.OrderBy(g => g.EducationLevelId).ThenBy(g => g.YearNumber).ToListAsync();
        var years = await db.AcademicYears.OrderBy(y => y.Year).ToListAsync();
        var studying = await db.StudentStatuses.FirstAsync(s => s.SystemCode == StudentStatusCodes.Studying);
        var admin = (await users.FindByEmailAsync(config["Seed:AdminEmail"] ?? "admin@college.example"))!;

        // เจ้าหน้าที่และครู
        var staff = await CreateUserAsync("staff@college.example", "สมชาย ใจดี", AppRoles.Staff, DemoPassword);
        db.StaffProfiles.Add(new StaffProfile { UserId = staff.Id, EmployeeCode = "S001", Position = "เจ้าหน้าที่งานกิจกรรม" });

        var teacher = await CreateUserAsync("teacher@college.example", "ครูวิภา ศรีสวัสดิ์", AppRoles.Teacher, DemoPassword);
        var teacherProfile = new TeacherProfile
        {
            UserId = teacher.Id, EmployeeCode = "T001",
            Assignments = [new TeacherAssignment { DepartmentId = departments["IT"].Id }, new TeacherAssignment { DepartmentId = departments["DBIZ"].Id }]
        };
        db.TeacherProfiles.Add(teacherProfile);

        var teacher2 = await CreateUserAsync("teacher2@college.example", "ครูประเสริฐ มั่นคง", AppRoles.Teacher, DemoPassword);
        var teacher2Profile = new TeacherProfile { UserId = teacher2.Id, EmployeeCode = "T002" };
        db.TeacherProfiles.Add(teacher2Profile);
        await db.SaveChangesAsync();

        // ห้องเรียนทั้งปีปัจจุบันและปีถัดไป (ปีถัดไปใช้ทดลองเลื่อนชั้น)
        var roomDepartments = new[] { "IT", "DBIZ", "ACC", "AUTO" };
        foreach (var year in years)
        foreach (var code in roomDepartments)
        foreach (var grade in grades)
        foreach (var room in new[] { 1, 2 })
        {
            db.Classrooms.Add(new Classroom
            {
                AcademicYearId = year.Id, EducationLevelId = grade.EducationLevelId, GradeLevelId = grade.Id,
                DepartmentId = departments[code].Id, RoomNumber = room,
                Name = AcademicStructureService.ClassroomName(grade.Name, code, room)
            });
        }
        await db.SaveChangesAsync();

        var current = years.First(y => y.IsCurrent);
        var rooms = await db.Classrooms.Where(c => c.AcademicYearId == current.Id).ToListAsync();
        Classroom Room(string code, int gradeIndex, int room) => rooms.First(c =>
            c.DepartmentId == departments[code].Id && c.GradeLevelId == grades[gradeIndex].Id && c.RoomNumber == room);

        Room("IT", 0, 1).AdvisorTeacherProfileId = teacherProfile.Id;

        // ตัวอย่างการดูแลแบบแคบกว่าทั้งแผนก: ทุกห้องของ ปวช.1 และเฉพาะห้อง ปวส.1 ACC/1
        var accRoom = Room("ACC", 3, 1);
        db.TeacherAssignments.AddRange(
            new TeacherAssignment { TeacherProfileId = teacher2Profile.Id, DepartmentId = departments["ACC"].Id, GradeLevelId = grades[0].Id },
            new TeacherAssignment { TeacherProfileId = teacher2Profile.Id, DepartmentId = departments["ACC"].Id, GradeLevelId = accRoom.GradeLevelId, ClassroomId = accRoom.Id });

        // นักเรียน
        (string Name, string Dept, int Grade, int Room)[] names =
        [
            ("กมลชนก ใจดี", "IT", 0, 1), ("ธนกฤต แสงทอง", "IT", 0, 1), ("พิมพ์ชนก รุ่งเรือง", "IT", 0, 2), ("ณัฐพล คำมี", "IT", 0, 2),
            ("กิตติพงศ์ รักเรียน", "IT", 1, 1), ("ณิชาภา แก้วใส", "IT", 1, 2), ("วรัญญา พรหมดี", "IT", 1, 1),
            ("ธนวัฒน์ บุญมี", "IT", 2, 1), ("ชญานินท์ สุขใจ", "IT", 2, 2),
            ("พีรพล วัฒนชัย", "IT", 3, 1), ("อรทัย มณีรัตน์", "IT", 3, 2),
            ("ธีรภัทร พิทักษ์", "IT", 4, 1), ("พิมพ์ลภัส นาคทอง", "IT", 4, 2),
            ("ศุภวรรณ มีสุข", "DBIZ", 0, 1), ("วรเมธ พูลผล", "DBIZ", 1, 1),
            ("ชลธิชา ทองดี", "ACC", 0, 1), ("ภาคภูมิ ใจกล้า", "ACC", 1, 1), ("ศิริพร ทองคำ", "ACC", 3, 1),
            ("เอกภพ แสงจันทร์", "AUTO", 0, 1), ("ปกรณ์ เกียรติดำรง", "AUTO", 2, 1)
        ];

        var firstStudent = await CreateUserAsync("student@college.example", "นภา ตั้งใจเรียน", AppRoles.Student, DemoPassword);
        AddStudent(firstStudent, "6900001", Room("IT", 0, 1), departments["IT"].Id, studying.Id, current.Id, admin.Id);

        var index = 2;
        foreach (var (name, dept, grade, room) in names)
        {
            var user = await CreateUserAsync($"student{index:00}@college.example", name, AppRoles.Student, DemoPassword);
            AddStudent(user, $"69{index:00000}", Room(dept, grade, room), departments[dept].Id, studying.Id, current.Id, admin.Id);
            index++;
        }
        await db.SaveChangesAsync();

        await SeedEventsAsync(admin, staff, teacher, firstStudent, departments, Room);
        await tx.CommitAsync();
        log.LogInformation("สร้างข้อมูลตัวอย่างเรียบร้อย");
    }

    private async Task SeedEventsAsync(
        ApplicationUser admin, ApplicationUser staff, ApplicationUser teacher, ApplicationUser student,
        Dictionary<string, Department> departments, Func<string, int, int, Classroom> room)
    {
        var categories = await db.ActivityCategories.ToDictionaryAsync(c => c.Name, c => c.Id);
        var locations = await db.Locations.ToDictionaryAsync(l => l.Name, l => l.Id);
        var today = clock.Today;
        var now = clock.Now;

        Event Make(string title, string description, string category, int startOffset, int endOffset, string? start, string? end,
            string? location, ApplicationUser owner, EventStatus status, params EventTarget[] targets) => new()
        {
            Title = title,
            Description = description,
            ActivityCategoryId = categories[category],
            StartDate = today.AddDays(startOffset),
            EndDate = today.AddDays(endOffset),
            StartTime = start is null ? null : TimeOnly.Parse(start),
            EndTime = end is null ? null : TimeOnly.Parse(end),
            IsAllDay = start is null,
            LocationId = location is null ? null : locations[location],
            IsOnline = location is null,
            ResponsibleUserId = owner.Id,
            CreatedByUserId = owner.Id,
            Status = status,
            PublishAt = status == EventStatus.Published ? now.AddDays(-7) : status == EventStatus.Scheduled ? now.AddDays(3) : null,
            CreatedAt = now,
            UpdatedAt = now,
            Targets = targets.ToList()
        };

        EventTarget College() => new() { TargetType = EventTargetType.College };
        EventTarget Depts(params string[] codes) => new()
        {
            TargetType = EventTargetType.Department,
            Departments = codes.Select(c => new EventTargetDepartment { DepartmentId = departments[c].Id }).ToList()
        };
        EventTarget Rooms(params Classroom[] list) => new()
        {
            TargetType = EventTargetType.Classroom,
            Classrooms = list.Select(c => new EventTargetClassroom { ClassroomId = c.Id }).ToList()
        };
        EventTarget People(params ApplicationUser[] list) => new()
        {
            TargetType = EventTargetType.Individual,
            Users = list.Select(u => new EventTargetUser { UserId = u.Id }).ToList()
        };

        db.Events.AddRange(
            Make("กิจกรรมจิตอาสาพัฒนาวิทยาลัย", "ร่วมพัฒนาพื้นที่วิทยาลัยและชุมชนโดยรอบ แต่งกายชุดกิจกรรม", "จิตอาสา", 0, 0, "08:00", "12:00", "ลานกิจกรรมกลาง", staff, EventStatus.Published, College()),
            Make("ปฐมนิเทศภาคเรียนที่ 2", "ชี้แจงกำหนดการและระเบียบการเรียนสำหรับนักเรียนทุกชั้นปี", "วิชาการ", -4, -4, "09:00", "11:30", "หอประชุมใหญ่", admin, EventStatus.Published, College()),
            Make("สัปดาห์กีฬาสีวิทยาลัย", "การแข่งขันกีฬาสีภายในตลอดสัปดาห์ และพิธีปิดในวันสุดท้าย", "กีฬาและนันทนาการ", 10, 14, null, null, "สนามกีฬา", staff, EventStatus.Published, College()),
            Make("วันพ่อแห่งชาติ", "กิจกรรมเทิดพระเกียรติและวางพานพุ่มร่วมกัน", "วันสำคัญ", 26, 26, "07:30", "09:30", "ลานกิจกรรมกลาง", admin, EventStatus.Published, College()),
            Make("ประชุมครูและบุคลากรประจำเดือน", "ติดตามแผนงานและสรุปกิจกรรมประจำเดือน", "ประชุม", 3, 3, "13:30", "15:30", "ห้องประชุม A", admin, EventStatus.Published, People(staff, teacher)),
            Make("อบรมทักษะดิจิทัลสำหรับแผนก IT", "เวิร์กช็อปเครื่องมือดิจิทัลและการทำงานร่วมกันบนคลาวด์", "วิชาการ", 7, 7, "13:00", "16:00", "ห้องคอมพิวเตอร์ 2", teacher, EventStatus.Published, Depts("IT", "DBIZ")),
            Make("แนะแนวอาชีพสายบัญชี", "พบผู้ประกอบการและเตรียมความพร้อมก่อนฝึกงาน", "แนะแนวและอาชีพ", 15, 15, "10:00", "12:00", "หอประชุมใหญ่", staff, EventStatus.Published, Depts("ACC")),
            Make("พบครูที่ปรึกษา ปวช.1 IT/1", "ติดตามผลการเรียนและวางแผนภาคเรียนที่ 2", "ประชุม", 2, 2, "15:30", "16:30", "ห้องประชุม A", teacher, EventStatus.Published, Rooms(room("IT", 0, 1))),
            Make("สอบเก็บคะแนนออนไลน์ ปวช.2 IT", "สอบผ่านระบบออนไลน์ ลิงก์จะแจ้งในชั้นเรียน", "วิชาการ", 5, 5, "09:00", "10:30", null, teacher, EventStatus.Published, Rooms(room("IT", 1, 1), room("IT", 1, 2))),
            Make("นัดหมายติดตามโครงงานรายบุคคล", "นำความคืบหน้าโครงงานมาปรึกษาครูผู้สอน", "วิชาการ", 1, 1, "14:00", "14:30", "ห้องคอมพิวเตอร์ 2", teacher, EventStatus.Published, People(student)),
            Make("เตรียมงานนิทรรศการวิชาชีพ", "ร่างกำหนดการและรายชื่อผู้รับผิดชอบ ยังไม่เผยแพร่", "วิชาการ", 30, 31, null, null, "หอประชุมใหญ่", staff, EventStatus.Draft, College()),
            Make("ค่ายพัฒนาเว็บไซต์", "ค่ายสองวันสำหรับนักเรียนที่สนใจการพัฒนาเว็บ เปิดรับสมัครเร็ว ๆ นี้", "วิชาการ", 20, 21, "09:00", "16:00", "ห้องคอมพิวเตอร์ 2", teacher, EventStatus.Scheduled, Depts("IT")),
            Make("ทัศนศึกษาสถานประกอบการ", "ยกเลิกเนื่องจากสถานประกอบการไม่สะดวก", "แนะแนวและอาชีพ", 9, 9, "08:00", "16:00", "ลานกิจกรรมกลาง", staff, EventStatus.Cancelled, Depts("AUTO"))
        );
        await db.SaveChangesAsync();
    }

    private void AddStudent(ApplicationUser user, string code, Classroom room, int departmentId, int statusId, int yearId, string adminId)
    {
        var profile = new StudentProfile
        {
            UserId = user.Id, StudentCode = code, DepartmentId = departmentId, ClassroomId = room.Id, GradeLevelId = room.GradeLevelId,
            StudentStatusId = statusId, EnrollDate = clock.Today.AddMonths(-5)
        };
        profile.Histories.Add(new StudentAcademicHistory
        {
            ChangeType = AcademicChangeType.Enrolled, AcademicYearId = yearId, DepartmentId = departmentId, GradeLevelId = room.GradeLevelId,
            ClassroomId = room.Id, StudentStatusId = statusId, CreatedAt = clock.Now, ChangedByUserId = adminId, Note = "ข้อมูลตั้งต้น"
        });
        db.StudentProfiles.Add(profile);
    }

    private async Task<ApplicationUser> CreateUserAsync(string email, string name, string role, string password)
    {
        var user = new ApplicationUser
        {
            UserName = email, Email = email, EmailConfirmed = true, FullName = name, IsActive = true, CreatedAt = clock.Now
        };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException($"สร้างบัญชี {email} ไม่สำเร็จ: {string.Join(", ", result.Errors.Select(e => e.Description))}");
        await users.AddToRoleAsync(user, role);
        return user;
    }
}
