using System.Security.Claims;
using CollegeActivityCalendar.Data;
using CollegeActivityCalendar.Infrastructure;
using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CollegeActivityCalendar.Services;

/// <summary>ขอบเขตข้อมูลของผู้ใช้ปัจจุบัน</summary>
public sealed class UserScope
{
    public string UserId { get; init; } = string.Empty;
    public bool IsAdmin { get; init; }
    public bool IsStaff { get; init; }
    public bool IsTeacher { get; init; }
    public bool IsStudent { get; init; }
    public bool CanCreateEvents => IsAdmin || IsStaff || IsTeacher;
    public bool CanTargetCollege => IsAdmin || IsStaff;
    /// <summary>ครู: แผนกที่ดูแลทั้งแผนก / นักเรียน: แผนกของตน</summary>
    public List<int> DepartmentIds { get; init; } = [];
    /// <summary>ครู: ห้องที่ดูแลรายห้อง รายชั้นปี หรือเป็นที่ปรึกษา / นักเรียน: ห้องของตน</summary>
    public List<int> ClassroomIds { get; init; } = [];
    /// <summary>ครู: ทุกแผนกที่เกี่ยวข้อง แม้ดูแลเพียงบางห้อง ใช้สำหรับการมองเห็นเท่านั้น ไม่ใช่สิทธิ์จัดการ</summary>
    public List<int> RelatedDepartmentIds { get; init; } = [];
}

public interface IUserScopeProvider
{
    Task<UserScope> GetAsync();
}

public class UserScopeProvider(IHttpContextAccessor http, ApplicationDbContext db) : IUserScopeProvider
{
    private UserScope? _cached;

    public async Task<UserScope> GetAsync()
    {
        if (_cached is not null) return _cached;
        var user = http.HttpContext?.User ?? new ClaimsPrincipal();
        var userId = user.GetUserId();
        var isTeacher = user.IsInRole(AppRoles.Teacher);
        var isStudent = user.IsInRole(AppRoles.Student);
        List<int> departments = [], classrooms = [], related = [];

        if (isTeacher)
        {
            var assignments = await db.TeacherAssignments.Where(x => x.TeacherProfile.UserId == userId)
                .Select(x => new { x.DepartmentId, x.GradeLevelId, x.ClassroomId }).ToListAsync();
            departments = assignments.Where(a => a.GradeLevelId == null && a.ClassroomId == null)
                .Select(a => a.DepartmentId).Distinct().ToList();

            var advised = await db.Classrooms.Where(x => x.AdvisorTeacher != null && x.AdvisorTeacher.UserId == userId)
                .Select(x => new { x.Id, x.DepartmentId }).ToListAsync();
            classrooms = advised.Select(c => c.Id)
                .Concat(assignments.Where(a => a.ClassroomId != null).Select(a => a.ClassroomId!.Value)).ToList();

            // ดูแลรายชั้นปี = ทุกห้องของชั้นปีนั้นในแผนกนั้น ทุกปีการศึกษา
            var byGrade = assignments.Where(a => a.GradeLevelId != null && a.ClassroomId == null)
                .Select(a => (a.DepartmentId, GradeLevelId: a.GradeLevelId!.Value)).ToHashSet();
            if (byGrade.Count > 0)
            {
                var gradeIds = byGrade.Select(g => g.GradeLevelId).Distinct().ToList();
                var rooms = await db.Classrooms.Where(c => gradeIds.Contains(c.GradeLevelId))
                    .Select(c => new { c.Id, c.DepartmentId, c.GradeLevelId }).ToListAsync();
                classrooms.AddRange(rooms.Where(c => byGrade.Contains((c.DepartmentId, c.GradeLevelId))).Select(c => c.Id));
            }
            classrooms = classrooms.Distinct().ToList();
            related = assignments.Select(a => a.DepartmentId).Concat(advised.Select(c => c.DepartmentId)).Distinct().ToList();
        }
        else if (isStudent)
        {
            var profile = await db.StudentProfiles.Where(x => x.UserId == userId)
                .Select(x => new { x.DepartmentId, x.ClassroomId }).FirstOrDefaultAsync();
            if (profile is not null)
            {
                departments.Add(profile.DepartmentId);
                if (profile.ClassroomId is not null) classrooms.Add(profile.ClassroomId.Value);
                related = departments;
            }
        }

        return _cached = new UserScope
        {
            UserId = userId,
            IsAdmin = user.IsInRole(AppRoles.Admin),
            IsStaff = user.IsInRole(AppRoles.Staff),
            IsTeacher = isTeacher,
            IsStudent = isStudent,
            DepartmentIds = departments,
            ClassroomIds = classrooms,
            RelatedDepartmentIds = related
        };
    }
}

public interface IEventAccessService
{
    /// <summary>กิจกรรมที่ผู้ใช้เห็นได้ — ทุกหน้าที่อ่านกิจกรรมต้องผ่านเมธอดนี้</summary>
    IQueryable<Event> Visible(IQueryable<Event> source, UserScope scope);
    /// <summary>กิจกรรมที่ผู้ใช้แก้ไข เผยแพร่ ยกเลิก หรือลบได้</summary>
    IQueryable<Event> Manageable(IQueryable<Event> source, UserScope scope);
    Task<ServiceResult> ValidateTargetsAsync(UserScope scope, EventTargetInput requested, Event? existing);
}

public class EventAccessService(ApplicationDbContext db, IClock clock) : IEventAccessService
{
    public IQueryable<Event> Visible(IQueryable<Event> source, UserScope s)
    {
        if (s.IsAdmin) return source;
        var now = clock.Now;
        var today = clock.Today;
        var uid = s.UserId;
        var departments = s.DepartmentIds;
        var classrooms = s.ClassroomIds;

        if (s.IsStudent)
        {
            return source.Where(e =>
                (e.Status == EventStatus.Published || e.Status == EventStatus.Scheduled)
                && (e.PublishAt == null || e.PublishAt <= now)
                && (e.DisplayUntil == null || e.DisplayUntil >= today)
                && e.Targets.Any(t =>
                    t.TargetType == EventTargetType.College
                    || t.Departments.Any(d => departments.Contains(d.DepartmentId))
                    || t.Classrooms.Any(c => classrooms.Contains(c.ClassroomId))
                    || t.Users.Any(u => u.UserId == uid)));
        }

        if (s.IsStaff)
        {
            return source.Where(e =>
                e.CreatedByUserId == uid || e.ResponsibleUserId == uid
                || e.Targets.Any(t => t.TargetType == EventTargetType.College)
                || ((e.Status == EventStatus.Published || e.Status == EventStatus.Scheduled)
                    && (e.PublishAt == null || e.PublishAt <= now)
                    && (e.DisplayUntil == null || e.DisplayUntil >= today)
                    // นัดหมายรายบุคคลของผู้อื่นถือเป็นข้อมูลส่วนตัว
                    && e.Targets.Any(t => t.TargetType != EventTargetType.Individual || t.Users.Any(u => u.UserId == uid))));
        }

        if (s.IsTeacher)
        {
            var relatedDepartments = s.RelatedDepartmentIds;
            return source.Where(e =>
                e.CreatedByUserId == uid || e.ResponsibleUserId == uid
                // กิจกรรมทุกสถานะที่กลุ่มเป้าหมายทั้งหมดอยู่ในความรับผิดชอบ
                || (e.Targets.Any() && e.Targets.All(t =>
                       t.TargetType != EventTargetType.College
                       && t.Departments.All(d => departments.Contains(d.DepartmentId))
                       && t.Classrooms.All(c => departments.Contains(c.Classroom.DepartmentId) || classrooms.Contains(c.ClassroomId))
                       && t.Users.All(u => u.UserId == uid || (u.User.StudentProfile != null
                           && (departments.Contains(u.User.StudentProfile.DepartmentId)
                               || (u.User.StudentProfile.ClassroomId != null && classrooms.Contains(u.User.StudentProfile.ClassroomId.Value)))))))
                // กิจกรรมเผยแพร่แล้วที่ตรงกับครู
                || ((e.Status == EventStatus.Published || e.Status == EventStatus.Scheduled)
                    && (e.PublishAt == null || e.PublishAt <= now)
                    && (e.DisplayUntil == null || e.DisplayUntil >= today)
                    && e.Targets.Any(t =>
                        t.TargetType == EventTargetType.College
                        || t.Departments.Any(d => relatedDepartments.Contains(d.DepartmentId))
                        || t.Classrooms.Any(c => departments.Contains(c.Classroom.DepartmentId) || classrooms.Contains(c.ClassroomId))
                        || t.Users.Any(u => u.UserId == uid))));
        }

        return source.Where(_ => false);
    }

    public IQueryable<Event> Manageable(IQueryable<Event> source, UserScope s)
    {
        if (s.IsAdmin) return source;
        var uid = s.UserId;
        var departments = s.DepartmentIds;
        var classrooms = s.ClassroomIds;

        if (s.IsStaff)
        {
            return source.Where(e => e.CreatedByUserId == uid || e.ResponsibleUserId == uid
                || e.Targets.Any(t => t.TargetType == EventTargetType.College));
        }

        if (s.IsTeacher)
        {
            return source.Where(e => e.CreatedByUserId == uid || e.ResponsibleUserId == uid
                || (e.Targets.Any() && e.Targets.All(t =>
                       t.TargetType != EventTargetType.College
                       && t.Departments.All(d => departments.Contains(d.DepartmentId))
                       && t.Classrooms.All(c => departments.Contains(c.Classroom.DepartmentId) || classrooms.Contains(c.ClassroomId))
                       && t.Users.All(u => u.UserId == uid || (u.User.StudentProfile != null
                           && (departments.Contains(u.User.StudentProfile.DepartmentId)
                               || (u.User.StudentProfile.ClassroomId != null && classrooms.Contains(u.User.StudentProfile.ClassroomId.Value))))))));
        }

        return source.Where(_ => false);
    }

    public async Task<ServiceResult> ValidateTargetsAsync(UserScope s, EventTargetInput r, Event? existing)
    {
        if (!s.CanCreateEvents) return ServiceResult.Forbidden();

        // ข้อมูลอ้างอิงต้องมีอยู่จริงเสมอ ไม่ว่าเป็นบทบาทใด
        var deptIds = r.DepartmentIds.Distinct().ToList();
        var roomIds = r.ClassroomIds.Distinct().ToList();
        var userIds = r.UserIds.Distinct().ToList();
        if (await db.Departments.CountAsync(d => deptIds.Contains(d.Id)) != deptIds.Count
            || await db.Classrooms.CountAsync(c => roomIds.Contains(c.Id)) != roomIds.Count
            || await db.Users.CountAsync(u => userIds.Contains(u.Id)) != userIds.Count)
        {
            return ServiceResult.Invalid("พบกลุ่มเป้าหมายที่ไม่มีอยู่ในระบบ กรุณาเลือกใหม่");
        }

        if (!s.IsTeacher || s.IsAdmin) return ServiceResult.Ok();

        // ครูเพิ่มได้เฉพาะเป้าหมายในความรับผิดชอบ ส่วนเป้าหมายเดิมที่ได้รับมอบหมายคงไว้ได้
        var oldTargets = existing?.Targets ?? [];
        var hadCollege = oldTargets.Any(t => t.TargetType == EventTargetType.College);
        var oldDepts = oldTargets.SelectMany(t => t.Departments).Select(d => d.DepartmentId).ToHashSet();
        var oldRooms = oldTargets.SelectMany(t => t.Classrooms).Select(c => c.ClassroomId).ToHashSet();
        var oldUsers = oldTargets.SelectMany(t => t.Users).Select(u => u.UserId).ToHashSet();

        if (r.College && !hadCollege)
            return ServiceResult.Forbidden("ครูไม่สามารถกำหนดกลุ่มเป้าหมายระดับวิทยาลัยได้");

        var newDepts = deptIds.Where(id => !oldDepts.Contains(id)).ToList();
        if (newDepts.Any(id => !s.DepartmentIds.Contains(id)))
            return ServiceResult.Forbidden("มีแผนกที่อยู่นอกความรับผิดชอบของคุณ");

        var newRooms = roomIds.Where(id => !oldRooms.Contains(id)).ToList();
        var allowedRooms = await db.Classrooms.CountAsync(c => newRooms.Contains(c.Id)
            && (s.DepartmentIds.Contains(c.DepartmentId) || s.ClassroomIds.Contains(c.Id)));
        if (allowedRooms != newRooms.Count)
            return ServiceResult.Forbidden("มีห้องเรียนที่อยู่นอกความรับผิดชอบของคุณ");

        var newUsers = userIds.Where(id => !oldUsers.Contains(id) && id != s.UserId).ToList();
        var allowedUsers = await db.StudentProfiles.CountAsync(p => newUsers.Contains(p.UserId)
            && (s.DepartmentIds.Contains(p.DepartmentId)
                || (p.ClassroomId != null && s.ClassroomIds.Contains(p.ClassroomId.Value))));
        if (allowedUsers != newUsers.Count)
            return ServiceResult.Forbidden("มีบุคคลที่อยู่นอกความรับผิดชอบของคุณ");

        return ServiceResult.Ok();
    }
}
