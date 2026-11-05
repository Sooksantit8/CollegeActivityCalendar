using CollegeActivityCalendar.Data;
using CollegeActivityCalendar.Infrastructure;
using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Models.ViewModels;
using CollegeActivityCalendar.Repositories;
using Microsoft.EntityFrameworkCore;

namespace CollegeActivityCalendar.Services;

public interface IAcademicStructureService
{
    Task<AcademicStructureViewModel> GetIndexAsync(string tab, int? yearFilter);
    Task<AcademicYearInput?> GetYearAsync(int id);
    Task<ServiceResult> SaveYearAsync(AcademicYearInput i);
    Task<ServiceResult> DeleteYearAsync(int id);
    Task<SemesterInput?> GetSemesterAsync(int id);
    Task<ServiceResult> SaveSemesterAsync(SemesterInput i);
    Task<ServiceResult> DeleteSemesterAsync(int id);
    Task<EducationLevelInput?> GetLevelAsync(int id);
    Task<ServiceResult> SaveLevelAsync(EducationLevelInput i);
    Task<ServiceResult> DeleteLevelAsync(int id);
    Task<GradeLevelInput?> GetGradeAsync(int id);
    Task<ServiceResult> SaveGradeAsync(GradeLevelInput i);
    Task<ServiceResult> DeleteGradeAsync(int id);
    Task<ClassroomInput?> GetClassroomAsync(int id);
    Task<ServiceResult> SaveClassroomAsync(ClassroomInput i);
    Task<ServiceResult> DeleteClassroomAsync(int id);
    /// <summary>คัดลอกโครงห้องเรียนจากปีหนึ่งไปอีกปี (ข้ามห้องที่มีอยู่แล้ว)</summary>
    Task<ServiceResult<int>> CloneClassroomsAsync(int fromYearId, int toYearId);
}

public class AcademicStructureService(ApplicationDbContext db, ILookupService lookups) : IAcademicStructureService
{
    private const string InUse = "ลบไม่ได้ เนื่องจากข้อมูลนี้ถูกใช้งานอยู่";

    public async Task<AcademicStructureViewModel> GetIndexAsync(string tab, int? yearFilter)
    {
        var vm = new AcademicStructureViewModel { Tab = tab, YearOptions = await lookups.AcademicYearsAsync() };
        var currentYearId = await db.AcademicYears.Where(y => y.IsCurrent).Select(y => (int?)y.Id).FirstOrDefaultAsync();
        vm.YearFilter = yearFilter ?? currentYearId;

        switch (tab)
        {
            case "semesters":
                var semesters = await db.Semesters.AsNoTracking().Include(s => s.AcademicYear)
                    .OrderByDescending(s => s.AcademicYear.Year).ThenBy(s => s.Number).ToListAsync();
                vm.Semesters = semesters.Select(s => new SemesterRow
                {
                    Id = s.Id, YearName = s.AcademicYear.Name, Name = s.Name,
                    RangeText = $"{ThaiDate.Format(s.StartDate)} – {ThaiDate.Format(s.EndDate)}"
                }).ToList();
                break;
            case "levels":
                vm.Levels = await db.EducationLevels.AsNoTracking().OrderBy(l => l.SortOrder).Select(l => new EducationLevelRow
                {
                    Id = l.Id, Code = l.Code, Name = l.Name, SortOrder = l.SortOrder, GradeCount = l.GradeLevels.Count
                }).ToListAsync();
                break;
            case "grades":
                var grades = await db.GradeLevels.AsNoTracking().Include(g => g.EducationLevel).Include(g => g.NextGradeLevel)
                    .OrderBy(g => g.EducationLevel.SortOrder).ThenBy(g => g.YearNumber).ToListAsync();
                vm.Grades = grades.Select(g => new GradeLevelRow
                {
                    Id = g.Id, LevelName = g.EducationLevel.Name, Name = g.Name, YearNumber = g.YearNumber,
                    NextText = g.IsFinal ? "สำเร็จการศึกษา" : g.NextGradeLevel?.Name ?? "-"
                }).ToList();
                break;
            case "classrooms":
                vm.Classrooms = await db.Classrooms.AsNoTracking()
                    .Where(c => vm.YearFilter == null || c.AcademicYearId == vm.YearFilter)
                    .OrderBy(c => c.EducationLevel.SortOrder).ThenBy(c => c.GradeLevel.YearNumber).ThenBy(c => c.Department.Name).ThenBy(c => c.RoomNumber)
                    .Select(c => new ClassroomRow
                    {
                        Id = c.Id, Name = c.Name, YearName = c.AcademicYear.Name, GradeName = c.GradeLevel.Name,
                        DepartmentName = c.Department.Name,
                        AdvisorName = c.AdvisorTeacher != null ? c.AdvisorTeacher.User.FullName : "-",
                        StudentCount = db.StudentProfiles.Count(s => s.ClassroomId == c.Id)
                    }).ToListAsync();
                break;
            default:
                vm.Tab = "years";
                var years = await db.AcademicYears.AsNoTracking().OrderByDescending(y => y.Year)
                    .Select(y => new { y.Id, y.Name, y.StartDate, y.EndDate, y.IsCurrent, Count = y.Classrooms.Count }).ToListAsync();
                vm.Years = years.Select(y => new AcademicYearRow
                {
                    Id = y.Id, Name = y.Name, IsCurrent = y.IsCurrent, ClassroomCount = y.Count,
                    RangeText = $"{ThaiDate.Format(y.StartDate)} – {ThaiDate.Format(y.EndDate)}"
                }).ToList();
                break;
        }
        return vm;
    }

    public async Task<AcademicYearInput?> GetYearAsync(int id) =>
        await db.AcademicYears.AsNoTracking().Where(y => y.Id == id).Select(y => new AcademicYearInput
        {
            Id = y.Id, Year = y.Year, StartDate = y.StartDate, EndDate = y.EndDate, IsCurrent = y.IsCurrent
        }).FirstOrDefaultAsync();

    public async Task<ServiceResult> SaveYearAsync(AcademicYearInput i)
    {
        if (await db.AcademicYears.AnyAsync(y => y.Year == i.Year && y.Id != i.Id))
            return ServiceResult.Invalid($"มีปีการศึกษา {i.Year} อยู่แล้ว");
        var e = i.Id is null ? new AcademicYear() : await db.AcademicYears.FindAsync(i.Id);
        if (e is null) return ServiceResult.NotFound();
        e.Year = i.Year!.Value;
        e.Name = $"ปีการศึกษา {i.Year}";
        e.StartDate = i.StartDate!.Value;
        e.EndDate = i.EndDate!.Value;
        if (i.IsCurrent) await db.AcademicYears.Where(y => y.IsCurrent).ExecuteUpdateAsync(s => s.SetProperty(y => y.IsCurrent, false));
        e.IsCurrent = i.IsCurrent;
        if (i.Id is null) db.AcademicYears.Add(e);
        await db.SaveChangesAsync();
        return ServiceResult.Ok("บันทึกปีการศึกษาเรียบร้อยแล้ว");
    }

    public Task<ServiceResult> DeleteYearAsync(int id) => DeleteAsync<AcademicYear>(id, "ลบปีการศึกษาเรียบร้อยแล้ว");

    public async Task<SemesterInput?> GetSemesterAsync(int id) =>
        await db.Semesters.AsNoTracking().Where(s => s.Id == id).Select(s => new SemesterInput
        {
            Id = s.Id, AcademicYearId = s.AcademicYearId, Number = s.Number, StartDate = s.StartDate, EndDate = s.EndDate
        }).FirstOrDefaultAsync();

    public async Task<ServiceResult> SaveSemesterAsync(SemesterInput i)
    {
        var year = await db.AcademicYears.AsNoTracking().FirstOrDefaultAsync(y => y.Id == i.AcademicYearId);
        if (year is null) return ServiceResult.Invalid("ไม่พบปีการศึกษานี้");
        if (await db.Semesters.AnyAsync(s => s.AcademicYearId == i.AcademicYearId && s.Number == i.Number && s.Id != i.Id))
            return ServiceResult.Invalid("ปีการศึกษานี้มีภาคเรียนนี้อยู่แล้ว");
        var e = i.Id is null ? new Semester() : await db.Semesters.FindAsync(i.Id);
        if (e is null) return ServiceResult.NotFound();
        e.AcademicYearId = year.Id;
        e.Number = i.Number!.Value;
        e.Name = $"ภาคเรียนที่ {i.Number}/{year.Year}";
        e.StartDate = i.StartDate!.Value;
        e.EndDate = i.EndDate!.Value;
        if (i.Id is null) db.Semesters.Add(e);
        await db.SaveChangesAsync();
        return ServiceResult.Ok("บันทึกภาคเรียนเรียบร้อยแล้ว");
    }

    public Task<ServiceResult> DeleteSemesterAsync(int id) => DeleteAsync<Semester>(id, "ลบภาคเรียนเรียบร้อยแล้ว");

    public async Task<EducationLevelInput?> GetLevelAsync(int id) =>
        await db.EducationLevels.AsNoTracking().Where(l => l.Id == id).Select(l => new EducationLevelInput
        {
            Id = l.Id, Code = l.Code, Name = l.Name, SortOrder = l.SortOrder
        }).FirstOrDefaultAsync();

    public async Task<ServiceResult> SaveLevelAsync(EducationLevelInput i)
    {
        if (await db.EducationLevels.AnyAsync(l => l.Code == i.Code && l.Id != i.Id))
            return ServiceResult.Invalid("รหัสระดับการศึกษานี้ถูกใช้แล้ว");
        var e = i.Id is null ? new EducationLevel() : await db.EducationLevels.FindAsync(i.Id);
        if (e is null) return ServiceResult.NotFound();
        e.Code = i.Code!.Trim();
        e.Name = i.Name!.Trim();
        e.SortOrder = i.SortOrder;
        if (i.Id is null) db.EducationLevels.Add(e);
        await db.SaveChangesAsync();
        return ServiceResult.Ok("บันทึกระดับการศึกษาเรียบร้อยแล้ว");
    }

    public Task<ServiceResult> DeleteLevelAsync(int id) => DeleteAsync<EducationLevel>(id, "ลบระดับการศึกษาเรียบร้อยแล้ว");

    public async Task<GradeLevelInput?> GetGradeAsync(int id) =>
        await db.GradeLevels.AsNoTracking().Where(g => g.Id == id).Select(g => new GradeLevelInput
        {
            Id = g.Id, EducationLevelId = g.EducationLevelId, YearNumber = g.YearNumber, Name = g.Name,
            NextGradeLevelId = g.NextGradeLevelId, IsFinal = g.IsFinal
        }).FirstOrDefaultAsync();

    public async Task<ServiceResult> SaveGradeAsync(GradeLevelInput i)
    {
        if (await db.GradeLevels.AnyAsync(g => g.EducationLevelId == i.EducationLevelId && g.YearNumber == i.YearNumber && g.Id != i.Id))
            return ServiceResult.Invalid("ระดับการศึกษานี้มีชั้นปีนี้อยู่แล้ว");
        var e = i.Id is null ? new GradeLevel() : await db.GradeLevels.FindAsync(i.Id);
        if (e is null) return ServiceResult.NotFound();
        e.EducationLevelId = i.EducationLevelId!.Value;
        e.YearNumber = i.YearNumber!.Value;
        e.Name = i.Name!.Trim();
        e.NextGradeLevelId = i.NextGradeLevelId;
        e.IsFinal = i.IsFinal;
        if (i.Id is null) db.GradeLevels.Add(e);
        await db.SaveChangesAsync();
        return ServiceResult.Ok("บันทึกชั้นปีเรียบร้อยแล้ว");
    }

    public Task<ServiceResult> DeleteGradeAsync(int id) => DeleteAsync<GradeLevel>(id, "ลบชั้นปีเรียบร้อยแล้ว");

    public async Task<ClassroomInput?> GetClassroomAsync(int id) =>
        await db.Classrooms.AsNoTracking().Where(c => c.Id == id).Select(c => new ClassroomInput
        {
            Id = c.Id, AcademicYearId = c.AcademicYearId, GradeLevelId = c.GradeLevelId, DepartmentId = c.DepartmentId,
            RoomNumber = c.RoomNumber, AdvisorTeacherProfileId = c.AdvisorTeacherProfileId
        }).FirstOrDefaultAsync();

    public async Task<ServiceResult> SaveClassroomAsync(ClassroomInput i)
    {
        var grade = await db.GradeLevels.AsNoTracking().FirstOrDefaultAsync(g => g.Id == i.GradeLevelId);
        var dept = await db.Departments.AsNoTracking().FirstOrDefaultAsync(d => d.Id == i.DepartmentId);
        if (grade is null || dept is null || !await db.AcademicYears.AnyAsync(y => y.Id == i.AcademicYearId))
            return ServiceResult.Invalid("ข้อมูลอ้างอิงไม่ถูกต้อง");
        if (await db.Classrooms.AnyAsync(c => c.AcademicYearId == i.AcademicYearId && c.GradeLevelId == i.GradeLevelId
                && c.DepartmentId == i.DepartmentId && c.RoomNumber == i.RoomNumber && c.Id != i.Id))
            return ServiceResult.Invalid("มีห้องเรียนนี้ในปีการศึกษานี้อยู่แล้ว");

        var e = i.Id is null ? new Classroom() : await db.Classrooms.FindAsync(i.Id);
        if (e is null) return ServiceResult.NotFound();
        e.AcademicYearId = i.AcademicYearId!.Value;
        e.GradeLevelId = grade.Id;
        e.EducationLevelId = grade.EducationLevelId;
        e.DepartmentId = dept.Id;
        e.RoomNumber = i.RoomNumber!.Value;
        e.Name = ClassroomName(grade.Name, dept.Code, i.RoomNumber.Value);
        e.AdvisorTeacherProfileId = i.AdvisorTeacherProfileId;
        if (i.Id is null) db.Classrooms.Add(e);
        await db.SaveChangesAsync();
        return ServiceResult.Ok("บันทึกห้องเรียนเรียบร้อยแล้ว");
    }

    public Task<ServiceResult> DeleteClassroomAsync(int id) => DeleteAsync<Classroom>(id, "ลบห้องเรียนเรียบร้อยแล้ว");

    public async Task<ServiceResult<int>> CloneClassroomsAsync(int fromYearId, int toYearId)
    {
        if (fromYearId == toYearId) return ServiceResult<int>.From(ServiceResult.Invalid("ปีต้นทางและปลายทางต้องไม่ซ้ำกัน"));
        if (!await db.AcademicYears.AnyAsync(y => y.Id == toYearId))
            return ServiceResult<int>.From(ServiceResult.NotFound("ไม่พบปีการศึกษาปลายทาง"));
        var source = await db.Classrooms.AsNoTracking().Where(c => c.AcademicYearId == fromYearId).ToListAsync();
        var existing = (await db.Classrooms.AsNoTracking().Where(c => c.AcademicYearId == toYearId)
            .Select(c => new { c.GradeLevelId, c.DepartmentId, c.RoomNumber }).ToListAsync()).ToHashSet();
        var added = 0;
        foreach (var c in source.Where(c => !existing.Contains(new { c.GradeLevelId, c.DepartmentId, c.RoomNumber })))
        {
            db.Classrooms.Add(new Classroom
            {
                AcademicYearId = toYearId, EducationLevelId = c.EducationLevelId, GradeLevelId = c.GradeLevelId,
                DepartmentId = c.DepartmentId, RoomNumber = c.RoomNumber, Name = c.Name
            });
            added++;
        }
        await db.SaveChangesAsync();
        return ServiceResult<int>.Ok(added, $"สร้างห้องเรียนใหม่ {added} ห้อง");
    }

    public static string ClassroomName(string gradeName, string departmentCode, int room) => $"{gradeName} {departmentCode}/{room}";

    private async Task<ServiceResult> DeleteAsync<T>(int id, string message) where T : class
    {
        var e = await db.Set<T>().FindAsync(id);
        if (e is null) return ServiceResult.NotFound();
        db.Set<T>().Remove(e);
        try
        {
            await db.SaveChangesAsync();
            return ServiceResult.Ok(message);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return ServiceResult.Invalid(InUse);
        }
    }
}

public interface IMasterDataService
{
    Task<List<DepartmentRow>> GetDepartmentsAsync();
    Task<DepartmentInput?> GetDepartmentAsync(int id);
    Task<DepartmentDetailsViewModel?> GetDepartmentDetailsAsync(int id, int? yearId);
    Task<ServiceResult> SaveDepartmentAsync(DepartmentInput i);
    Task<ServiceResult> DeleteDepartmentAsync(int id);
    Task<MasterDataViewModel> GetIndexAsync(string tab);
    Task<LocationInput?> GetLocationAsync(int id);
    Task<ServiceResult> SaveLocationAsync(LocationInput i);
    Task<ServiceResult> DeleteLocationAsync(int id);
    Task<CategoryInput?> GetCategoryAsync(int id);
    Task<ServiceResult> SaveCategoryAsync(CategoryInput i);
    Task<ServiceResult> DeleteCategoryAsync(int id);
    Task<StudentStatusInput?> GetStatusAsync(int id);
    Task<ServiceResult> SaveStatusAsync(StudentStatusInput i);
    Task<ServiceResult> DeleteStatusAsync(int id);
}

public class MasterDataService(
    ApplicationDbContext db,
    IRepository<Department> departments,
    IRepository<Location> locations,
    IRepository<ActivityCategory> categories,
    IRepository<StudentStatus> statuses) : IMasterDataService
{
    private const string InUse = "ลบไม่ได้ เนื่องจากข้อมูลนี้ถูกใช้งานอยู่ แนะนำให้ปิดการใช้งานแทน";

    public Task<List<DepartmentRow>> GetDepartmentsAsync() =>
        departments.Query().AsNoTracking().OrderBy(d => d.Name).Select(d => new DepartmentRow
        {
            Id = d.Id, Code = d.Code, Name = d.Name, IsActive = d.IsActive,
            ClassroomCount = d.Classrooms.Count,
            StudentCount = db.StudentProfiles.Count(s => s.DepartmentId == d.Id),
            TeacherCount = db.TeacherAssignments.Where(t => t.DepartmentId == d.Id).Select(t => t.TeacherProfileId).Distinct().Count(),
            EventCount = db.EventTargetDepartments.Count(t => t.DepartmentId == d.Id)
        }).ToListAsync();

    public Task<DepartmentInput?> GetDepartmentAsync(int id) =>
        departments.Query().AsNoTracking().Where(d => d.Id == id).Select(d => new DepartmentInput
        {
            Id = d.Id, Code = d.Code, Name = d.Name, IsActive = d.IsActive
        }).FirstOrDefaultAsync();

    public async Task<DepartmentDetailsViewModel?> GetDepartmentDetailsAsync(int id, int? yearId)
    {
        var dept = await departments.Query().AsNoTracking().FirstOrDefaultAsync(d => d.Id == id);
        if (dept is null) return null;
        var year = yearId ?? await db.AcademicYears.AsNoTracking().Where(y => y.IsCurrent).Select(y => (int?)y.Id).FirstOrDefaultAsync();

        var rooms = await db.Classrooms.AsNoTracking()
            .Where(c => c.DepartmentId == id && c.AcademicYearId == year)
            .OrderBy(c => c.EducationLevel.SortOrder).ThenBy(c => c.GradeLevel.YearNumber).ThenBy(c => c.RoomNumber)
            .Select(c => new
            {
                c.Id, c.Name, c.GradeLevelId, GradeName = c.GradeLevel.Name,
                AdvisorUserId = c.AdvisorTeacher != null ? c.AdvisorTeacher.UserId : null,
                AdvisorName = c.AdvisorTeacher != null ? c.AdvisorTeacher.User.FullName : null
            }).ToListAsync();
        var roomIds = rooms.Select(r => r.Id).ToList();

        var students = await db.StudentProfiles.AsNoTracking()
            .Where(s => s.ClassroomId != null && roomIds.Contains(s.ClassroomId.Value))
            .OrderBy(s => s.StudentCode)
            .Select(s => new { RoomId = s.ClassroomId!.Value, s.UserId, s.StudentCode, s.User.FullName, Status = s.StudentStatus.Name })
            .ToListAsync();

        // ห้องของปีอื่นไม่เกี่ยวกับปีที่กำลังดู
        var assignments = await db.TeacherAssignments.AsNoTracking()
            .Where(a => a.DepartmentId == id && (a.ClassroomId == null || roomIds.Contains(a.ClassroomId.Value)))
            .OrderBy(a => a.TeacherProfile.User.FullName)
            .Select(a => new
            {
                a.TeacherProfile.UserId, a.TeacherProfile.User.FullName, a.GradeLevelId, a.ClassroomId,
                GradeName = a.GradeLevel != null ? a.GradeLevel.Name : null,
                RoomName = a.Classroom != null ? a.Classroom.Name : null
            }).ToListAsync();

        List<DepartmentTeacherTag> RoomTeachers(int roomId, int gradeId, string? advisorId, string? advisorName)
        {
            var tags = new List<DepartmentTeacherTag>();
            if (advisorId is not null) tags.Add(new() { UserId = advisorId, Name = advisorName!, Scope = "ที่ปรึกษา" });
            tags.AddRange(assignments.Where(a => a.ClassroomId == roomId).Select(a => new DepartmentTeacherTag { UserId = a.UserId, Name = a.FullName, Scope = "ดูแลห้องนี้" }));
            tags.AddRange(assignments.Where(a => a.ClassroomId == null && a.GradeLevelId == gradeId).Select(a => new DepartmentTeacherTag { UserId = a.UserId, Name = a.FullName, Scope = "ดูแลทั้งชั้นปี" }));
            tags.AddRange(assignments.Where(a => a.ClassroomId == null && a.GradeLevelId == null).Select(a => new DepartmentTeacherTag { UserId = a.UserId, Name = a.FullName, Scope = "ดูแลทั้งแผนก" }));
            // คนเดียวมีได้หลายเหตุ แสดงเหตุที่เฉพาะเจาะจงที่สุด
            return tags.DistinctBy(t => t.UserId).ToList();
        }

        var grades = rooms.GroupBy(r => new { r.GradeLevelId, r.GradeName }).Select(g => new DepartmentGradeView
        {
            Name = g.Key.GradeName,
            Rooms = g.Select(r => new DepartmentRoomView
            {
                Id = r.Id,
                Name = r.Name,
                Teachers = RoomTeachers(r.Id, r.GradeLevelId, r.AdvisorUserId, r.AdvisorName),
                Students = students.Where(s => s.RoomId == r.Id).Select(s => new DepartmentStudentRow
                {
                    UserId = s.UserId, Code = s.StudentCode, Name = s.FullName, Status = s.Status
                }).ToList()
            }).ToList()
        }).ToList();

        var teacherScopes = assignments.Select(a => new
        {
            a.UserId, a.FullName,
            Scope = a.ClassroomId != null ? $"ห้อง {a.RoomName}" : a.GradeLevelId != null ? $"{a.GradeName} ทุกห้อง" : "ทั้งแผนก"
        }).Concat(rooms.Where(r => r.AdvisorUserId != null).Select(r => new
        {
            UserId = r.AdvisorUserId!, FullName = r.AdvisorName!, Scope = $"ที่ปรึกษา {r.Name}"
        }));
        var teachers = teacherScopes.GroupBy(t => new { t.UserId, t.FullName }).OrderBy(g => g.Key.FullName).Select(g => new DepartmentTeacherTag
        {
            UserId = g.Key.UserId, Name = g.Key.FullName, Scope = string.Join(", ", g.Select(x => x.Scope).Distinct())
        }).ToList();

        var unassigned = await db.StudentProfiles.AsNoTracking()
            .Where(s => s.DepartmentId == id && s.ClassroomId == null && s.StudentStatus.IsEnrolled)
            .OrderBy(s => s.StudentCode)
            .Select(s => new DepartmentStudentRow { UserId = s.UserId, Code = s.StudentCode, Name = s.User.FullName, Status = s.StudentStatus.Name })
            .ToListAsync();

        return new DepartmentDetailsViewModel
        {
            Id = dept.Id,
            Code = dept.Code,
            Name = dept.Name,
            YearId = year,
            Grades = grades,
            Teachers = teachers,
            UnassignedStudents = unassigned,
            Stats =
            [
                new() { Label = "ชั้นปี", Value = grades.Count.ToString("N0"), Icon = "bi-layers" },
                new() { Label = "ห้องเรียน", Value = rooms.Count.ToString("N0"), Icon = "bi-door-open" },
                new() { Label = "นักเรียนในห้อง", Value = students.Count.ToString("N0"), Icon = "bi-mortarboard" },
                new() { Label = "ครูผู้ดูแล", Value = teachers.Count.ToString("N0"), Icon = "bi-person-badge" }
            ]
        };
    }

    public async Task<ServiceResult> SaveDepartmentAsync(DepartmentInput i)
    {
        if (await departments.Query().AnyAsync(d => (d.Code == i.Code || d.Name == i.Name) && d.Id != i.Id))
            return ServiceResult.Invalid("รหัสหรือชื่อแผนกนี้ถูกใช้แล้ว");
        var e = i.Id is null ? new Department() : await departments.FindAsync(i.Id.Value);
        if (e is null) return ServiceResult.NotFound();
        e.Code = i.Code!.Trim();
        e.Name = i.Name!.Trim();
        e.IsActive = i.IsActive;
        if (i.Id is null) departments.Add(e);
        await departments.SaveChangesAsync();
        return ServiceResult.Ok("บันทึกแผนกเรียบร้อยแล้ว");
    }

    public Task<ServiceResult> DeleteDepartmentAsync(int id) => DeleteAsync(departments, id, "ลบแผนกเรียบร้อยแล้ว");

    public async Task<MasterDataViewModel> GetIndexAsync(string tab)
    {
        var vm = new MasterDataViewModel { Tab = tab };
        switch (tab)
        {
            case "categories":
                vm.Categories = await categories.Query().AsNoTracking().OrderBy(c => c.Name).Select(c => new CategoryRow
                {
                    Id = c.Id, Name = c.Name, Color = c.Color, IsActive = c.IsActive,
                    EventCount = db.Events.Count(e => e.ActivityCategoryId == c.Id)
                }).ToListAsync();
                break;
            case "statuses":
                vm.Statuses = await statuses.Query().AsNoTracking().OrderBy(s => s.SortOrder).Select(s => new StudentStatusRow
                {
                    Id = s.Id, Name = s.Name, IsEnrolled = s.IsEnrolled, IsSystem = s.SystemCode != null,
                    StudentCount = db.StudentProfiles.Count(p => p.StudentStatusId == s.Id)
                }).ToListAsync();
                break;
            default:
                vm.Tab = "locations";
                vm.Locations = await locations.Query().AsNoTracking().OrderBy(l => l.Name).Select(l => new LocationRow
                {
                    Id = l.Id, Name = l.Name, Building = l.Building ?? "-", Capacity = l.Capacity, IsActive = l.IsActive
                }).ToListAsync();
                break;
        }
        return vm;
    }

    public Task<LocationInput?> GetLocationAsync(int id) =>
        locations.Query().AsNoTracking().Where(l => l.Id == id).Select(l => new LocationInput
        {
            Id = l.Id, Name = l.Name, Building = l.Building, Capacity = l.Capacity, IsActive = l.IsActive
        }).FirstOrDefaultAsync();

    public async Task<ServiceResult> SaveLocationAsync(LocationInput i)
    {
        if (await locations.Query().AnyAsync(l => l.Name == i.Name && l.Id != i.Id))
            return ServiceResult.Invalid("มีชื่อสถานที่นี้แล้ว");
        var e = i.Id is null ? new Location() : await locations.FindAsync(i.Id.Value);
        if (e is null) return ServiceResult.NotFound();
        e.Name = i.Name!.Trim();
        e.Building = i.Building?.Trim();
        e.Capacity = i.Capacity;
        e.IsActive = i.IsActive;
        if (i.Id is null) locations.Add(e);
        await locations.SaveChangesAsync();
        return ServiceResult.Ok("บันทึกสถานที่เรียบร้อยแล้ว");
    }

    public Task<ServiceResult> DeleteLocationAsync(int id) => DeleteAsync(locations, id, "ลบสถานที่เรียบร้อยแล้ว");

    public Task<CategoryInput?> GetCategoryAsync(int id) =>
        categories.Query().AsNoTracking().Where(c => c.Id == id).Select(c => new CategoryInput
        {
            Id = c.Id, Name = c.Name, Color = c.Color, IsActive = c.IsActive
        }).FirstOrDefaultAsync();

    public async Task<ServiceResult> SaveCategoryAsync(CategoryInput i)
    {
        if (await categories.Query().AnyAsync(c => c.Name == i.Name && c.Id != i.Id))
            return ServiceResult.Invalid("มีชื่อประเภทกิจกรรมนี้แล้ว");
        var e = i.Id is null ? new ActivityCategory() : await categories.FindAsync(i.Id.Value);
        if (e is null) return ServiceResult.NotFound();
        e.Name = i.Name!.Trim();
        e.Color = i.Color!;
        e.IsActive = i.IsActive;
        if (i.Id is null) categories.Add(e);
        await categories.SaveChangesAsync();
        return ServiceResult.Ok("บันทึกประเภทกิจกรรมเรียบร้อยแล้ว");
    }

    public Task<ServiceResult> DeleteCategoryAsync(int id) => DeleteAsync(categories, id, "ลบประเภทกิจกรรมเรียบร้อยแล้ว");

    public Task<StudentStatusInput?> GetStatusAsync(int id) =>
        statuses.Query().AsNoTracking().Where(s => s.Id == id).Select(s => new StudentStatusInput
        {
            Id = s.Id, Name = s.Name, IsEnrolled = s.IsEnrolled, SortOrder = s.SortOrder, IsSystem = s.SystemCode != null
        }).FirstOrDefaultAsync();

    public async Task<ServiceResult> SaveStatusAsync(StudentStatusInput i)
    {
        var e = i.Id is null ? new StudentStatus() : await statuses.FindAsync(i.Id.Value);
        if (e is null) return ServiceResult.NotFound();
        e.Name = i.Name!.Trim();
        e.SortOrder = i.SortOrder;
        // สถานะของระบบใช้โดยการเลื่อนชั้น จึงไม่ให้เปลี่ยนความหมาย
        if (e.SystemCode is null) e.IsEnrolled = i.IsEnrolled;
        if (i.Id is null) statuses.Add(e);
        await statuses.SaveChangesAsync();
        return ServiceResult.Ok("บันทึกสถานะนักเรียนเรียบร้อยแล้ว");
    }

    public async Task<ServiceResult> DeleteStatusAsync(int id)
    {
        var e = await statuses.FindAsync(id);
        if (e?.SystemCode is not null) return ServiceResult.Invalid("สถานะนี้เป็นสถานะของระบบ ไม่สามารถลบได้");
        return await DeleteAsync(statuses, id, "ลบสถานะนักเรียนเรียบร้อยแล้ว");
    }

    private async Task<ServiceResult> DeleteAsync<T>(IRepository<T> repo, int id, string message) where T : class
    {
        var e = await repo.FindAsync(id);
        if (e is null) return ServiceResult.NotFound();
        repo.Remove(e);
        try
        {
            await repo.SaveChangesAsync();
            return ServiceResult.Ok(message);
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return ServiceResult.Invalid(InUse);
        }
    }
}
