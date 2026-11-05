using System.Globalization;
using System.Text;
using CollegeActivityCalendar.Data;
using CollegeActivityCalendar.Infrastructure;
using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Models.ViewModels;
using CollegeActivityCalendar.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace CollegeActivityCalendar.Services;

public interface IDashboardService
{
    Task<DashboardViewModel> GetAsync();
}

public class DashboardService(
    ApplicationDbContext db,
    IEventAccessService access,
    IUserScopeProvider scopes,
    IEventService events,
    IEventRepository eventRepository,
    ISettingsService settings,
    IClock clock) : IDashboardService
{
    public async Task<DashboardViewModel> GetAsync()
    {
        var scope = await scopes.GetAsync();
        var today = clock.Today;
        var upcomingDays = (await settings.GetAsync()).UpcomingDays;
        var monthStart = new DateOnly(today.Year, today.Month, 1);
        var monthEnd = monthStart.AddMonths(1).AddDays(-1);

        var visible = access.Visible(db.Events.AsNoTracking(), scope);
        var vm = new DashboardViewModel
        {
            HeroTitle = scope.IsStudent ? "ทุกกิจกรรมของคุณ ในปฏิทินเดียว" : "วางแผนทุกกิจกรรม อย่างเป็นระบบ",
            HeroText = scope.IsStudent
                ? "ติดตามกิจกรรมของวิทยาลัย แผนก และห้องเรียนของคุณได้ที่นี่"
                : "ติดตามกำหนดการ สถานะการเผยแพร่ และภาพรวมกิจกรรมของวิทยาลัย",
            Upcoming = await events.GetUpcomingAsync(upcomingDays, 6)
        };

        var thisMonth = await visible.CountAsync(e => e.StartDate <= monthEnd && e.EndDate >= monthStart && e.Status != EventStatus.Cancelled);
        var today_ = await visible.CountAsync(e => e.StartDate <= today && e.EndDate >= today && e.Status != EventStatus.Cancelled);
        var upcoming = await visible.CountAsync(e => e.StartDate > today && e.StartDate <= today.AddDays(upcomingDays) && e.Status != EventStatus.Cancelled);

        // เงื่อนไขของลิงก์ต้องตรงกับที่นับด้านบน ตัวเลขบนการ์ดจึงเท่ากับจำนวนในหน้ารายการ
        static string Iso(DateOnly d) => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        vm.Stats.Add(new()
        {
            Label = "กิจกรรมวันนี้", Value = today_.ToString("N0"), Icon = "bi-calendar-check", LinkController = "Events",
            LinkValues = new() { ["preset"] = "today", ["from"] = Iso(today), ["to"] = Iso(today), ["activeOnly"] = "true" }
        });
        vm.Stats.Add(new()
        {
            Label = "กิจกรรมเดือนนี้", Value = thisMonth.ToString("N0"), Icon = "bi-calendar3", LinkController = "Events",
            LinkValues = new() { ["preset"] = "month", ["from"] = Iso(monthStart), ["to"] = Iso(monthEnd), ["activeOnly"] = "true" }
        });
        vm.Stats.Add(new()
        {
            Label = $"กำลังจะมาถึงใน {upcomingDays} วัน", Value = upcoming.ToString("N0"), Icon = "bi-calendar-event", LinkController = "Events",
            LinkValues = new() { ["preset"] = "upcoming", ["startsAfter"] = Iso(today), ["to"] = Iso(today.AddDays(upcomingDays)), ["activeOnly"] = "true" }
        });

        if (scope.IsAdmin)
        {
            vm.Stats.Add(new()
            {
                Label = "ผู้ใช้งานที่เปิดใช้", Value = (await db.Users.CountAsync(u => u.IsActive)).ToString("N0"), Icon = "bi-people",
                LinkController = "Users", LinkValues = new() { ["isActive"] = "true" }
            });
        }
        else if (scope.IsStudent)
        {
            var room = await db.StudentProfiles.AsNoTracking().Where(s => s.UserId == scope.UserId)
                .Select(s => new { s.ClassroomId, Name = s.Classroom != null ? s.Classroom.Name : null }).FirstOrDefaultAsync();
            var card = new StatCardViewModel { Label = "ห้องเรียนของฉัน", Value = room?.Name ?? "-", Icon = "bi-mortarboard" };
            if (room?.ClassroomId is not null)
            {
                card.LinkController = "Events";
                card.LinkValues = new() { ["preset"] = "classroom", ["classroomId"] = room.ClassroomId.Value.ToString(CultureInfo.InvariantCulture) };
            }
            vm.Stats.Add(card);
        }
        else
        {
            var drafts = await access.Manageable(db.Events.AsNoTracking(), scope).CountAsync(e => e.Status == EventStatus.Draft);
            vm.Stats.Add(new()
            {
                Label = "แบบร่างที่ต้องจัดการ", Value = drafts.ToString("N0"), Icon = "bi-pencil-square", LinkController = "Events",
                LinkValues = new() { ["preset"] = "drafts", ["status"] = nameof(EventStatus.Draft), ["manageableOnly"] = "true" }
            });
        }

        if (!scope.IsStudent)
        {
            var ids = await access.Manageable(db.Events.AsNoTracking(), scope)
                .Where(e => (e.Status == EventStatus.Draft || e.Status == EventStatus.Scheduled) && e.EndDate >= today)
                .OrderBy(e => e.StartDate).Take(5).Select(e => e.Id).ToListAsync();
            var items = await eventRepository.QueryWithDetails().Where(e => ids.Contains(e.Id)).ToListAsync();
            vm.NeedsAttention = await events.ToRowsAsync(items.OrderBy(e => ids.IndexOf(e.Id)).ToList(), scope);
            vm.Form = await events.GetFormOptionsAsync();
        }
        return vm;
    }
}

public interface IReportService
{
    Task<ReportViewModel> GetAsync(DateOnly? from, DateOnly? to, int? departmentId);
    Task<byte[]> ExportEventsCsvAsync(DateOnly? from, DateOnly? to, int? departmentId);
}

public class ReportService(ApplicationDbContext db, ILookupService lookups, IStudentPromotionService promotions, IClock clock) : IReportService
{
    private (DateOnly From, DateOnly To) ResolveRange(DateOnly? from, DateOnly? to)
    {
        var today = clock.Today;
        var f = from ?? new DateOnly(today.Year, 1, 1);
        var t = to ?? new DateOnly(today.Year, 12, 31);
        return f > t ? (t, f) : (f, t);
    }

    private IQueryable<Event> Scoped(DateOnly from, DateOnly to, int? departmentId)
    {
        var q = db.Events.AsNoTracking().Where(e => e.StartDate <= to && e.EndDate >= from);
        if (departmentId is not null)
        {
            q = q.Where(e => e.Targets.Any(t => t.Departments.Any(d => d.DepartmentId == departmentId)
                                             || t.Classrooms.Any(c => c.Classroom.DepartmentId == departmentId)));
        }
        return q;
    }

    public async Task<ReportViewModel> GetAsync(DateOnly? from, DateOnly? to, int? departmentId)
    {
        var (f, t) = ResolveRange(from, to);
        var q = Scoped(f, t, departmentId);

        var total = await q.CountAsync();
        var byStatus = await q.GroupBy(e => e.Status).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var byCategory = await q.GroupBy(e => new { e.ActivityCategory.Name, e.ActivityCategory.Color })
            .Select(g => new { g.Key.Name, g.Key.Color, Count = g.Count() }).OrderByDescending(x => x.Count).ToListAsync();
        var byMonth = await q.GroupBy(e => new { e.StartDate.Year, e.StartDate.Month })
            .Select(g => new { g.Key.Year, g.Key.Month, Count = g.Count() }).OrderBy(x => x.Year).ThenBy(x => x.Month).ToListAsync();
        var eventIds = q.Select(e => e.Id);
        var byLevel = await db.EventTargets.AsNoTracking().Where(x => eventIds.Contains(x.EventId))
            .GroupBy(x => x.TargetType).Select(g => new { g.Key, Count = g.Count() }).ToListAsync();
        var byDept = await db.EventTargetDepartments.AsNoTracking().Where(x => eventIds.Contains(x.EventTarget.EventId))
            .GroupBy(x => x.Department.Name).Select(g => new { g.Key, Count = g.Select(y => y.EventTarget.EventId).Distinct().Count() })
            .OrderByDescending(x => x.Count).Take(12).ToListAsync();
        var students = await db.StudentProfiles.AsNoTracking().Where(s => s.StudentStatus.IsEnrolled)
            .Where(s => departmentId == null || s.DepartmentId == departmentId)
            .GroupBy(s => s.Department.Name).Select(g => new { g.Key, Count = g.Count() })
            .OrderByDescending(x => x.Count).ToListAsync();

        int Status(EventStatus s) => byStatus.FirstOrDefault(x => x.Key == s)?.Count ?? 0;

        return new ReportViewModel
        {
            From = f,
            To = t,
            DepartmentId = departmentId,
            Departments = await lookups.DepartmentsAsync(activeOnly: false),
            Stats =
            [
                new() { Label = "กิจกรรมทั้งหมด", Value = total.ToString("N0"), Icon = "bi-calendar3" },
                new() { Label = "เผยแพร่แล้ว", Value = Status(EventStatus.Published).ToString("N0"), Icon = "bi-check2-circle" },
                new() { Label = "แบบร่าง / ตั้งเวลา", Value = (Status(EventStatus.Draft) + Status(EventStatus.Scheduled)).ToString("N0"), Icon = "bi-pencil-square" },
                new() { Label = "ยกเลิก", Value = Status(EventStatus.Cancelled).ToString("N0"), Icon = "bi-x-circle" }
            ],
            ByMonth = byMonth.Select(x => new ReportBar { Label = ThaiDate.MonthYear(x.Year, x.Month), Count = x.Count }).ToList(),
            ByCategory = byCategory.Select(x => new ReportBar { Label = x.Name, Count = x.Count, Color = x.Color }).ToList(),
            ByLevel = byLevel.OrderBy(x => x.Key).Select(x => new ReportBar { Label = DisplayText.Level(x.Key), Count = x.Count }).ToList(),
            ByStatus = byStatus.OrderBy(x => x.Key).Select(x => new ReportBar { Label = DisplayText.Status(x.Key), Count = x.Count }).ToList(),
            ByDepartment = byDept.Select(x => new ReportBar { Label = x.Key, Count = x.Count }).ToList(),
            StudentsByDepartment = students.Select(x => new ReportBar { Label = x.Key, Count = x.Count }).ToList(),
            RecentPromotions = await promotions.GetHistoryAsync(5)
        };
    }

    public async Task<byte[]> ExportEventsCsvAsync(DateOnly? from, DateOnly? to, int? departmentId)
    {
        var (f, t) = ResolveRange(from, to);
        var rows = await Scoped(f, t, departmentId).OrderBy(e => e.StartDate).ThenBy(e => e.StartTime)
            .Select(e => new
            {
                e.Id, e.Title, Category = e.ActivityCategory.Name, e.StartDate, e.EndDate, e.StartTime, e.EndTime, e.IsAllDay,
                Location = e.Location != null ? e.Location.Name : e.LocationText, e.IsOnline,
                Responsible = e.ResponsibleUser.FullName, e.Status,
                Levels = e.Targets.Select(x => x.TargetType).ToList()
            }).ToListAsync();

        var sb = new StringBuilder();
        sb.AppendLine("รหัส,ชื่อกิจกรรม,ประเภท,วันเวลา,สถานที่,ผู้รับผิดชอบ,สถานะ,ระดับกลุ่มเป้าหมาย");
        foreach (var e in rows)
        {
            sb.AppendLine(string.Join(",", new[]
            {
                e.Id.ToString(), Csv(e.Title), Csv(e.Category),
                Csv(ThaiDate.Range(e.StartDate, e.EndDate, e.StartTime, e.EndTime, e.IsAllDay)),
                Csv(e.IsOnline ? $"{e.Location} ออนไลน์".Trim() : e.Location ?? ""),
                Csv(e.Responsible), Csv(DisplayText.Status(e.Status)),
                Csv(string.Join(" / ", e.Levels.OrderBy(x => x).Select(DisplayText.Level)))
            }));
        }
        // BOM ทำให้ Excel อ่านภาษาไทยได้ถูกต้อง
        return [.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(sb.ToString())];
    }

    private static string Csv(string value)
    {
        // กัน CSV formula injection เมื่อเปิดใน Excel
        if (value.Length > 0 && "=+-@\t\r".Contains(value[0])) value = "'" + value;
        return $"\"{value.Replace("\"", "\"\"")}\"";
    }
}

public interface ISettingsService
{
    Task<SettingsInput> GetAsync();
    Task<ServiceResult> SaveAsync(SettingsInput input);
}

public class SettingsService(ApplicationDbContext db, IMemoryCache cache) : ISettingsService
{
    private const string CacheKey = "system-settings";

    public async Task<SettingsInput> GetAsync()
    {
        var cached = await cache.GetOrCreateAsync(CacheKey, async entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10);
            var values = await db.SystemSettings.AsNoTracking().ToDictionaryAsync(s => s.Key, s => s.Value);
            return new SettingsInput
            {
                CollegeName = values.GetValueOrDefault(nameof(SettingsInput.CollegeName)) ?? "วิทยาลัยตัวอย่าง",
                ContactEmail = values.GetValueOrDefault(nameof(SettingsInput.ContactEmail)),
                ContactPhone = values.GetValueOrDefault(nameof(SettingsInput.ContactPhone)),
                DefaultCalendarView = values.GetValueOrDefault(nameof(SettingsInput.DefaultCalendarView)) ?? "dayGridMonth",
                UpcomingDays = int.TryParse(values.GetValueOrDefault(nameof(SettingsInput.UpcomingDays)), out var d) ? d : 30
            };
        });
        var result = cached!;
        return new SettingsInput
        {
            CollegeName = result.CollegeName, ContactEmail = result.ContactEmail, ContactPhone = result.ContactPhone,
            DefaultCalendarView = result.DefaultCalendarView, UpcomingDays = result.UpcomingDays,
            CurrentAcademicYearId = await db.AcademicYears.Where(y => y.IsCurrent).Select(y => (int?)y.Id).FirstOrDefaultAsync()
        };
    }

    public async Task<ServiceResult> SaveAsync(SettingsInput i)
    {
        if (!await db.AcademicYears.AnyAsync(y => y.Id == i.CurrentAcademicYearId))
            return ServiceResult.Invalid("ไม่พบปีการศึกษานี้");
        await using var tx = await db.Database.BeginTransactionAsync();
        await SetAsync(nameof(i.CollegeName), i.CollegeName);
        await SetAsync(nameof(i.ContactEmail), i.ContactEmail);
        await SetAsync(nameof(i.ContactPhone), i.ContactPhone);
        await SetAsync(nameof(i.DefaultCalendarView), i.DefaultCalendarView);
        await SetAsync(nameof(i.UpcomingDays), i.UpcomingDays.ToString());
        await db.SaveChangesAsync();
        await db.AcademicYears.ExecuteUpdateAsync(s => s.SetProperty(y => y.IsCurrent, y => y.Id == i.CurrentAcademicYearId));
        await tx.CommitAsync();
        cache.Remove(CacheKey);
        return ServiceResult.Ok("บันทึกการตั้งค่าเรียบร้อยแล้ว");
    }

    private async Task SetAsync(string key, string? value)
    {
        var row = await db.SystemSettings.FindAsync(key);
        if (row is null) db.SystemSettings.Add(new SystemSetting { Key = key, Value = value });
        else row.Value = value;
    }
}
