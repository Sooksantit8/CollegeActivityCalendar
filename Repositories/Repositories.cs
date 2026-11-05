using CollegeActivityCalendar.Data;
using CollegeActivityCalendar.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace CollegeActivityCalendar.Repositories;

public interface IRepository<T> where T : class
{
    IQueryable<T> Query();
    Task<T?> FindAsync(params object[] keys);
    void Add(T entity);
    void Remove(T entity);
    Task SaveChangesAsync();
}

public class Repository<T>(ApplicationDbContext db) : IRepository<T> where T : class
{
    public IQueryable<T> Query() => db.Set<T>();
    public async Task<T?> FindAsync(params object[] keys) => await db.Set<T>().FindAsync(keys);
    public void Add(T entity) => db.Set<T>().Add(entity);
    public void Remove(T entity) => db.Set<T>().Remove(entity);
    public Task SaveChangesAsync() => db.SaveChangesAsync();
}

public interface IEventRepository : IRepository<Event>
{
    IQueryable<Event> QueryWithDetails();
    Task<Event?> GetForUpdateAsync(int id);
}

public class EventRepository(ApplicationDbContext db) : Repository<Event>(db), IEventRepository
{
    private readonly ApplicationDbContext _db = db;

    public IQueryable<Event> QueryWithDetails() => _db.Events.AsNoTracking()
        .Include(e => e.ActivityCategory)
        .Include(e => e.Location)
        .Include(e => e.ResponsibleUser)
        .Include(e => e.Targets).ThenInclude(t => t.Departments).ThenInclude(d => d.Department)
        .Include(e => e.Targets).ThenInclude(t => t.Classrooms).ThenInclude(c => c.Classroom)
        .Include(e => e.Targets).ThenInclude(t => t.Users).ThenInclude(u => u.User)
        .AsSplitQuery();

    public Task<Event?> GetForUpdateAsync(int id) => _db.Events
        .Include(e => e.Targets).ThenInclude(t => t.Departments)
        .Include(e => e.Targets).ThenInclude(t => t.Classrooms)
        .Include(e => e.Targets).ThenInclude(t => t.Users)
        .AsSplitQuery()
        .FirstOrDefaultAsync(e => e.Id == id);
}
