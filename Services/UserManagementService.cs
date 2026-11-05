using CollegeActivityCalendar.Data;
using CollegeActivityCalendar.Infrastructure;
using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CollegeActivityCalendar.Services;

public interface IUserManagementService
{
    Task<UserListViewModel> GetListAsync(UserFilter f, int pageSize = 20);
    Task<UserInput?> GetInputAsync(string id);
    Task<ServiceResult> CreateAsync(UserInput i);
    Task<ServiceResult> UpdateAsync(UserInput i);
    Task<ServiceResult> DeleteAsync(string id, string currentUserId);
    Task<ServiceResult> SetActiveAsync(string id, bool active, string currentUserId);
    Task<ServiceResult> ResetPasswordAsync(string id, string newPassword);
    Task<UserDetailsViewModel?> GetDetailsAsync(string id);
    Task<ServiceResult> TransferStudentAsync(StudentTransferInput i, string changedByUserId);
    Task<List<ClassroomOption>> GetAssignableClassroomsAsync(int? departmentId, int? gradeLevelId, int? includeId);
}

public class UserManagementService(
    ApplicationDbContext db,
    UserManager<ApplicationUser> users,
    IClock clock) : IUserManagementService
{
    public async Task<UserListViewModel> GetListAsync(UserFilter f, int pageSize = 20)
    {
        var q = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(f.Q))
        {
            var term = f.Q.Trim();
            q = q.Where(u => u.FullName.Contains(term) || u.Email!.Contains(term)
                || (u.StudentProfile != null && u.StudentProfile.StudentCode.Contains(term))
                || (u.TeacherProfile != null && u.TeacherProfile.EmployeeCode.Contains(term))
                || (u.StaffProfile != null && u.StaffProfile.EmployeeCode.Contains(term)));
        }
        if (f.IsActive is not null) q = q.Where(u => u.IsActive == f.IsActive);
        if (!string.IsNullOrEmpty(f.Role))
            q = q.Where(u => db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && r.Name == f.Role)));
        if (f.DepartmentId is not null)
        {
            q = q.Where(u => (u.StudentProfile != null && u.StudentProfile.DepartmentId == f.DepartmentId)
                || (u.TeacherProfile != null && u.TeacherProfile.Assignments.Any(a => a.DepartmentId == f.DepartmentId))
                || (u.StaffProfile != null && u.StaffProfile.DepartmentId == f.DepartmentId));
        }
        if (f.GradeLevelId is not null)
        {
            // ครูที่ดูแลทั้งแผนกถือว่าดูแลทุกชั้นปี
            q = q.Where(u => (u.StudentProfile != null && u.StudentProfile.GradeLevelId == f.GradeLevelId)
                || (u.TeacherProfile != null && u.TeacherProfile.Assignments.Any(a =>
                    (a.GradeLevelId == f.GradeLevelId && (f.DepartmentId == null || a.DepartmentId == f.DepartmentId))
                    || (a.GradeLevelId == null && a.ClassroomId == null && (f.DepartmentId == null || a.DepartmentId == f.DepartmentId)))));
        }
        if (f.ClassroomId is not null)
        {
            var room = await db.Classrooms.AsNoTracking().Where(c => c.Id == f.ClassroomId)
                .Select(c => new { c.DepartmentId, c.GradeLevelId }).FirstOrDefaultAsync();
            var dept = room?.DepartmentId;
            var grade = room?.GradeLevelId;
            q = q.Where(u => (u.StudentProfile != null && u.StudentProfile.ClassroomId == f.ClassroomId)
                || (u.TeacherProfile != null && (
                    u.TeacherProfile.AdvisedClassrooms.Any(c => c.Id == f.ClassroomId)
                    || u.TeacherProfile.Assignments.Any(a => a.ClassroomId == f.ClassroomId
                        || (a.ClassroomId == null && a.DepartmentId == dept && (a.GradeLevelId == null || a.GradeLevelId == grade))))));
        }

        var total = await q.CountAsync();
        var totalPages = Math.Max(1, (int)Math.Ceiling(total / (double)pageSize));
        var page = Math.Clamp(f.Page, 1, totalPages);

        var rows = await q.OrderBy(u => u.FullName).Skip((page - 1) * pageSize).Take(pageSize).Select(u => new
        {
            u.Id, u.FullName, Email = u.Email ?? "", u.IsActive,
            Role = db.UserRoles.Where(ur => ur.UserId == u.Id).Join(db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name).FirstOrDefault(),
            StudentCode = u.StudentProfile != null ? u.StudentProfile.StudentCode : null,
            StudentRoom = u.StudentProfile != null && u.StudentProfile.Classroom != null ? u.StudentProfile.Classroom.Name : null,
            StudentGrade = u.StudentProfile != null && u.StudentProfile.GradeLevel != null ? u.StudentProfile.GradeLevel.Name : null,
            StudentDept = u.StudentProfile != null ? u.StudentProfile.Department.Name : null,
            StudentStatus = u.StudentProfile != null ? u.StudentProfile.StudentStatus.Name : null,
            TeacherCode = u.TeacherProfile != null ? u.TeacherProfile.EmployeeCode : null,
            TeacherAssignments = u.TeacherProfile != null
                ? u.TeacherProfile.Assignments.Select(a => new
                {
                    Dept = a.Department.Name,
                    Grade = a.GradeLevel != null ? a.GradeLevel.Name : null,
                    Room = a.Classroom != null ? a.Classroom.Name : null
                }).ToList()
                : null,
            StaffCode = u.StaffProfile != null ? u.StaffProfile.EmployeeCode : null,
            StaffPosition = u.StaffProfile != null ? u.StaffProfile.Position : null
        }).ToListAsync();

        return new UserListViewModel
        {
            Page = page,
            TotalPages = totalPages,
            TotalCount = total,
            Items = rows.Select(u => new UserRow
            {
                Id = u.Id, FullName = u.FullName, Email = u.Email, IsActive = u.IsActive, Role = u.Role ?? "",
                Code = u.StudentCode ?? u.TeacherCode ?? u.StaffCode ?? "-",
                Detail = u.StudentCode is not null ? $"{u.StudentRoom ?? u.StudentGrade ?? "ยังไม่กำหนดห้อง"} · {u.StudentDept} · {u.StudentStatus}"
                    : u.TeacherCode is not null
                        ? (u.TeacherAssignments is { Count: > 0 }
                            ? string.Join(", ", u.TeacherAssignments.Select(a => DisplayText.Assignment(a.Dept, a.Grade, a.Room)))
                            : "ยังไม่กำหนดขอบเขต")
                    : u.StaffPosition ?? "-"
            }).ToList()
        };
    }

    public async Task<UserInput?> GetInputAsync(string id)
    {
        var u = await db.Users.AsNoTracking()
            .Include(x => x.StudentProfile).Include(x => x.StaffProfile)
            .Include(x => x.TeacherProfile).ThenInclude(t => t!.Assignments)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return null;
        return new UserInput
        {
            Id = u.Id, FullName = u.FullName, Email = u.Email, PhoneNumber = u.PhoneNumber, IsActive = u.IsActive,
            Role = (await users.GetRolesAsync(u)).FirstOrDefault(),
            Code = u.StudentProfile?.StudentCode ?? u.TeacherProfile?.EmployeeCode ?? u.StaffProfile?.EmployeeCode,
            DepartmentId = u.StudentProfile?.DepartmentId ?? u.StaffProfile?.DepartmentId,
            GradeLevelId = u.StudentProfile?.GradeLevelId,
            ClassroomId = u.StudentProfile?.ClassroomId,
            StudentStatusId = u.StudentProfile?.StudentStatusId,
            Assignments = u.TeacherProfile?.Assignments
                .Select(a => new TeacherAssignmentInput { DepartmentId = a.DepartmentId, GradeLevelId = a.GradeLevelId, ClassroomId = a.ClassroomId })
                .ToList() ?? [],
            Position = u.StaffProfile?.Position
        };
    }

    public async Task<ServiceResult> CreateAsync(UserInput i)
    {
        if (!AppRoles.All.Contains(i.Role)) return ServiceResult.Invalid("บทบาทไม่ถูกต้อง");
        var codeCheck = await CheckCodeAsync(i);
        if (!codeCheck.Succeeded) return codeCheck;

        Classroom? classroom = null;
        if (i.Role == AppRoles.Student && i.ClassroomId is not null)
        {
            classroom = await db.Classrooms.AsNoTracking().FirstOrDefaultAsync(c => c.Id == i.ClassroomId);
            if (classroom is null || classroom.DepartmentId != i.DepartmentId)
                return ServiceResult.Invalid("ห้องเรียนต้องอยู่ในแผนกที่เลือก");
        }

        var assignmentCheck = await NormalizeAssignmentsAsync(i);
        if (!assignmentCheck.Succeeded) return assignmentCheck;

        await using var tx = await db.Database.BeginTransactionAsync();
        var user = new ApplicationUser
        {
            UserName = i.Email, Email = i.Email, EmailConfirmed = true, FullName = i.FullName!.Trim(),
            PhoneNumber = i.PhoneNumber, IsActive = i.IsActive, CreatedAt = clock.Now
        };
        var created = await users.CreateAsync(user, i.Password!);
        if (!created.Succeeded) return ServiceResult.Invalid(IdentityMessage(created));
        await users.AddToRoleAsync(user, i.Role!);

        switch (i.Role)
        {
            case AppRoles.Student:
                var profile = new StudentProfile
                {
                    UserId = user.Id, StudentCode = i.Code!.Trim(), DepartmentId = i.DepartmentId!.Value,
                    ClassroomId = classroom?.Id, GradeLevelId = classroom?.GradeLevelId ?? i.GradeLevelId,
                    StudentStatusId = i.StudentStatusId!.Value, EnrollDate = clock.Today
                };
                db.StudentProfiles.Add(profile);
                await db.SaveChangesAsync();
                db.StudentAcademicHistories.Add(new StudentAcademicHistory
                {
                    StudentProfileId = profile.Id, ChangeType = AcademicChangeType.Enrolled,
                    AcademicYearId = classroom?.AcademicYearId, DepartmentId = profile.DepartmentId,
                    GradeLevelId = profile.GradeLevelId, ClassroomId = profile.ClassroomId,
                    StudentStatusId = profile.StudentStatusId, CreatedAt = clock.Now, ChangedByUserId = user.Id,
                    Note = "ขึ้นทะเบียนนักเรียนใหม่"
                });
                break;
            case AppRoles.Teacher:
                db.TeacherProfiles.Add(new TeacherProfile
                {
                    UserId = user.Id, EmployeeCode = i.Code!.Trim(),
                    Assignments = i.Assignments.Select(a => new TeacherAssignment
                    {
                        DepartmentId = a.DepartmentId!.Value, GradeLevelId = a.GradeLevelId, ClassroomId = a.ClassroomId
                    }).ToList()
                });
                break;
            case AppRoles.Staff:
                db.StaffProfiles.Add(new StaffProfile
                {
                    UserId = user.Id, EmployeeCode = i.Code!.Trim(), Position = i.Position, DepartmentId = i.DepartmentId
                });
                break;
        }
        await db.SaveChangesAsync();
        await tx.CommitAsync();
        return ServiceResult.Ok("เพิ่มผู้ใช้งานเรียบร้อยแล้ว");
    }

    public async Task<ServiceResult> UpdateAsync(UserInput i)
    {
        var user = await db.Users
            .Include(x => x.StudentProfile).Include(x => x.StaffProfile)
            .Include(x => x.TeacherProfile).ThenInclude(t => t!.Assignments)
            .FirstOrDefaultAsync(x => x.Id == i.Id);
        if (user is null) return ServiceResult.NotFound();
        var codeCheck = await CheckCodeAsync(i);
        if (!codeCheck.Succeeded) return codeCheck;
        var assignmentCheck = await NormalizeAssignmentsAsync(i);
        if (!assignmentCheck.Succeeded) return assignmentCheck;

        user.FullName = i.FullName!.Trim();
        user.PhoneNumber = i.PhoneNumber;
        if (!string.Equals(user.Email, i.Email, StringComparison.OrdinalIgnoreCase))
        {
            var setEmail = await users.SetEmailAsync(user, i.Email);
            if (!setEmail.Succeeded) return ServiceResult.Invalid(IdentityMessage(setEmail));
            var setName = await users.SetUserNameAsync(user, i.Email);
            if (!setName.Succeeded) return ServiceResult.Invalid(IdentityMessage(setName));
            user.EmailConfirmed = true;
        }

        if (user.StudentProfile is not null) user.StudentProfile.StudentCode = i.Code!.Trim();
        if (user.StaffProfile is not null)
        {
            user.StaffProfile.EmployeeCode = i.Code!.Trim();
            user.StaffProfile.Position = i.Position;
            user.StaffProfile.DepartmentId = i.DepartmentId;
        }
        if (user.TeacherProfile is not null)
        {
            user.TeacherProfile.EmployeeCode = i.Code!.Trim();
            var current = user.TeacherProfile.Assignments;
            foreach (var old in current.Where(c => !i.Assignments.Any(a => SameScope(a, c))).ToList()) current.Remove(old);
            foreach (var a in i.Assignments.Where(a => !current.Any(c => SameScope(a, c))))
            {
                current.Add(new TeacherAssignment { DepartmentId = a.DepartmentId!.Value, GradeLevelId = a.GradeLevelId, ClassroomId = a.ClassroomId });
            }
        }
        await db.SaveChangesAsync();
        return ServiceResult.Ok("แก้ไขผู้ใช้งานเรียบร้อยแล้ว");
    }

    public async Task<ServiceResult> DeleteAsync(string id, string currentUserId)
    {
        if (id == currentUserId) return ServiceResult.Invalid("ไม่สามารถลบบัญชีของตนเองได้");
        var user = await db.Users
            .Include(x => x.StudentProfile!).ThenInclude(p => p.Histories)
            .Include(x => x.StaffProfile)
            .Include(x => x.TeacherProfile)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (user is null) return ServiceResult.NotFound();
        // ประวัติการศึกษาต้องไม่หายไปกับการลบบัญชี
        if (user.StudentProfile?.Histories.Any(h => h.ChangeType != AcademicChangeType.Enrolled) == true)
            return ServiceResult.Invalid("ลบไม่ได้ เนื่องจากนักเรียนมีประวัติการศึกษา แนะนำให้ปิดการใช้งานแทน");
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            if (user.StudentProfile is not null)
            {
                db.StudentAcademicHistories.RemoveRange(user.StudentProfile.Histories);
                db.StudentProfiles.Remove(user.StudentProfile);
            }
            if (user.TeacherProfile is not null) db.TeacherProfiles.Remove(user.TeacherProfile);
            if (user.StaffProfile is not null) db.StaffProfiles.Remove(user.StaffProfile);
            await db.SaveChangesAsync();
            var result = await users.DeleteAsync(user);
            if (!result.Succeeded) return ServiceResult.Invalid(IdentityMessage(result));
            await tx.CommitAsync();
            return ServiceResult.Ok("ลบผู้ใช้งานเรียบร้อยแล้ว");
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return ServiceResult.Invalid("ลบไม่ได้ เนื่องจากผู้ใช้นี้มีประวัติในระบบ แนะนำให้ปิดการใช้งานแทน");
        }
    }

    public async Task<ServiceResult> SetActiveAsync(string id, bool active, string currentUserId)
    {
        if (id == currentUserId && !active) return ServiceResult.Invalid("ไม่สามารถปิดการใช้งานบัญชีของตนเองได้");
        var user = await users.FindByIdAsync(id);
        if (user is null) return ServiceResult.NotFound();
        user.IsActive = active;
        await users.UpdateAsync(user);
        // เปลี่ยน security stamp เพื่อให้ session เดิมหมดอายุ
        await users.UpdateSecurityStampAsync(user);
        return ServiceResult.Ok(active ? "เปิดการใช้งานเรียบร้อยแล้ว" : "ปิดการใช้งานเรียบร้อยแล้ว");
    }

    public async Task<ServiceResult> ResetPasswordAsync(string id, string newPassword)
    {
        var user = await users.FindByIdAsync(id);
        if (user is null) return ServiceResult.NotFound();
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, newPassword);
        return result.Succeeded ? ServiceResult.Ok("ตั้งรหัสผ่านใหม่เรียบร้อยแล้ว") : ServiceResult.Invalid(IdentityMessage(result));
    }

    public async Task<UserDetailsViewModel?> GetDetailsAsync(string id)
    {
        var u = await db.Users.AsNoTracking()
            .Include(x => x.StudentProfile!).ThenInclude(p => p.Department)
            .Include(x => x.StudentProfile!).ThenInclude(p => p.GradeLevel)
            .Include(x => x.StudentProfile!).ThenInclude(p => p.Classroom)
            .Include(x => x.StudentProfile!).ThenInclude(p => p.StudentStatus)
            .Include(x => x.StaffProfile!).ThenInclude(p => p.Department)
            .Include(x => x.TeacherProfile!).ThenInclude(t => t.Assignments).ThenInclude(a => a.Department)
            .Include(x => x.TeacherProfile!).ThenInclude(t => t.Assignments).ThenInclude(a => a.GradeLevel)
            .Include(x => x.TeacherProfile!).ThenInclude(t => t.Assignments).ThenInclude(a => a.Classroom)
            .Include(x => x.TeacherProfile!).ThenInclude(t => t.AdvisedClassrooms)
            .AsSplitQuery()
            .FirstOrDefaultAsync(x => x.Id == id);
        if (u is null) return null;

        var vm = new UserDetailsViewModel
        {
            Id = u.Id, FullName = u.FullName, Email = u.Email ?? "", PhoneNumber = u.PhoneNumber, IsActive = u.IsActive,
            Role = (await users.GetRolesAsync(u)).FirstOrDefault() ?? "",
            Code = u.StudentProfile?.StudentCode ?? u.TeacherProfile?.EmployeeCode ?? u.StaffProfile?.EmployeeCode,
            Position = u.StaffProfile?.Position,
            DepartmentName = u.StudentProfile?.Department.Name ?? u.StaffProfile?.Department?.Name,
            TeacherAssignments = u.TeacherProfile?.Assignments
                .OrderBy(a => a.Department.Name).ThenBy(a => a.GradeLevel?.YearNumber).ThenBy(a => a.Classroom?.Name)
                .Select(a => DisplayText.Assignment(a.Department.Name, a.GradeLevel?.Name, a.Classroom?.Name)).ToList() ?? [],
            AdvisedClassrooms = u.TeacherProfile?.AdvisedClassrooms.Select(c => c.Name).OrderBy(n => n).ToList() ?? []
        };
        if (u.TeacherProfile is { } teacher)
        {
            var teacherId = teacher.Id;
            vm.ResponsibleStudentCount = await db.StudentProfiles.CountAsync(s => s.StudentStatus.IsEnrolled
                && ((s.Classroom != null && s.Classroom.AdvisorTeacherProfileId == teacherId)
                    || db.TeacherAssignments.Any(a => a.TeacherProfileId == teacherId
                        && (a.ClassroomId != null
                            ? a.ClassroomId == s.ClassroomId
                            : a.DepartmentId == s.DepartmentId && (a.GradeLevelId == null || a.GradeLevelId == s.GradeLevelId)))));
        }
        if (u.StudentProfile is { } p)
        {
            vm.GradeName = p.GradeLevel?.Name;
            vm.ClassroomName = p.Classroom?.Name;
            vm.StatusName = p.StudentStatus.Name;
            vm.EnrollDateText = ThaiDate.Format(p.EnrollDate);
            vm.Transfer = new StudentTransferInput
            {
                UserId = u.Id, DepartmentId = p.DepartmentId, GradeLevelId = p.GradeLevelId, ClassroomId = p.ClassroomId, StudentStatusId = p.StudentStatusId
            };
            var (studentDept, studentGrade, studentRoom) = (p.DepartmentId, p.GradeLevelId, p.ClassroomId);
            vm.ResponsibleTeachers = await db.TeacherProfiles.AsNoTracking()
                .Where(t => (studentRoom != null && t.AdvisedClassrooms.Any(c => c.Id == studentRoom))
                    || t.Assignments.Any(a => a.ClassroomId != null
                        ? a.ClassroomId == studentRoom
                        : a.DepartmentId == studentDept && (a.GradeLevelId == null || a.GradeLevelId == studentGrade)))
                .OrderBy(t => t.User.FullName).Select(t => t.User.FullName).ToListAsync();
            var history = await db.StudentAcademicHistories.AsNoTracking()
                .Where(h => h.StudentProfileId == p.Id)
                .OrderByDescending(h => h.CreatedAt).ThenByDescending(h => h.Id)
                .Select(h => new
                {
                    h.ChangeType, h.Note, h.CreatedAt,
                    Year = h.AcademicYear != null ? h.AcademicYear.Name : null,
                    Dept = h.Department.Name,
                    Grade = h.GradeLevel != null ? h.GradeLevel.Name : null,
                    Room = h.Classroom != null ? h.Classroom.Name : null,
                    Status = h.StudentStatus.Name,
                    By = h.ChangedByUser.FullName
                }).ToListAsync();
            vm.History = history.Select(h => new AcademicHistoryRow
            {
                ChangeText = DisplayText.Change(h.ChangeType), YearName = h.Year ?? "-", DepartmentName = h.Dept,
                GradeName = h.Grade ?? "-", ClassroomName = h.Room ?? "-", StatusName = h.Status, Note = h.Note,
                CreatedAtText = ThaiDate.Format(h.CreatedAt), ChangedByName = h.By
            }).ToList();
        }
        return vm;
    }

    public async Task<ServiceResult> TransferStudentAsync(StudentTransferInput i, string changedBy)
    {
        var p = await db.StudentProfiles.FirstOrDefaultAsync(x => x.UserId == i.UserId);
        if (p is null) return ServiceResult.NotFound();
        var classroom = i.ClassroomId is null ? null : await db.Classrooms.AsNoTracking().FirstOrDefaultAsync(c => c.Id == i.ClassroomId);
        if (i.ClassroomId is not null && classroom is null) return ServiceResult.Invalid("ไม่พบห้องเรียนนี้");
        if (classroom is not null && classroom.DepartmentId != i.DepartmentId)
            return ServiceResult.Invalid("ห้องเรียนต้องอยู่ในแผนกที่เลือก");
        if (!await db.Departments.AnyAsync(d => d.Id == i.DepartmentId) || !await db.StudentStatuses.AnyAsync(s => s.Id == i.StudentStatusId))
            return ServiceResult.Invalid("ข้อมูลอ้างอิงไม่ถูกต้อง");

        var newGrade = classroom?.GradeLevelId ?? i.GradeLevelId;
        var departmentChanged = p.DepartmentId != i.DepartmentId;
        var classroomChanged = p.ClassroomId != i.ClassroomId || p.GradeLevelId != newGrade;
        var statusChanged = p.StudentStatusId != i.StudentStatusId;
        if (!departmentChanged && !classroomChanged && !statusChanged)
            return ServiceResult.Invalid("ไม่มีการเปลี่ยนแปลงข้อมูล");

        var status = await db.StudentStatuses.AsNoTracking().FirstAsync(s => s.Id == i.StudentStatusId);
        var type = departmentChanged ? AcademicChangeType.DepartmentTransfer
            : classroomChanged ? AcademicChangeType.ClassroomTransfer
            : status.SystemCode switch
            {
                StudentStatusCodes.Suspended => AcademicChangeType.Suspended,
                StudentStatusCodes.Transferred => AcademicChangeType.TransferredOut,
                StudentStatusCodes.Graduated => AcademicChangeType.Graduated,
                _ => AcademicChangeType.StatusChange
            };

        p.DepartmentId = i.DepartmentId!.Value;
        p.ClassroomId = i.ClassroomId;
        p.GradeLevelId = newGrade;
        p.StudentStatusId = i.StudentStatusId!.Value;

        db.StudentAcademicHistories.Add(new StudentAcademicHistory
        {
            StudentProfileId = p.Id, ChangeType = type, AcademicYearId = classroom?.AcademicYearId,
            DepartmentId = p.DepartmentId, GradeLevelId = p.GradeLevelId, ClassroomId = p.ClassroomId,
            StudentStatusId = p.StudentStatusId, Note = i.Note, CreatedAt = clock.Now, ChangedByUserId = changedBy
        });
        await db.SaveChangesAsync();
        return ServiceResult.Ok("บันทึกการเปลี่ยนแปลงและประวัติเรียบร้อยแล้ว");
    }

    public async Task<List<ClassroomOption>> GetAssignableClassroomsAsync(int? departmentId, int? gradeLevelId, int? includeId)
    {
        // แสดงปีปัจจุบันและปีถัดไป พร้อมห้องที่เลือกไว้เดิมแม้เป็นปีเก่า
        var current = await db.AcademicYears.AsNoTracking().Where(y => y.IsCurrent).Select(y => (int?)y.Year).FirstOrDefaultAsync();
        var rows = await db.Classrooms.AsNoTracking()
            .Where(c => c.Id == includeId
                || ((current == null || c.AcademicYear.Year >= current)
                    && (departmentId == null || c.DepartmentId == departmentId)
                    && (gradeLevelId == null || c.GradeLevelId == gradeLevelId)))
            .OrderBy(c => c.AcademicYear.Year).ThenBy(c => c.EducationLevel.SortOrder).ThenBy(c => c.GradeLevel.YearNumber)
            .ThenBy(c => c.Department.Name).ThenBy(c => c.RoomNumber)
            .Take(500)
            .Select(c => new { c.Id, c.Name, c.AcademicYear.Year, c.GradeLevelId, c.DepartmentId, Grade = c.GradeLevel.Name, Dept = c.Department.Name, c.RoomNumber })
            .ToListAsync();
        return rows.Select(c => new ClassroomOption
        {
            Id = c.Id,
            Name = c.Year == current ? c.Name : $"{c.Name} (ปี {c.Year})",
            GradeName = c.Grade, DepartmentName = c.Dept, GradeLevelId = c.GradeLevelId, DepartmentId = c.DepartmentId, RoomNumber = c.RoomNumber
        }).ToList();
    }

    private static bool SameScope(TeacherAssignmentInput a, TeacherAssignment c) =>
        a.DepartmentId == c.DepartmentId && a.GradeLevelId == c.GradeLevelId && a.ClassroomId == c.ClassroomId;

    /// <summary>ตรวจว่าแผนก ชั้นปี และห้องสอดคล้องกัน ตัดแถวที่ซ้ำ และให้ห้องเป็นตัวกำหนดแผนกกับชั้นปี</summary>
    private async Task<ServiceResult> NormalizeAssignmentsAsync(UserInput i)
    {
        if (i.Role != AppRoles.Teacher) { i.Assignments = []; return ServiceResult.Ok(); }

        var deptIds = i.Assignments.Select(a => a.DepartmentId!.Value).Distinct().ToList();
        var gradeIds = i.Assignments.Where(a => a.GradeLevelId != null).Select(a => a.GradeLevelId!.Value).Distinct().ToList();
        var roomIds = i.Assignments.Where(a => a.ClassroomId != null).Select(a => a.ClassroomId!.Value).Distinct().ToList();
        var rooms = await db.Classrooms.AsNoTracking().Where(c => roomIds.Contains(c.Id))
            .ToDictionaryAsync(c => c.Id, c => new { c.DepartmentId, c.GradeLevelId });

        if (await db.Departments.CountAsync(d => deptIds.Contains(d.Id)) != deptIds.Count
            || await db.GradeLevels.CountAsync(g => gradeIds.Contains(g.Id)) != gradeIds.Count
            || rooms.Count != roomIds.Count)
            return ServiceResult.Invalid("พบแผนก ชั้นปี หรือห้องเรียนที่ไม่มีอยู่ในระบบ");

        foreach (var a in i.Assignments.Where(a => a.ClassroomId != null))
        {
            var room = rooms[a.ClassroomId!.Value];
            if (room.DepartmentId != a.DepartmentId || (a.GradeLevelId != null && a.GradeLevelId != room.GradeLevelId))
                return ServiceResult.Invalid("ห้องเรียนต้องอยู่ในแผนกและชั้นปีที่เลือก");
            a.GradeLevelId = room.GradeLevelId;
        }
        i.Assignments = i.Assignments.DistinctBy(a => (a.DepartmentId, a.GradeLevelId, a.ClassroomId)).ToList();
        return ServiceResult.Ok();
    }

    private async Task<ServiceResult> CheckCodeAsync(UserInput i)
    {
        var code = i.Code?.Trim();
        if (string.IsNullOrEmpty(code)) return ServiceResult.Ok();
        var taken = i.Role switch
        {
            AppRoles.Student => await db.StudentProfiles.AnyAsync(p => p.StudentCode == code && p.UserId != i.Id),
            AppRoles.Teacher => await db.TeacherProfiles.AnyAsync(p => p.EmployeeCode == code && p.UserId != i.Id),
            AppRoles.Staff => await db.StaffProfiles.AnyAsync(p => p.EmployeeCode == code && p.UserId != i.Id),
            _ => false
        };
        return taken ? ServiceResult.Invalid("รหัสนี้ถูกใช้แล้ว") : ServiceResult.Ok();
    }

    public static string IdentityMessage(IdentityResult result) => string.Join(" ", result.Errors.Select(e => e.Code switch
    {
        "DuplicateUserName" or "DuplicateEmail" => "อีเมลนี้ถูกใช้แล้ว",
        "InvalidEmail" => "รูปแบบอีเมลไม่ถูกต้อง",
        "PasswordTooShort" => "รหัสผ่านต้องมีอย่างน้อย 8 ตัวอักษร",
        "PasswordRequiresDigit" => "รหัสผ่านต้องมีตัวเลข",
        "PasswordRequiresLower" => "รหัสผ่านต้องมีตัวพิมพ์เล็กภาษาอังกฤษ",
        "PasswordRequiresUpper" => "รหัสผ่านต้องมีตัวพิมพ์ใหญ่ภาษาอังกฤษ",
        "PasswordRequiresNonAlphanumeric" => "รหัสผ่านต้องมีอักขระพิเศษ",
        "PasswordMismatch" => "รหัสผ่านปัจจุบันไม่ถูกต้อง",
        _ => e.Description
    }).Distinct());
}
