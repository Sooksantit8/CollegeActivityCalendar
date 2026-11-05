using CollegeActivityCalendar.Data;
using CollegeActivityCalendar.Infrastructure;
using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Models.ViewModels;
using CollegeActivityCalendar.Repositories;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CollegeActivityCalendar.Services;

public interface IEventService
{
    Task<EventListViewModel> GetListAsync(EventFilter filter, int pageSize = 12);
    Task<List<CalendarEventDto>> GetCalendarEventsAsync(DateOnly from, DateOnly to, EventFilter filter);
    Task<ServiceResult<EventDetailsDto>> GetDetailsAsync(int id);
    Task<ServiceResult<int>> CreateAsync(EventInput input);
    Task<ServiceResult<int>> UpdateAsync(int id, EventInput input);
    Task<ServiceResult> DeleteAsync(int id);
    Task<ServiceResult> PublishAsync(int id);
    Task<ServiceResult> CancelAsync(int id);
    Task<List<EventRow>> GetUpcomingAsync(int days, int take);
    Task<List<ClassroomOption>> GetTargetClassroomsAsync(int? yearId, int? levelId, int? gradeId, int? departmentId);
    Task<List<UserOption>> SearchTargetUsersAsync(string? role, string? q);
    Task<int> PublishDueEventsAsync();
    Task<EventFormOptions> GetFormOptionsAsync();
    Task<EventFilterOptions> GetFilterOptionsAsync();
    Task<List<EventRow>> ToRowsAsync(List<Event> events, UserScope scope);
}

public class EventService(
    ApplicationDbContext db,
    IEventRepository events,
    IEventAccessService access,
    IUserScopeProvider scopes,
    ILookupService lookups,
    IClock clock) : IEventService
{
    public async Task<EventListViewModel> GetListAsync(EventFilter f, int pageSize = 12)
    {
        var scope = await scopes.GetAsync();
        var visible = access.Visible(db.Events.AsNoTracking(), scope);
        if (f.ManageableOnly) visible = access.Manageable(visible, scope);
        var query = ApplyFilter(visible, f, scope);
        var total = await query.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var page = Math.Clamp(f.Page, 1, totalPages);

        // กิจกรรมที่กำลังจะมาถึงเรียงจากใกล้สุด ส่วนรายการทั่วไปเรียงจากล่าสุด
        var ordered = f.StartsAfter is not null ? query.OrderBy(e => e.StartDate) : query.OrderByDescending(e => e.StartDate);
        var ids = await ordered.ThenBy(e => e.StartTime).ThenBy(e => e.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).Select(e => e.Id).ToListAsync();
        var items = await events.QueryWithDetails().Where(e => ids.Contains(e.Id)).ToListAsync();
        items = items.OrderBy(e => ids.IndexOf(e.Id)).ToList();

        return new EventListViewModel
        {
            Items = await ToRowsAsync(items, scope),
            Page = page,
            TotalPages = totalPages,
            TotalCount = total
        };
    }

    public async Task<List<CalendarEventDto>> GetCalendarEventsAsync(DateOnly from, DateOnly to, EventFilter f)
    {
        var scope = await scopes.GetAsync();
        var query = access.Visible(db.Events.AsNoTracking(), scope).Where(e => e.StartDate < to && e.EndDate >= from);
        var list = await ApplyFilter(query, f, scope)
            .OrderBy(e => e.StartDate)
            .Take(1000)
            .Select(e => new
            {
                e.Id, e.Title, e.StartDate, e.EndDate, e.StartTime, e.EndTime, e.IsAllDay, e.Status, e.IsOnline,
                Category = e.ActivityCategory.Name,
                Location = e.Location != null ? e.Location.Name : e.LocationText,
                Levels = e.Targets.Select(t => t.TargetType).ToList()
            }).ToListAsync();

        return list.Select(e =>
        {
            var level = e.Levels.Count > 0 ? e.Levels.Min() : EventTargetType.College;
            var allDay = e.IsAllDay || e.StartTime is null;
            return new CalendarEventDto
            {
                Id = e.Id,
                Title = e.Title,
                AllDay = allDay,
                Start = allDay ? e.StartDate.ToString("yyyy-MM-dd") : $"{e.StartDate:yyyy-MM-dd}T{e.StartTime:HH:mm}",
                // วันสิ้นสุดของ FullCalendar สำหรับกิจกรรมทั้งวันเป็นแบบ exclusive
                End = allDay ? e.EndDate.AddDays(1).ToString("yyyy-MM-dd") : $"{e.EndDate:yyyy-MM-dd}T{e.EndTime:HH:mm}",
                Level = level.ToString().ToLowerInvariant(),
                LevelText = DisplayText.Level(level),
                ClassName = $"fc-level-{level.ToString().ToLowerInvariant()} fc-status-{e.Status.ToString().ToLowerInvariant()}",
                Status = e.Status.ToString(),
                StatusText = DisplayText.Status(e.Status),
                CategoryName = e.Category,
                LocationText = e.IsOnline ? (string.IsNullOrEmpty(e.Location) ? "ออนไลน์" : $"{e.Location} (ออนไลน์)") : e.Location ?? ""
            };
        }).ToList();
    }

    public async Task<ServiceResult<EventDetailsDto>> GetDetailsAsync(int id)
    {
        var scope = await scopes.GetAsync();
        var visible = await access.Visible(db.Events.AsNoTracking(), scope).AnyAsync(e => e.Id == id);
        if (!visible)
        {
            // ไม่บอกว่ามีกิจกรรมนี้อยู่จริงหรือไม่ หากผู้ใช้ไม่มีสิทธิ์เห็น
            return ServiceResult<EventDetailsDto>.From(ServiceResult.NotFound("ไม่พบกิจกรรมนี้ หรือคุณไม่มีสิทธิ์ดูกิจกรรมนี้"));
        }
        var e = await events.QueryWithDetails().FirstAsync(x => x.Id == id);
        var canManage = await access.Manageable(db.Events.AsNoTracking(), scope).AnyAsync(x => x.Id == id);

        var targets = new EventTargetsDto
        {
            College = e.Targets.Any(t => t.TargetType == EventTargetType.College),
            Departments = e.Targets.SelectMany(t => t.Departments).Select(d => new IdName(d.DepartmentId.ToString(), d.Department.Name)).ToList(),
            Classrooms = e.Targets.SelectMany(t => t.Classrooms).Select(c => new IdName(c.ClassroomId.ToString(), c.Classroom.Name)).ToList(),
            UserCount = e.Targets.SelectMany(t => t.Users).Count()
        };
        // รายชื่อบุคคลเป้าหมายเห็นเฉพาะผู้จัดการกิจกรรม นักเรียนเห็นเฉพาะชื่อตนเอง
        var userTargets = e.Targets.SelectMany(t => t.Users);
        if (!canManage) userTargets = userTargets.Where(u => u.UserId == scope.UserId);
        targets.Users = userTargets.Select(u => new IdName(u.UserId, u.User.FullName)).ToList();

        var dto = new EventDetailsDto
        {
            Id = e.Id,
            Title = e.Title,
            Description = e.Description,
            ActivityCategoryId = e.ActivityCategoryId,
            CategoryName = e.ActivityCategory.Name,
            CategoryColor = e.ActivityCategory.Color,
            StartDate = e.StartDate,
            EndDate = e.EndDate,
            StartTime = e.StartTime,
            EndTime = e.EndTime,
            IsAllDay = e.IsAllDay,
            DateText = ThaiDate.Range(e.StartDate, e.EndDate, e.StartTime, e.EndTime, e.IsAllDay),
            LocationId = e.LocationId,
            LocationText = e.LocationText,
            LocationDisplay = LocationDisplay(e),
            IsOnline = e.IsOnline,
            OnlineUrl = e.OnlineUrl,
            ResponsibleUserId = canManage ? e.ResponsibleUserId : null,
            ResponsibleName = e.ResponsibleUser.FullName,
            PublishAt = e.PublishAt,
            PublishAtText = ThaiDate.Format(e.PublishAt),
            DisplayUntil = e.DisplayUntil,
            DisplayUntilText = ThaiDate.Format(e.DisplayUntil),
            Status = e.Status,
            StatusText = DisplayText.Status(e.Status),
            Levels = e.Targets.Select(t => t.TargetType).OrderBy(t => t).Select(DisplayText.Level).ToList(),
            Targets = targets,
            CanManage = canManage
        };
        return ServiceResult<EventDetailsDto>.Ok(dto);
    }

    public async Task<ServiceResult<int>> CreateAsync(EventInput input)
    {
        var scope = await scopes.GetAsync();
        if (!scope.CanCreateEvents) return ServiceResult<int>.From(ServiceResult.Forbidden());

        var check = await ValidateReferencesAsync(input);
        if (!check.Succeeded) return ServiceResult<int>.From(check);
        var targetCheck = await access.ValidateTargetsAsync(scope, input.Targets, null);
        if (!targetCheck.Succeeded) return ServiceResult<int>.From(targetCheck);

        var now = clock.Now;
        var entity = new Event { CreatedByUserId = scope.UserId, CreatedAt = now };
        Map(input, entity, now);
        SyncTargets(entity, input.Targets);
        events.Add(entity);
        await events.SaveChangesAsync();
        return ServiceResult<int>.Ok(entity.Id, "บันทึกกิจกรรมเรียบร้อยแล้ว");
    }

    public async Task<ServiceResult<int>> UpdateAsync(int id, EventInput input)
    {
        var scope = await scopes.GetAsync();
        if (!await access.Manageable(db.Events, scope).AnyAsync(e => e.Id == id))
            return ServiceResult<int>.From(await MissingOrForbiddenAsync(id));

        var entity = await events.GetForUpdateAsync(id);
        if (entity is null) return ServiceResult<int>.From(ServiceResult.NotFound());

        var check = await ValidateReferencesAsync(input);
        if (!check.Succeeded) return ServiceResult<int>.From(check);
        var targetCheck = await access.ValidateTargetsAsync(scope, input.Targets, entity);
        if (!targetCheck.Succeeded) return ServiceResult<int>.From(targetCheck);

        Map(input, entity, clock.Now);
        SyncTargets(entity, input.Targets);
        await events.SaveChangesAsync();
        return ServiceResult<int>.Ok(entity.Id, "แก้ไขกิจกรรมเรียบร้อยแล้ว");
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        var scope = await scopes.GetAsync();
        if (!await access.Manageable(db.Events, scope).AnyAsync(e => e.Id == id))
            return await MissingOrForbiddenAsync(id);
        var entity = await events.GetForUpdateAsync(id);
        if (entity is null) return ServiceResult.NotFound();
        events.Remove(entity);
        await events.SaveChangesAsync();
        return ServiceResult.Ok("ลบกิจกรรมเรียบร้อยแล้ว");
    }

    public async Task<ServiceResult> PublishAsync(int id)
    {
        var scope = await scopes.GetAsync();
        if (!await access.Manageable(db.Events, scope).AnyAsync(e => e.Id == id))
            return await MissingOrForbiddenAsync(id);
        var entity = await events.FindAsync(id);
        if (entity is null) return ServiceResult.NotFound();
        var now = clock.Now;
        entity.Status = EventStatus.Published;
        if (entity.PublishAt is null || entity.PublishAt > now) entity.PublishAt = now;
        entity.UpdatedAt = now;
        await events.SaveChangesAsync();
        return ServiceResult.Ok("เผยแพร่กิจกรรมเรียบร้อยแล้ว");
    }

    public async Task<ServiceResult> CancelAsync(int id)
    {
        var scope = await scopes.GetAsync();
        if (!await access.Manageable(db.Events, scope).AnyAsync(e => e.Id == id))
            return await MissingOrForbiddenAsync(id);
        var entity = await events.FindAsync(id);
        if (entity is null) return ServiceResult.NotFound();
        entity.Status = EventStatus.Cancelled;
        entity.UpdatedAt = clock.Now;
        await events.SaveChangesAsync();
        return ServiceResult.Ok("ยกเลิกกิจกรรมเรียบร้อยแล้ว");
    }

    public async Task<List<EventRow>> GetUpcomingAsync(int days, int take)
    {
        var scope = await scopes.GetAsync();
        var today = clock.Today;
        var until = today.AddDays(days);
        var ids = await access.Visible(db.Events.AsNoTracking(), scope)
            .Where(e => e.EndDate >= today && e.StartDate <= until && e.Status != EventStatus.Cancelled && e.Status != EventStatus.Draft)
            .OrderBy(e => e.StartDate).ThenBy(e => e.StartTime)
            .Take(take).Select(e => e.Id).ToListAsync();
        var items = await events.QueryWithDetails().Where(e => ids.Contains(e.Id)).ToListAsync();
        return await ToRowsAsync(items.OrderBy(e => ids.IndexOf(e.Id)).ToList(), scope);
    }

    public async Task<List<ClassroomOption>> GetTargetClassroomsAsync(int? yearId, int? levelId, int? gradeId, int? departmentId)
    {
        var scope = await scopes.GetAsync();
        if (!scope.CanCreateEvents) return [];
        var q = db.Classrooms.AsNoTracking().AsQueryable();
        if (scope.IsTeacher && !scope.IsAdmin)
            q = q.Where(c => scope.DepartmentIds.Contains(c.DepartmentId) || scope.ClassroomIds.Contains(c.Id));
        if (yearId is not null) q = q.Where(c => c.AcademicYearId == yearId);
        if (levelId is not null) q = q.Where(c => c.EducationLevelId == levelId);
        if (gradeId is not null) q = q.Where(c => c.GradeLevelId == gradeId);
        if (departmentId is not null) q = q.Where(c => c.DepartmentId == departmentId);
        return await q.OrderBy(c => c.EducationLevel.SortOrder).ThenBy(c => c.GradeLevel.YearNumber).ThenBy(c => c.Department.Name).ThenBy(c => c.RoomNumber)
            .Take(300)
            .Select(c => new ClassroomOption
            {
                Id = c.Id, Name = c.Name, GradeName = c.GradeLevel.Name, DepartmentName = c.Department.Name,
                GradeLevelId = c.GradeLevelId, DepartmentId = c.DepartmentId, RoomNumber = c.RoomNumber
            }).ToListAsync();
    }

    public async Task<List<UserOption>> SearchTargetUsersAsync(string? role, string? q)
    {
        var scope = await scopes.GetAsync();
        if (!scope.CanCreateEvents) return [];
        var users = db.Users.AsNoTracking().Where(u => u.IsActive);
        var teacherLimited = scope.IsTeacher && !scope.IsAdmin;

        if (teacherLimited)
        {
            users = users.Where(u => u.Id == scope.UserId || (u.StudentProfile != null
                && (scope.DepartmentIds.Contains(u.StudentProfile.DepartmentId)
                    || (u.StudentProfile.ClassroomId != null && scope.ClassroomIds.Contains(u.StudentProfile.ClassroomId.Value)))));
        }
        users = role switch
        {
            AppRoles.Student => users.Where(u => u.StudentProfile != null),
            AppRoles.Teacher => users.Where(u => u.TeacherProfile != null),
            AppRoles.Staff => users.Where(u => u.StaffProfile != null),
            _ => users
        };
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim();
            users = users.Where(u => u.FullName.Contains(term) || u.Email!.Contains(term)
                || (u.StudentProfile != null && u.StudentProfile.StudentCode.Contains(term)));
        }
        var rows = await users.OrderBy(u => u.FullName).Take(30).Select(u => new
        {
            u.Id, u.FullName,
            IsStudent = u.StudentProfile != null,
            IsTeacher = u.TeacherProfile != null,
            IsStaff = u.StaffProfile != null,
            Room = u.StudentProfile != null && u.StudentProfile.Classroom != null ? u.StudentProfile.Classroom.Name : null
        }).ToListAsync();

        return rows.Select(u => new UserOption
        {
            Id = u.Id,
            Name = u.FullName,
            RoleText = u.IsStudent ? DisplayText.Role(AppRoles.Student)
                : u.IsTeacher ? DisplayText.Role(AppRoles.Teacher)
                : u.IsStaff ? DisplayText.Role(AppRoles.Staff) : DisplayText.Role(AppRoles.Admin),
            Detail = u.Room ?? ""
        }).ToList();
    }

    public async Task<int> PublishDueEventsAsync()
    {
        var now = clock.Now;
        return await db.Events
            .Where(e => e.Status == EventStatus.Scheduled && e.PublishAt != null && e.PublishAt <= now)
            .ExecuteUpdateAsync(s => s.SetProperty(e => e.Status, EventStatus.Published).SetProperty(e => e.UpdatedAt, now));
    }

    public async Task<EventFormOptions> GetFormOptionsAsync()
    {
        var scope = await scopes.GetAsync();
        var departments = await lookups.DepartmentsAsync();
        var roomDepartments = departments;
        if (scope.IsTeacher && !scope.IsAdmin)
        {
            roomDepartments = departments.Where(d => scope.RelatedDepartmentIds.Contains(int.Parse(d.Value))).ToList();
            departments = departments.Where(d => scope.DepartmentIds.Contains(int.Parse(d.Value))).ToList();
        }
        return new EventFormOptions
        {
            Categories = await lookups.CategoriesAsync(),
            Locations = await lookups.LocationsAsync(),
            ResponsibleUsers = await lookups.ResponsibleUsersAsync(),
            Statuses = lookups.EventStatuses(),
            Departments = departments,
            RoomDepartments = roomDepartments,
            AcademicYears = await lookups.AcademicYearsAsync(selectCurrent: true),
            EducationLevels = await lookups.EducationLevelsAsync(),
            GradeLevels = await lookups.GradeLevelsAsync(),
            CanTargetCollege = scope.CanTargetCollege,
            CurrentUserId = scope.UserId
        };
    }

    public async Task<EventFilterOptions> GetFilterOptionsAsync()
    {
        var scope = await scopes.GetAsync();
        return new EventFilterOptions
        {
            Departments = await lookups.DepartmentsAsync(),
            GradeLevels = await lookups.GradeLevelsAsync(),
            Classrooms = await lookups.ClassroomsAsync(null),
            Categories = await lookups.CategoriesAsync(),
            Statuses = lookups.EventStatuses(),
            ShowStatus = !scope.IsStudent
        };
    }

    public async Task<List<EventRow>> ToRowsAsync(List<Event> items, UserScope scope)
    {
        var ids = items.Select(i => i.Id).ToList();
        var manageable = (await access.Manageable(db.Events.AsNoTracking(), scope)
            .Where(e => ids.Contains(e.Id)).Select(e => e.Id).ToListAsync()).ToHashSet();
        return items.Select(e => new EventRow
        {
            Id = e.Id,
            Title = e.Title,
            CategoryName = e.ActivityCategory.Name,
            CategoryColor = e.ActivityCategory.Color,
            StartDate = e.StartDate,
            DateText = ThaiDate.Range(e.StartDate, e.EndDate, e.StartTime, e.EndTime, e.IsAllDay),
            LocationText = LocationDisplay(e),
            ResponsibleName = e.ResponsibleUser.FullName,
            Status = e.Status,
            Levels = e.Targets.Select(t => t.TargetType).OrderBy(t => t).ToList(),
            CanManage = manageable.Contains(e.Id)
        }).ToList();
    }

    private static IQueryable<Event> ApplyFilter(IQueryable<Event> q, EventFilter f, UserScope scope)
    {
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var term = f.Q.Trim();
            q = q.Where(e => e.Title.Contains(term)
                || (e.Description != null && e.Description.Contains(term))
                || (e.LocationText != null && e.LocationText.Contains(term))
                || (e.Location != null && e.Location.Name.Contains(term)));
        }
        if (f.CategoryId is not null) q = q.Where(e => e.ActivityCategoryId == f.CategoryId);
        // นักเรียนเห็นเฉพาะที่เผยแพร่แล้ว จึงไม่มีตัวกรองสถานะ
        if (f.Status is not null && !scope.IsStudent) q = q.Where(e => e.Status == f.Status);
        if (f.From is not null) q = q.Where(e => e.EndDate >= f.From);
        if (f.To is not null) q = q.Where(e => e.StartDate <= f.To);
        if (f.StartsAfter is not null) q = q.Where(e => e.StartDate > f.StartsAfter);
        if (f.ActiveOnly) q = q.Where(e => e.Status != EventStatus.Cancelled);
        if (f.Level is not null) q = q.Where(e => e.Targets.Any(t => t.TargetType == f.Level));
        if (f.DepartmentId is not null)
        {
            q = q.Where(e => e.Targets.Any(t => t.Departments.Any(d => d.DepartmentId == f.DepartmentId)
                                             || t.Classrooms.Any(c => c.Classroom.DepartmentId == f.DepartmentId)));
        }
        if (f.GradeLevelId is not null)
            q = q.Where(e => e.Targets.Any(t => t.Classrooms.Any(c => c.Classroom.GradeLevelId == f.GradeLevelId)));
        if (f.ClassroomId is not null)
            q = q.Where(e => e.Targets.Any(t => t.Classrooms.Any(c => c.ClassroomId == f.ClassroomId)));
        return q;
    }

    private async Task<ServiceResult> ValidateReferencesAsync(EventInput i)
    {
        var fields = new Dictionary<string, string[]>();
        if (!await db.ActivityCategories.AnyAsync(c => c.Id == i.ActivityCategoryId))
            fields[nameof(i.ActivityCategoryId)] = ["ไม่พบประเภทกิจกรรมนี้"];
        if (i.LocationId is not null && !await db.Locations.AnyAsync(l => l.Id == i.LocationId))
            fields[nameof(i.LocationId)] = ["ไม่พบสถานที่นี้"];
        var responsibleOk = await (from ur in db.UserRoles
                                   join r in db.Roles on ur.RoleId equals r.Id
                                   where ur.UserId == i.ResponsibleUserId && r.Name != AppRoles.Student
                                   select ur.UserId).AnyAsync();
        if (!responsibleOk) fields[nameof(i.ResponsibleUserId)] = ["ผู้รับผิดชอบต้องเป็นครู เจ้าหน้าที่ หรือผู้ดูแลระบบ"];

        if (i.Status == EventStatus.Published && i.PublishAt > clock.Now)
            fields[nameof(i.PublishAt)] = ["วันเวลาเผยแพร่อยู่ในอนาคต กรุณาใช้สถานะตั้งเวลาเผยแพร่"];

        return fields.Count == 0 ? ServiceResult.Ok() : ServiceResult.Invalid("กรุณาตรวจสอบข้อมูลอีกครั้ง", fields);
    }

    private async Task<ServiceResult> MissingOrForbiddenAsync(int id) =>
        await db.Events.AnyAsync(e => e.Id == id)
            ? ServiceResult.Forbidden("คุณไม่มีสิทธิ์จัดการกิจกรรมนี้")
            : ServiceResult.NotFound("ไม่พบกิจกรรมนี้");

    private static void Map(EventInput i, Event e, DateTime now)
    {
        e.Title = i.Title!.Trim();
        e.Description = string.IsNullOrWhiteSpace(i.Description) ? null : i.Description.Trim();
        e.ActivityCategoryId = i.ActivityCategoryId!.Value;
        e.StartDate = i.StartDate!.Value;
        e.EndDate = i.EndDate!.Value;
        e.IsAllDay = i.IsAllDay;
        e.StartTime = i.IsAllDay ? null : i.StartTime;
        e.EndTime = i.IsAllDay ? null : i.EndTime;
        e.IsOnline = i.IsOnline;
        e.OnlineUrl = i.IsOnline && !string.IsNullOrWhiteSpace(i.OnlineUrl) ? i.OnlineUrl.Trim() : null;
        e.LocationId = i.LocationId;
        e.LocationText = string.IsNullOrWhiteSpace(i.LocationText) ? null : i.LocationText.Trim();
        e.ResponsibleUserId = i.ResponsibleUserId!;
        e.Status = i.Status!.Value;
        e.PublishAt = i.PublishAt ?? (i.Status == EventStatus.Published ? now : null);
        e.DisplayUntil = i.DisplayUntil;
        e.UpdatedAt = now;
    }

    private static void SyncTargets(Event e, EventTargetInput input)
    {
        EventTarget? Find(EventTargetType type) => e.Targets.FirstOrDefault(t => t.TargetType == type);
        EventTarget Ensure(EventTargetType type)
        {
            var t = Find(type);
            if (t is null) { t = new EventTarget { TargetType = type }; e.Targets.Add(t); }
            return t;
        }
        void Drop(EventTargetType type)
        {
            var t = Find(type);
            if (t is not null) e.Targets.Remove(t);
        }

        if (input.College) Ensure(EventTargetType.College); else Drop(EventTargetType.College);

        var depts = input.DepartmentIds.Distinct().ToList();
        if (depts.Count == 0) Drop(EventTargetType.Department);
        else
        {
            var t = Ensure(EventTargetType.Department);
            foreach (var old in t.Departments.Where(d => !depts.Contains(d.DepartmentId)).ToList()) t.Departments.Remove(old);
            foreach (var id in depts.Where(id => t.Departments.All(d => d.DepartmentId != id)))
                t.Departments.Add(new EventTargetDepartment { DepartmentId = id });
        }

        var rooms = input.ClassroomIds.Distinct().ToList();
        if (rooms.Count == 0) Drop(EventTargetType.Classroom);
        else
        {
            var t = Ensure(EventTargetType.Classroom);
            foreach (var old in t.Classrooms.Where(c => !rooms.Contains(c.ClassroomId)).ToList()) t.Classrooms.Remove(old);
            foreach (var id in rooms.Where(id => t.Classrooms.All(c => c.ClassroomId != id)))
                t.Classrooms.Add(new EventTargetClassroom { ClassroomId = id });
        }

        var users = input.UserIds.Distinct().ToList();
        if (users.Count == 0) Drop(EventTargetType.Individual);
        else
        {
            var t = Ensure(EventTargetType.Individual);
            foreach (var old in t.Users.Where(u => !users.Contains(u.UserId)).ToList()) t.Users.Remove(old);
            foreach (var id in users.Where(id => t.Users.All(u => u.UserId != id)))
                t.Users.Add(new EventTargetUser { UserId = id });
        }
    }

    private static string LocationDisplay(Event e)
    {
        var place = e.Location?.Name;
        if (!string.IsNullOrEmpty(e.LocationText))
            place = string.IsNullOrEmpty(place) ? e.LocationText : $"{place} ({e.LocationText})";
        if (e.IsOnline) return string.IsNullOrEmpty(place) ? "ออนไลน์" : $"{place} + ออนไลน์";
        return place ?? "-";
    }
}

public class ScheduledEventPublisher(IServiceScopeFactory factory, ILogger<ScheduledEventPublisher> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                using var scope = factory.CreateScope();
                var count = await scope.ServiceProvider.GetRequiredService<IEventService>().PublishDueEventsAsync();
                if (count > 0) log.LogInformation("เผยแพร่กิจกรรมตามเวลาที่ตั้งไว้ {Count} รายการ", count);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                log.LogWarning(ex, "เผยแพร่กิจกรรมตามเวลาไม่สำเร็จ จะลองใหม่ในรอบถัดไป");
            }
        }
    }
}
