using CollegeActivityCalendar.Data;
using CollegeActivityCalendar.Infrastructure;
using CollegeActivityCalendar.Models.Entities;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CollegeActivityCalendar.Services;

public interface ILookupService
{
    Task<List<SelectListItem>> DepartmentsAsync(bool activeOnly = true);
    Task<List<SelectListItem>> CategoriesAsync(bool activeOnly = true);
    Task<List<SelectListItem>> LocationsAsync();
    Task<List<SelectListItem>> ResponsibleUsersAsync();
    Task<List<SelectListItem>> AcademicYearsAsync(bool selectCurrent = false);
    Task<List<SelectListItem>> EducationLevelsAsync();
    Task<List<SelectListItem>> GradeLevelsAsync();
    Task<List<SelectListItem>> ClassroomsAsync(int? academicYearId);
    Task<List<SelectListItem>> StudentStatusesAsync();
    Task<List<SelectListItem>> TeachersAsync();
    List<SelectListItem> EventStatuses();
    List<SelectListItem> Roles();
}

public class LookupService(ApplicationDbContext db) : ILookupService
{
    public async Task<List<SelectListItem>> DepartmentsAsync(bool activeOnly = true) =>
        await db.Departments.AsNoTracking().Where(d => !activeOnly || d.IsActive).OrderBy(d => d.Name)
            .Select(d => new SelectListItem(d.Name, d.Id.ToString())).ToListAsync();

    public async Task<List<SelectListItem>> CategoriesAsync(bool activeOnly = true) =>
        await db.ActivityCategories.AsNoTracking().Where(c => !activeOnly || c.IsActive).OrderBy(c => c.Name)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToListAsync();

    public async Task<List<SelectListItem>> LocationsAsync() =>
        await db.Locations.AsNoTracking().Where(l => l.IsActive).OrderBy(l => l.Name)
            .Select(l => new SelectListItem(l.Name, l.Id.ToString())).ToListAsync();

    public async Task<List<SelectListItem>> ResponsibleUsersAsync() =>
        await (from u in db.Users.AsNoTracking()
               where u.IsActive
               join ur in db.UserRoles on u.Id equals ur.UserId
               join r in db.Roles on ur.RoleId equals r.Id
               where r.Name != AppRoles.Student
               orderby u.FullName
               select new SelectListItem(u.FullName, u.Id)).ToListAsync();

    public async Task<List<SelectListItem>> AcademicYearsAsync(bool selectCurrent = false) =>
        await db.AcademicYears.AsNoTracking().OrderByDescending(y => y.Year)
            .Select(y => new SelectListItem(y.Name, y.Id.ToString(), selectCurrent && y.IsCurrent)).ToListAsync();

    public async Task<List<SelectListItem>> EducationLevelsAsync() =>
        await db.EducationLevels.AsNoTracking().OrderBy(l => l.SortOrder)
            .Select(l => new SelectListItem(l.Name, l.Id.ToString())).ToListAsync();

    public async Task<List<SelectListItem>> GradeLevelsAsync() =>
        await db.GradeLevels.AsNoTracking().OrderBy(g => g.EducationLevel.SortOrder).ThenBy(g => g.YearNumber)
            .Select(g => new SelectListItem(g.Name, g.Id.ToString())).ToListAsync();

    public async Task<List<SelectListItem>> ClassroomsAsync(int? academicYearId)
    {
        var query = db.Classrooms.AsNoTracking().AsQueryable();
        query = academicYearId is not null
            ? query.Where(c => c.AcademicYearId == academicYearId)
            : query.Where(c => c.AcademicYear.IsCurrent);
        return await query.OrderBy(c => c.EducationLevel.SortOrder).ThenBy(c => c.GradeLevel.YearNumber).ThenBy(c => c.Department.Name).ThenBy(c => c.RoomNumber)
            .Select(c => new SelectListItem(c.Name, c.Id.ToString())).ToListAsync();
    }

    public async Task<List<SelectListItem>> StudentStatusesAsync() =>
        await db.StudentStatuses.AsNoTracking().OrderBy(s => s.SortOrder)
            .Select(s => new SelectListItem(s.Name, s.Id.ToString())).ToListAsync();

    public async Task<List<SelectListItem>> TeachersAsync() =>
        await db.TeacherProfiles.AsNoTracking().OrderBy(t => t.User.FullName)
            .Select(t => new SelectListItem(t.User.FullName, t.Id.ToString())).ToListAsync();

    public List<SelectListItem> EventStatuses() =>
        Enum.GetValues<EventStatus>().Select(s => new SelectListItem(DisplayText.Status(s), s.ToString())).ToList();

    public List<SelectListItem> Roles() =>
        AppRoles.All.Select(r => new SelectListItem($"{DisplayText.Role(r)} ({r})", r)).ToList();
}
