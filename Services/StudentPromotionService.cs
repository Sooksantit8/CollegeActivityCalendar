using CollegeActivityCalendar.Data;
using CollegeActivityCalendar.Infrastructure;
using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace CollegeActivityCalendar.Services;

public interface IStudentPromotionService
{
    Task<PromotionIndexViewModel> GetIndexAsync();
    Task<ServiceResult<PromotionStudentsResponse>> GetStudentsAsync(PromotionStudentsRequest r);
    Task<ServiceResult<PromotionPreviewResponse>> PreviewAsync(PromotionConfirmRequest r);
    Task<ServiceResult<int>> ConfirmAsync(PromotionConfirmRequest r, string executedBy);
    Task<List<PromotionHistoryRow>> GetHistoryAsync(int take = 200);
    Task<PromotionHistoryDetailsViewModel?> GetHistoryDetailsAsync(int id);
}

public class StudentPromotionService(ApplicationDbContext db, ILookupService lookups, IClock clock) : IStudentPromotionService
{
    private sealed record PlannedItem(StudentProfile Student, PromotionItemInput Input, Classroom? Target, string? Error);

    public async Task<PromotionIndexViewModel> GetIndexAsync()
    {
        var years = await db.AcademicYears.AsNoTracking().OrderBy(y => y.Year).ToListAsync();
        var current = years.FirstOrDefault(y => y.IsCurrent) ?? years.LastOrDefault();
        var next = current is null ? null : years.FirstOrDefault(y => y.Year > current.Year);
        var grades = await db.GradeLevels.AsNoTracking().OrderBy(g => g.EducationLevel.SortOrder).ThenBy(g => g.YearNumber)
            .Select(g => new PromotionRuleRow { GradeLevelId = g.Id, GradeName = g.Name, NextGradeLevelId = g.NextGradeLevelId, IsFinal = g.IsFinal })
            .ToListAsync();
        return new PromotionIndexViewModel
        {
            Years = await lookups.AcademicYearsAsync(),
            DefaultFromYearId = current?.Id,
            DefaultToYearId = next?.Id,
            Departments = await lookups.DepartmentsAsync(),
            GradeLevels = await lookups.GradeLevelsAsync(),
            Rules = grades
        };
    }

    public async Task<ServiceResult<PromotionStudentsResponse>> GetStudentsAsync(PromotionStudentsRequest r)
    {
        var yearCheck = await ValidateYearsAsync(r.FromYearId, r.ToYearId);
        if (!yearCheck.Succeeded) return ServiceResult<PromotionStudentsResponse>.From(yearCheck);

        var q = db.StudentProfiles.AsNoTracking()
            .Where(s => s.Classroom != null && s.Classroom.AcademicYearId == r.FromYearId && s.User.IsActive);
        if (r.DepartmentId is not null) q = q.Where(s => s.DepartmentId == r.DepartmentId);
        if (r.GradeLevelId is not null) q = q.Where(s => s.GradeLevelId == r.GradeLevelId);
        if (r.ClassroomId is not null) q = q.Where(s => s.ClassroomId == r.ClassroomId);

        var students = await q.OrderBy(s => s.Classroom!.GradeLevel.EducationLevel.SortOrder)
            .ThenBy(s => s.Classroom!.GradeLevel.YearNumber).ThenBy(s => s.Department.Name)
            .ThenBy(s => s.Classroom!.RoomNumber).ThenBy(s => s.StudentCode)
            .Select(s => new
            {
                s.Id, s.StudentCode, s.User.FullName, s.DepartmentId, Dept = s.Department.Name,
                GradeId = s.Classroom!.GradeLevelId, Grade = s.Classroom.GradeLevel.Name,
                Room = s.Classroom.Name, s.Classroom.RoomNumber, Status = s.StudentStatus.Name,
                Enrolled = s.StudentStatus.IsEnrolled
            }).ToListAsync();

        var targets = await db.Classrooms.AsNoTracking().Where(c => c.AcademicYearId == r.ToYearId)
            .OrderBy(c => c.EducationLevel.SortOrder).ThenBy(c => c.GradeLevel.YearNumber).ThenBy(c => c.Department.Name).ThenBy(c => c.RoomNumber)
            .Select(c => new ClassroomOption
            {
                Id = c.Id, Name = c.Name, GradeName = c.GradeLevel.Name, DepartmentName = c.Department.Name,
                GradeLevelId = c.GradeLevelId, DepartmentId = c.DepartmentId, RoomNumber = c.RoomNumber
            }).ToListAsync();

        var rules = await db.GradeLevels.AsNoTracking().ToDictionaryAsync(g => g.Id, g => (Next: g.NextGradeLevelId, Final: g.IsFinal));
        foreach (var rule in r.Rules) rules[rule.GradeLevelId] = (rule.Graduate ? null : rule.NextGradeLevelId, rule.Graduate);

        var rows = students.Select(s =>
        {
            var (next, final) = rules.GetValueOrDefault(s.GradeId);
            var row = new PromotionStudentRow
            {
                StudentProfileId = s.Id, StudentCode = s.StudentCode, FullName = s.FullName, DepartmentId = s.DepartmentId,
                DepartmentName = s.Dept, GradeLevelId = s.GradeId, GradeName = s.Grade, ClassroomName = s.Room, StatusName = s.Status
            };
            if (final)
            {
                row.SuggestedAction = PromotionAction.Graduate;
            }
            else
            {
                // เลือกห้องเลขเดิมก่อน หากไม่มีจึงใช้ห้องแรกของชั้นและแผนกเดียวกัน
                var grade = next ?? s.GradeId;
                var candidates = targets.Where(c => c.DepartmentId == s.DepartmentId && c.GradeLevelId == grade).ToList();
                row.SuggestedAction = next is null ? PromotionAction.Retain : PromotionAction.Promote;
                row.SuggestedGradeLevelId = grade;
                row.SuggestedClassroomId = (candidates.FirstOrDefault(c => c.RoomNumber == s.RoomNumber) ?? candidates.FirstOrDefault())?.Id;
            }
            return row;
        }).ToList();

        return ServiceResult<PromotionStudentsResponse>.Ok(new PromotionStudentsResponse { Students = rows, TargetClassrooms = targets });
    }

    public async Task<ServiceResult<PromotionPreviewResponse>> PreviewAsync(PromotionConfirmRequest r)
    {
        var yearCheck = await ValidateYearsAsync(r.FromYearId, r.ToYearId);
        if (!yearCheck.Succeeded) return ServiceResult<PromotionPreviewResponse>.From(yearCheck);
        if (r.Items.Count == 0) return ServiceResult<PromotionPreviewResponse>.From(ServiceResult.Invalid("กรุณาเลือกนักเรียนอย่างน้อย 1 คน"));

        var planned = await PlanAsync(r);
        var years = await db.AcademicYears.AsNoTracking().Where(y => y.Id == r.FromYearId || y.Id == r.ToYearId).ToDictionaryAsync(y => y.Id, y => y.Name);

        var rows = planned.Select(p => new PromotionPreviewRow
        {
            StudentProfileId = p.Student.Id,
            StudentCode = p.Student.StudentCode,
            FullName = p.Student.User.FullName,
            ActionText = DisplayText.Promotion(p.Input.Action),
            FromText = $"{p.Student.Classroom?.Name ?? "-"} · {p.Student.Department.Name}",
            ToText = p.Input.Action switch
            {
                PromotionAction.Suspend => "พักการเรียน (คงห้องเดิมไว้)",
                PromotionAction.TransferOut => "ย้ายออกจากสถานศึกษา",
                PromotionAction.Graduate => "สำเร็จการศึกษา",
                _ => p.Target is null ? "-" : $"{p.Target.Name} · {p.Target.Department.Name}"
            },
            Note = p.Input.Note,
            Error = p.Error
        }).ToList();

        var response = new PromotionPreviewResponse
        {
            FromYear = years.GetValueOrDefault(r.FromYearId, ""),
            ToYear = years.GetValueOrDefault(r.ToYearId, ""),
            Total = rows.Count,
            ErrorCount = rows.Count(x => x.Error is not null),
            Summary = rows.GroupBy(x => x.ActionText).Select(g => new KeyValuePair<string, int>(g.Key, g.Count())).ToList(),
            Rows = rows
        };
        return ServiceResult<PromotionPreviewResponse>.Ok(response);
    }

    public async Task<ServiceResult<int>> ConfirmAsync(PromotionConfirmRequest r, string executedBy)
    {
        var yearCheck = await ValidateYearsAsync(r.FromYearId, r.ToYearId);
        if (!yearCheck.Succeeded) return ServiceResult<int>.From(yearCheck);
        if (r.Items.Count == 0) return ServiceResult<int>.From(ServiceResult.Invalid("กรุณาเลือกนักเรียนอย่างน้อย 1 คน"));

        await using var tx = await db.Database.BeginTransactionAsync();
        // ตรวจซ้ำบนข้อมูลล่าสุดภายใน transaction
        var planned = await PlanAsync(r);
        var failed = planned.Count(p => p.Error is not null);
        if (failed > 0)
            return ServiceResult<int>.From(ServiceResult.Invalid($"มีรายการไม่ถูกต้อง {failed} รายการ กรุณาตรวจสอบหน้าสรุปอีกครั้ง"));

        var statusIds = await db.StudentStatuses.AsNoTracking().Where(s => s.SystemCode != null)
            .ToDictionaryAsync(s => s.SystemCode!, s => s.Id);
        var now = clock.Now;
        var header = new PromotionHistory
        {
            FromAcademicYearId = r.FromYearId, ToAcademicYearId = r.ToYearId, ExecutedByUserId = executedBy,
            ExecutedAt = now, TotalStudents = planned.Count, Note = r.Note?.Trim()
        };
        db.PromotionHistories.Add(header);

        foreach (var p in planned)
        {
            var s = p.Student;
            var moved = p.Input.Action is PromotionAction.Promote or PromotionAction.Retain or PromotionAction.TransferDepartment;
            var toStatus = p.Input.Action switch
            {
                PromotionAction.Suspend => statusIds[StudentStatusCodes.Suspended],
                PromotionAction.TransferOut => statusIds[StudentStatusCodes.Transferred],
                PromotionAction.Graduate => statusIds[StudentStatusCodes.Graduated],
                _ => statusIds[StudentStatusCodes.Studying]
            };
            int? toClassroom = moved ? p.Target!.Id : p.Input.Action == PromotionAction.Suspend ? s.ClassroomId : null;
            var toDepartment = moved ? p.Target!.DepartmentId : s.DepartmentId;
            var toGrade = moved ? p.Target!.GradeLevelId : s.GradeLevelId;

            var detail = new PromotionHistoryDetail
            {
                PromotionHistory = header, StudentProfileId = s.Id, Action = p.Input.Action,
                FromDepartmentId = s.DepartmentId, FromGradeLevelId = s.GradeLevelId, FromClassroomId = s.ClassroomId, FromStudentStatusId = s.StudentStatusId,
                ToDepartmentId = toDepartment, ToGradeLevelId = toGrade, ToClassroomId = toClassroom, ToStudentStatusId = toStatus,
                Note = p.Input.Note?.Trim()
            };
            db.PromotionHistoryDetails.Add(detail);

            db.StudentAcademicHistories.Add(new StudentAcademicHistory
            {
                StudentProfileId = s.Id,
                ChangeType = p.Input.Action switch
                {
                    PromotionAction.Promote => AcademicChangeType.Promoted,
                    PromotionAction.Retain => AcademicChangeType.Retained,
                    PromotionAction.TransferDepartment => AcademicChangeType.DepartmentTransfer,
                    PromotionAction.Suspend => AcademicChangeType.Suspended,
                    PromotionAction.TransferOut => AcademicChangeType.TransferredOut,
                    _ => AcademicChangeType.Graduated
                },
                AcademicYearId = moved ? r.ToYearId : r.FromYearId,
                DepartmentId = toDepartment, GradeLevelId = toGrade, ClassroomId = toClassroom, StudentStatusId = toStatus,
                Note = p.Input.Note?.Trim(), CreatedAt = now, ChangedByUserId = executedBy,
                PromotionHistoryDetail = detail
            });

            s.DepartmentId = toDepartment;
            s.GradeLevelId = toGrade;
            s.ClassroomId = toClassroom;
            s.StudentStatusId = toStatus;
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ServiceResult<int>.Ok(header.Id, $"ดำเนินการเรียบร้อย {planned.Count} รายการ และบันทึกประวัติแล้ว");
    }

    public async Task<List<PromotionHistoryRow>> GetHistoryAsync(int take = 200)
    {
        var rows = await db.PromotionHistories.AsNoTracking().OrderByDescending(h => h.ExecutedAt).Take(take)
            .Select(h => new
            {
                h.Id, From = h.FromAcademicYear.Name, To = h.ToAcademicYear.Name, By = h.ExecutedByUser.FullName,
                h.ExecutedAt, h.TotalStudents, h.Note
            }).ToListAsync();
        return rows.Select(h => new PromotionHistoryRow
        {
            Id = h.Id, FromYear = h.From, ToYear = h.To, ExecutedBy = h.By, ExecutedAtText = ThaiDate.Format(h.ExecutedAt),
            Total = h.TotalStudents, Note = h.Note
        }).ToList();
    }

    public async Task<PromotionHistoryDetailsViewModel?> GetHistoryDetailsAsync(int id)
    {
        var h = await db.PromotionHistories.AsNoTracking().Where(x => x.Id == id)
            .Select(x => new
            {
                x.Id, From = x.FromAcademicYear.Name, To = x.ToAcademicYear.Name, By = x.ExecutedByUser.FullName,
                x.ExecutedAt, x.TotalStudents, x.Note
            }).FirstOrDefaultAsync();
        if (h is null) return null;

        var details = await db.PromotionHistoryDetails.AsNoTracking().Where(d => d.PromotionHistoryId == id)
            .OrderBy(d => d.StudentProfile.StudentCode)
            .Select(d => new
            {
                d.StudentProfileId, d.StudentProfile.StudentCode, d.StudentProfile.User.FullName, d.Action, d.Note,
                FromRoom = d.FromClassroom != null ? d.FromClassroom.Name : "-",
                FromDept = d.FromDepartment.Name,
                ToRoom = d.ToClassroom != null ? d.ToClassroom.Name : "-",
                ToDept = d.ToDepartment.Name,
                ToStatus = d.ToStudentStatus.Name
            }).ToListAsync();

        return new PromotionHistoryDetailsViewModel
        {
            Header = new PromotionHistoryRow
            {
                Id = h.Id, FromYear = h.From, ToYear = h.To, ExecutedBy = h.By, ExecutedAtText = ThaiDate.Format(h.ExecutedAt),
                Total = h.TotalStudents, Note = h.Note
            },
            Rows = details.Select(d => new PromotionPreviewRow
            {
                StudentProfileId = d.StudentProfileId, StudentCode = d.StudentCode, FullName = d.FullName,
                ActionText = DisplayText.Promotion(d.Action),
                FromText = $"{d.FromRoom} · {d.FromDept}",
                ToText = $"{d.ToRoom} · {d.ToDept} ({d.ToStatus})",
                Note = d.Note
            }).ToList()
        };
    }

    private async Task<ServiceResult> ValidateYearsAsync(int fromId, int toId)
    {
        var years = await db.AcademicYears.AsNoTracking().Where(y => y.Id == fromId || y.Id == toId).ToListAsync();
        var from = years.FirstOrDefault(y => y.Id == fromId);
        var to = years.FirstOrDefault(y => y.Id == toId);
        if (from is null || to is null) return ServiceResult.Invalid("กรุณาเลือกปีการศึกษาต้นทางและปลายทาง");
        if (to.Year <= from.Year) return ServiceResult.Invalid("ปีการศึกษาปลายทางต้องอยู่หลังปีต้นทาง");
        return ServiceResult.Ok();
    }

    private async Task<List<PlannedItem>> PlanAsync(PromotionConfirmRequest r)
    {
        var ids = r.Items.Select(i => i.StudentProfileId).Distinct().ToList();
        var students = await db.StudentProfiles
            .Include(s => s.User).Include(s => s.Department).Include(s => s.Classroom)
            .Where(s => ids.Contains(s.Id)).ToDictionaryAsync(s => s.Id);
        var roomIds = r.Items.Where(i => i.ToClassroomId is not null).Select(i => i.ToClassroomId!.Value).Distinct().ToList();
        var rooms = await db.Classrooms.AsNoTracking().Include(c => c.Department)
            .Where(c => roomIds.Contains(c.Id)).ToDictionaryAsync(c => c.Id);

        var seen = new HashSet<int>();
        var result = new List<PlannedItem>();
        foreach (var item in r.Items)
        {
            if (!students.TryGetValue(item.StudentProfileId, out var s)) continue;
            Classroom? target = null;
            if (item.ToClassroomId is not null) rooms.TryGetValue(item.ToClassroomId.Value, out target);
            string? error = null;

            if (!seen.Add(s.Id)) error = "นักเรียนซ้ำในรายการ";
            else if (s.Classroom is null || s.Classroom.AcademicYearId != r.FromYearId)
                error = "นักเรียนไม่ได้อยู่ในปีการศึกษาต้นทางแล้ว";
            else if (item.Note?.Length > 500) error = "หมายเหตุยาวเกิน 500 ตัวอักษร";
            else if (item.Action is PromotionAction.Promote or PromotionAction.Retain or PromotionAction.TransferDepartment)
            {
                if (target is null) error = "ยังไม่ได้เลือกห้องปลายทาง";
                else if (target.AcademicYearId != r.ToYearId) error = "ห้องปลายทางไม่ได้อยู่ในปีการศึกษาปลายทาง";
                else error = item.Action switch
                {
                    PromotionAction.Promote when target.DepartmentId != s.DepartmentId => "การเลื่อนชั้นต้องอยู่แผนกเดิม หากย้ายแผนกให้เลือกการดำเนินการย้ายแผนก",
                    PromotionAction.Promote when target.GradeLevelId == s.Classroom.GradeLevelId => "ห้องปลายทางเป็นชั้นปีเดิม หากซ้ำชั้นให้เลือกการดำเนินการซ้ำชั้น",
                    PromotionAction.Retain when target.DepartmentId != s.DepartmentId || target.GradeLevelId != s.Classroom.GradeLevelId => "การซ้ำชั้นต้องเลือกห้องชั้นปีและแผนกเดิม",
                    PromotionAction.TransferDepartment when target.DepartmentId == s.DepartmentId => "การย้ายแผนกต้องเลือกห้องของแผนกอื่น",
                    _ => null
                };
            }
            result.Add(new PlannedItem(s, item, target, error));
        }
        return result;
    }
}
