using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using ClosedXML.Excel;
using CollegeActivityCalendar.Data;
using CollegeActivityCalendar.Infrastructure;
using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Models.ViewModels;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CollegeActivityCalendar.Services;

public interface IUserImportService
{
    Task<byte[]> BuildTemplateAsync(string role);
    /// <summary>ตรวจไฟล์โดยไม่บันทึกอะไรลงฐานข้อมูล</summary>
    Task<ServiceResult<ImportPreviewResponse>> PreviewAsync(Stream file, string role);
    /// <summary>นำเข้าทั้งไฟล์ หากมีแถวไม่ถูกต้องจะไม่บันทึกเลยแม้แต่แถวเดียว</summary>
    Task<ServiceResult<ImportResultResponse>> ConfirmAsync(Stream file, string role, string executedByUserId);
}

public class UserImportService(ApplicationDbContext db, UserManager<ApplicationUser> users, IClock clock) : IUserImportService
{
    public const int MaxRows = 2000;
    private const string DataSheet = "นำเข้า";
    private const string ReferenceSheet = "ข้อมูลอ้างอิง";
    private const string ExampleSheet = "ตัวอย่าง";

    private const string HName = "ชื่อ-นามสกุล", HEmail = "อีเมล", HStudentCode = "รหัสนักเรียน", HStaffCode = "รหัสบุคลากร",
        HDept = "แผนก", HGrade = "ชั้นปี", HRoom = "ห้องเรียน", HYear = "ปีการศึกษา", HStatus = "สถานะ", HPhone = "เบอร์โทร", HPassword = "รหัสผ่าน";

    private sealed class Person
    {
        public List<int> Rows { get; } = [];
        public string FullName = "", Email = "", Code = "";
        public string? Phone, Password;
        public int DepartmentId;
        public int? GradeLevelId, ClassroomId, AcademicYearId, StudentStatusId;
        public string Detail = "";
        public List<(int DepartmentId, int? GradeLevelId, int? ClassroomId, string Text)> Assignments { get; } = [];
        public List<string> Errors { get; } = [];
    }

    private sealed record RoomRef(int Id, int DepartmentId, int GradeLevelId, int YearId, string Name);

    private sealed class Refs
    {
        public Dictionary<string, (int Id, string Name)> Departments { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, (int Id, string Name)> Grades { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, (int Id, string Name)> Statuses { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<(int Year, string Name), RoomRef> Rooms { get; } = [];
        public Dictionary<int, int> YearIdByYear { get; } = [];
        public int? CurrentYearId;
        public int? StudyingStatusId;
    }

    /// <summary>ให้ "ปวช 1" กับ "ปวช.1" เทียบเท่ากัน</summary>
    private static string Norm(string s) => new(s.Where(c => !char.IsWhiteSpace(c) && c != '.').ToArray());

    private async Task<Refs> LoadRefsAsync()
    {
        var r = new Refs();
        foreach (var d in await db.Departments.AsNoTracking().Where(d => d.IsActive).ToListAsync())
        {
            r.Departments[d.Code] = (d.Id, d.Name);
            r.Departments[d.Name] = (d.Id, d.Name);
        }
        foreach (var g in await db.GradeLevels.AsNoTracking().ToListAsync()) r.Grades[Norm(g.Name)] = (g.Id, g.Name);
        foreach (var s in await db.StudentStatuses.AsNoTracking().ToListAsync())
        {
            r.Statuses[s.Name] = (s.Id, s.Name);
            if (s.SystemCode == StudentStatusCodes.Studying) r.StudyingStatusId = s.Id;
        }
        foreach (var y in await db.AcademicYears.AsNoTracking().ToListAsync())
        {
            r.YearIdByYear[y.Year] = y.Id;
            if (y.IsCurrent) r.CurrentYearId = y.Id;
        }
        var rooms = await db.Classrooms.AsNoTracking().Select(c => new RoomRef(c.Id, c.DepartmentId, c.GradeLevelId, c.AcademicYearId, c.Name)).ToListAsync();
        foreach (var c in rooms) r.Rooms[(c.YearId, Norm(c.Name))] = c;
        return r;
    }

    private static bool IsTeacher(string role) => role == AppRoles.Teacher;

    private static string[] Headers(string role) => IsTeacher(role)
        ? [HName, HEmail, HStaffCode, HDept, HGrade, HRoom, HYear, HPhone, HPassword]
        : [HName, HEmail, HStudentCode, HDept, HGrade, HRoom, HYear, HStatus, HPhone, HPassword];

    private static string[] RequiredHeaders(string role) => [HName, HEmail, IsTeacher(role) ? HStaffCode : HStudentCode];

    public async Task<byte[]> BuildTemplateAsync(string role)
    {
        var teacher = IsTeacher(role);
        var headers = Headers(role);
        var required = RequiredHeaders(role);
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add(DataSheet);

        // ข้อมูลอ้างอิงสำหรับดรอปดาวน์
        var depts = await db.Departments.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.Name).Select(d => new { d.Code, d.Name }).ToListAsync();
        var grades = await db.GradeLevels.AsNoTracking().OrderBy(g => g.EducationLevel.SortOrder).ThenBy(g => g.YearNumber).Select(g => g.Name).ToListAsync();
        var statuses = await db.StudentStatuses.AsNoTracking().OrderBy(s => s.SortOrder).Select(s => s.Name).ToListAsync();
        var rooms = await db.Classrooms.AsNoTracking().Where(c => c.AcademicYear.IsCurrent)
            .OrderBy(c => c.EducationLevel.SortOrder).ThenBy(c => c.GradeLevel.YearNumber).ThenBy(c => c.Department.Name).ThenBy(c => c.RoomNumber)
            .Select(c => c.Name).ToListAsync();
        var currentYear = await db.AcademicYears.AsNoTracking().Where(y => y.IsCurrent).Select(y => (int?)y.Year).FirstOrDefaultAsync();

        var refSheet = wb.Worksheets.Add(ReferenceSheet);
        string[] refHeaders = ["รหัสแผนก", "ชื่อแผนก", "ชั้นปี", $"ห้องเรียน (ปี {currentYear})", "สถานะนักเรียน"];
        for (var i = 0; i < refHeaders.Length; i++) StyleHeader(refSheet.Cell(1, i + 1).SetValue(refHeaders[i]));
        for (var i = 0; i < depts.Count; i++)
        {
            refSheet.Cell(i + 2, 1).SetValue(depts[i].Code);
            refSheet.Cell(i + 2, 2).SetValue(depts[i].Name);
        }
        for (var i = 0; i < grades.Count; i++) refSheet.Cell(i + 2, 3).SetValue(grades[i]);
        for (var i = 0; i < rooms.Count; i++) refSheet.Cell(i + 2, 4).SetValue(rooms[i]);
        for (var i = 0; i < statuses.Count; i++) refSheet.Cell(i + 2, 5).SetValue(statuses[i]);
        refSheet.Columns().AdjustToContents();

        for (var i = 0; i < headers.Length; i++)
        {
            var label = required.Contains(headers[i]) ? headers[i] + " *" : headers[i];
            StyleHeader(ws.Cell(1, i + 1).SetValue(label));
            var column = ws.Column(i + 1);
            column.Width = headers[i] is HName or HEmail or HDept or HRoom ? 26 : 16;
            // เก็บเป็นข้อความ เพื่อไม่ให้ Excel ตัดเลข 0 นำหน้า
            ws.Range(2, i + 1, MaxRows + 1, i + 1).Style.NumberFormat.Format = "@";

            var list = headers[i] switch
            {
                HDept when depts.Count > 0 => refSheet.Range(2, 2, depts.Count + 1, 2),
                HGrade when grades.Count > 0 => refSheet.Range(2, 3, grades.Count + 1, 3),
                HRoom when rooms.Count > 0 => refSheet.Range(2, 4, rooms.Count + 1, 4),
                HStatus when statuses.Count > 0 => refSheet.Range(2, 5, statuses.Count + 1, 5),
                _ => null
            };
            if (list is not null)
            {
                var validation = ws.Range(2, i + 1, MaxRows + 1, i + 1).CreateDataValidation();
                validation.List(list);
                validation.IgnoreBlanks = true;
                // ยังพิมพ์ค่าอื่นได้ เช่น รหัสแผนก หรือห้องของปีอื่น ระบบตรวจซ้ำตอนอัปโหลด
                validation.ShowErrorMessage = false;
            }
        }
        ws.SheetView.FreezeRows(1);

        var example = wb.Worksheets.Add(ExampleSheet);
        for (var i = 0; i < headers.Length; i++) StyleHeader(example.Cell(1, i + 1).SetValue(headers[i]));
        var firstRoom = rooms.FirstOrDefault() ?? "";
        var firstDept = depts.FirstOrDefault()?.Name ?? "";
        var firstGrade = grades.FirstOrDefault() ?? "";
        var rows = teacher
            ? new[]
            {
                new[] { "ครูตัวอย่าง ใจดี", "teacher.a@college.example", "T1001", firstDept, "", "", "", "0810000001", "" },
                new[] { "", "teacher.a@college.example", "", firstDept, firstGrade, "", "", "", "" },
                new[] { "ครูสาธิต ตั้งใจ", "teacher.b@college.example", "T1002", "", "", firstRoom, "", "", "" }
            }
            : new[]
            {
                new[] { "นักเรียนตัวอย่าง ขยันเรียน", "student.a@college.example", "6901001", firstDept, firstGrade, "", "", "กำลังศึกษา", "0810000002", "" },
                new[] { "นักเรียนสาธิต ตั้งใจ", "student.b@college.example", "6901002", "", "", firstRoom, "", "", "", "" }
            };
        for (var r = 0; r < rows.Length; r++)
            for (var c = 0; c < rows[r].Length; c++) example.Cell(r + 2, c + 1).SetValue(rows[r][c]);
        var noteRow = rows.Length + 3;
        string[] notes = teacher
            ?
            [
                "คัดลอกรูปแบบจากแผ่นนี้ไปกรอกที่แผ่น \"นำเข้า\" ระบบอ่านเฉพาะแผ่นนั้น",
                "ครูที่ดูแลหลายขอบเขต ให้กรอกหลายแถวด้วยอีเมลเดียวกัน แถวถัดไปเว้นชื่อและรหัสได้",
                "เลือกแค่แผนก = ดูแลทั้งแผนก, แผนก + ชั้นปี = ทุกห้องของชั้นปีนั้น, ใส่ห้องเรียน = เฉพาะห้องนั้น",
                "ไม่ใส่ปีการศึกษา = ใช้ปีปัจจุบัน, ไม่ใส่รหัสผ่าน = ระบบสุ่มให้และให้ดาวน์โหลดตอนนำเข้าเสร็จ"
            ]
            :
            [
                "คัดลอกรูปแบบจากแผ่นนี้ไปกรอกที่แผ่น \"นำเข้า\" ระบบอ่านเฉพาะแผ่นนั้น",
                "ใส่ห้องเรียนแล้วเว้นแผนกและชั้นปีได้ ถ้าไม่ใส่ห้องเรียนต้องใส่แผนก",
                "ไม่ใส่ปีการศึกษา = ใช้ปีปัจจุบัน, ไม่ใส่สถานะ = กำลังศึกษา",
                "ไม่ใส่รหัสผ่าน = ระบบสุ่มให้และให้ดาวน์โหลดตอนนำเข้าเสร็จ"
            ];
        foreach (var note in notes) example.Cell(noteRow++, 1).SetValue(note);
        example.Columns(1, headers.Length).Width = 24;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    private static void StyleHeader(IXLCell cell)
    {
        cell.Style.Font.Bold = true;
        cell.Style.Font.FontColor = XLColor.White;
        cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#172D3B");
    }

    public async Task<ServiceResult<ImportPreviewResponse>> PreviewAsync(Stream file, string role)
    {
        var plan = await ParseAsync(file, role);
        if (!plan.Succeeded) return ServiceResult<ImportPreviewResponse>.From(plan);
        var people = plan.Data!;
        return ServiceResult<ImportPreviewResponse>.Ok(new ImportPreviewResponse
        {
            RoleText = DisplayText.Role(role),
            Total = people.Count,
            ErrorCount = people.Count(p => p.Errors.Count > 0),
            ValidCount = people.Count(p => p.Errors.Count == 0),
            Rows = people.Select(p => new ImportRowView
            {
                Rows = string.Join(", ", p.Rows),
                FullName = p.FullName,
                Email = p.Email,
                Code = p.Code,
                Detail = p.Detail,
                PasswordText = string.IsNullOrEmpty(p.Password) ? "ระบบสุ่มให้" : "ตามไฟล์",
                Errors = p.Errors
            }).ToList()
        });
    }

    public async Task<ServiceResult<ImportResultResponse>> ConfirmAsync(Stream file, string role, string executedByUserId)
    {
        var plan = await ParseAsync(file, role);
        if (!plan.Succeeded) return ServiceResult<ImportResultResponse>.From(plan);
        var people = plan.Data!;
        var failed = people.Count(p => p.Errors.Count > 0);
        if (failed > 0)
            return ServiceResult<ImportResultResponse>.From(ServiceResult.Invalid($"มีข้อมูลไม่ถูกต้อง {failed} รายการ กรุณาแก้ไขไฟล์แล้วตรวจสอบใหม่ ยังไม่มีการบันทึกข้อมูล"));

        var generated = new List<(string Name, string Email, string Code, string Password)>();
        var now = clock.Now;
        try
        {
            await using var tx = await db.Database.BeginTransactionAsync();
            foreach (var p in people)
            {
                var password = p.Password;
                if (string.IsNullOrEmpty(password))
                {
                    password = GeneratePassword();
                    generated.Add((p.FullName, p.Email, p.Code, password));
                }
                var user = new ApplicationUser
                {
                    UserName = p.Email, Email = p.Email, EmailConfirmed = true, FullName = p.FullName,
                    PhoneNumber = p.Phone, IsActive = true, CreatedAt = now
                };
                var created = await users.CreateAsync(user, password);
                if (!created.Succeeded)
                    return ServiceResult<ImportResultResponse>.From(ServiceResult.Invalid($"{p.Email}: {UserManagementService.IdentityMessage(created)} ยังไม่มีการบันทึกข้อมูล"));
                await users.AddToRoleAsync(user, role);

                if (IsTeacher(role))
                {
                    db.TeacherProfiles.Add(new TeacherProfile
                    {
                        UserId = user.Id,
                        EmployeeCode = p.Code,
                        Assignments = p.Assignments
                            .DistinctBy(a => (a.DepartmentId, a.GradeLevelId, a.ClassroomId))
                            .Select(a => new TeacherAssignment { DepartmentId = a.DepartmentId, GradeLevelId = a.GradeLevelId, ClassroomId = a.ClassroomId })
                            .ToList()
                    });
                }
                else
                {
                    var profile = new StudentProfile
                    {
                        UserId = user.Id, StudentCode = p.Code, DepartmentId = p.DepartmentId, GradeLevelId = p.GradeLevelId,
                        ClassroomId = p.ClassroomId, StudentStatusId = p.StudentStatusId!.Value, EnrollDate = clock.Today
                    };
                    profile.Histories.Add(new StudentAcademicHistory
                    {
                        ChangeType = AcademicChangeType.Enrolled, AcademicYearId = p.AcademicYearId, DepartmentId = p.DepartmentId,
                        GradeLevelId = p.GradeLevelId, ClassroomId = p.ClassroomId, StudentStatusId = p.StudentStatusId!.Value,
                        CreatedAt = now, ChangedByUserId = executedByUserId, Note = "นำเข้าจาก Excel"
                    });
                    db.StudentProfiles.Add(profile);
                }
            }
            await db.SaveChangesAsync();
            await tx.CommitAsync();
        }
        catch (DbUpdateException)
        {
            db.ChangeTracker.Clear();
            return ServiceResult<ImportResultResponse>.From(ServiceResult.Invalid("บันทึกไม่สำเร็จ อาจมีผู้เพิ่มรหัสหรืออีเมลเดียวกันระหว่างนำเข้า กรุณาตรวจสอบไฟล์ใหม่ ยังไม่มีการบันทึกข้อมูล"));
        }

        var result = new ImportResultResponse { Imported = people.Count, GeneratedPasswords = generated.Count };
        if (generated.Count > 0)
        {
            result.CredentialsFileBase64 = Convert.ToBase64String(BuildCredentialsFile(generated, role));
            result.CredentialsFileName = IsTeacher(role) ? "teacher-passwords.xlsx" : "student-passwords.xlsx";
        }
        return ServiceResult<ImportResultResponse>.Ok(result, $"นำเข้า{DisplayText.Role(role)}เรียบร้อย {people.Count:N0} คน");
    }

    private async Task<ServiceResult<List<Person>>> ParseAsync(Stream file, string role)
    {
        if (role is not (AppRoles.Student or AppRoles.Teacher))
            return ServiceResult<List<Person>>.From(ServiceResult.Invalid("ประเภทการนำเข้าไม่ถูกต้อง"));

        XLWorkbook wb;
        try { wb = new XLWorkbook(file); }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            return ServiceResult<List<Person>>.From(ServiceResult.Invalid("อ่านไฟล์ไม่ได้ กรุณาใช้ไฟล์ Excel นามสกุล .xlsx"));
        }

        using (wb)
        {
            if (!wb.Worksheets.TryGetWorksheet(DataSheet, out var ws)) ws = wb.Worksheets.First();

            // อ่านตำแหน่งคอลัมน์จากหัวตาราง จึงสลับคอลัมน์ได้
            var columns = new Dictionary<string, int>();
            foreach (var cell in ws.Row(1).CellsUsed())
            {
                var header = cell.GetFormattedString().Replace("*", "").Trim();
                if (header.Length > 0) columns.TryAdd(header, cell.Address.ColumnNumber);
            }
            var missing = RequiredHeaders(role).Where(h => !columns.ContainsKey(h)).ToList();
            if (missing.Count > 0)
            {
                var hint = (IsTeacher(role) ? columns.ContainsKey(HStudentCode) : columns.ContainsKey(HStaffCode))
                    ? " ไฟล์นี้อาจเป็นแม่แบบของอีกประเภท" : "";
                return ServiceResult<List<Person>>.From(ServiceResult.Invalid($"ไม่พบคอลัมน์: {string.Join(", ", missing)}{hint}"));
            }

            string Read(int row, string header) =>
                columns.TryGetValue(header, out var col) ? ws.Cell(row, col).GetFormattedString().Trim() : "";

            var refs = await LoadRefsAsync();
            var teacher = IsTeacher(role);
            var codeHeader = teacher ? HStaffCode : HStudentCode;
            var people = new List<Person>();
            var byEmail = new Dictionary<string, Person>(StringComparer.OrdinalIgnoreCase);
            var byCode = new Dictionary<string, Person>(StringComparer.OrdinalIgnoreCase);
            var lastRow = ws.LastRowUsed()?.RowNumber() ?? 1;
            var dataRows = 0;

            for (var row = 2; row <= lastRow; row++)
            {
                if (columns.Values.All(c => string.IsNullOrWhiteSpace(ws.Cell(row, c).GetFormattedString()))) continue;
                if (++dataRows > MaxRows)
                    return ServiceResult<List<Person>>.From(ServiceResult.Invalid($"นำเข้าได้ไม่เกิน {MaxRows:N0} แถวต่อไฟล์ กรุณาแบ่งไฟล์"));

                var name = Read(row, HName);
                var email = Read(row, HEmail);
                var code = Read(row, codeHeader);
                var phone = Read(row, HPhone);
                var password = Read(row, HPassword);

                Person? person = null;
                if (email.Length > 0) byEmail.TryGetValue(email, out person);
                var isContinuation = person is not null;

                if (isContinuation && !teacher)
                {
                    // นักเรียนมีได้คนละหนึ่งแถว
                    person!.Errors.Add($"แถว {row}: อีเมลซ้ำกับแถว {person.Rows[0]}");
                    person.Rows.Add(row);
                    continue;
                }

                if (!isContinuation)
                {
                    person = new Person { FullName = name, Email = email, Code = code, Phone = phone.Length > 0 ? phone : null, Password = password.Length > 0 ? password : null };
                    people.Add(person);
                    if (email.Length > 0) byEmail[email] = person;
                    ValidateIdentity(person, row, codeHeader, byCode);
                }
                else
                {
                    if (name.Length > 0 && !string.Equals(name, person!.FullName, StringComparison.Ordinal))
                        person.Errors.Add($"แถว {row}: ชื่อไม่ตรงกับแถว {person.Rows[0]} ที่ใช้อีเมลเดียวกัน");
                    if (code.Length > 0 && !string.Equals(code, person!.Code, StringComparison.OrdinalIgnoreCase))
                        person.Errors.Add($"แถว {row}: รหัสไม่ตรงกับแถว {person.Rows[0]} ที่ใช้อีเมลเดียวกัน");
                }
                person!.Rows.Add(row);

                if (teacher) ReadAssignment(person, row, Read(row, HDept), Read(row, HGrade), Read(row, HRoom), Read(row, HYear), refs);
                else ReadStudent(person, row, Read(row, HDept), Read(row, HGrade), Read(row, HRoom), Read(row, HYear), Read(row, HStatus), refs);
            }

            if (people.Count == 0)
                return ServiceResult<List<Person>>.From(ServiceResult.Invalid($"ไม่พบข้อมูลในแผ่น \"{ws.Name}\" กรุณากรอกตั้งแต่แถวที่ 2"));

            foreach (var p in people.Where(p => teacher))
                p.Detail = string.Join(", ", p.Assignments.Select(a => a.Text).Distinct());

            await CheckExistingAsync(people, role);
            await CheckPasswordsAsync(people);
            return ServiceResult<List<Person>>.Ok(people);
        }
    }

    private void ValidateIdentity(Person p, int row, string codeHeader, Dictionary<string, Person> byCode)
    {
        if (p.FullName.Length == 0) p.Errors.Add($"แถว {row}: ไม่ได้กรอก{HName}");
        else if (p.FullName.Length > 150) p.Errors.Add($"แถว {row}: ชื่อยาวเกิน 150 ตัวอักษร");

        var allowed = users.Options.User.AllowedUserNameCharacters;
        if (p.Email.Length == 0) p.Errors.Add($"แถว {row}: ไม่ได้กรอก{HEmail}");
        else if (p.Email.Length > 256 || !new EmailAddressAttribute().IsValid(p.Email) || p.Email.Any(c => !allowed.Contains(c)))
            p.Errors.Add($"แถว {row}: รูปแบบอีเมลไม่ถูกต้อง");

        if (p.Code.Length == 0) p.Errors.Add($"แถว {row}: ไม่ได้กรอก{codeHeader}");
        else if (p.Code.Length > 20) p.Errors.Add($"แถว {row}: รหัสยาวเกิน 20 ตัวอักษร");
        else if (byCode.TryGetValue(p.Code, out var other)) p.Errors.Add($"แถว {row}: รหัสซ้ำกับแถว {other.Rows[0]}");
        else byCode[p.Code] = p;

        if (p.Phone?.Length > 20) p.Errors.Add($"แถว {row}: เบอร์โทรยาวเกิน 20 ตัวอักษร");
    }

    /// <summary>หาห้องเรียนจากชื่อและปีการศึกษา คืน false เมื่อมีข้อผิดพลาด</summary>
    private static bool TryResolveRoom(Person p, int row, string room, string year, Refs refs, out RoomRef? found)
    {
        found = null;
        if (room.Length == 0) return true;
        var yearId = refs.CurrentYearId;
        if (year.Length > 0)
        {
            if (!int.TryParse(year, out var y) || !refs.YearIdByYear.TryGetValue(y, out var id))
            {
                p.Errors.Add($"แถว {row}: ไม่พบ{HYear} \"{year}\"");
                return false;
            }
            yearId = id;
        }
        if (yearId is null || !refs.Rooms.TryGetValue((yearId.Value, Norm(room)), out found))
        {
            p.Errors.Add($"แถว {row}: ไม่พบ{HRoom} \"{room}\"{(year.Length > 0 ? $" ในปี {year}" : " ในปีการศึกษาปัจจุบัน")}");
            return false;
        }
        return true;
    }

    /// <summary>หาแผนกและชั้นปี และตรวจว่าตรงกับห้องเรียน คืน false เมื่อมีข้อผิดพลาด</summary>
    private static bool ResolveScope(Person p, int row, string dept, string grade, RoomRef? room, bool roomGiven, Refs refs,
        out int departmentId, out int? gradeId, out string departmentName, out string? gradeName)
    {
        departmentId = 0; gradeId = null; departmentName = ""; gradeName = null;
        var ok = true;

        if (grade.Length > 0)
        {
            if (refs.Grades.TryGetValue(Norm(grade), out var g)) { gradeId = g.Id; gradeName = g.Name; }
            else { p.Errors.Add($"แถว {row}: ไม่พบ{HGrade} \"{grade}\""); ok = false; }
        }
        if (dept.Length > 0)
        {
            if (refs.Departments.TryGetValue(dept, out var d)) { departmentId = d.Id; departmentName = d.Name; }
            else { p.Errors.Add($"แถว {row}: ไม่พบ{HDept} \"{dept}\""); ok = false; }
        }
        else if (!roomGiven)
        {
            p.Errors.Add($"แถว {row}: ต้องกรอก{HDept} หรือ{HRoom}");
            ok = false;
        }

        if (room is not null && ok)
        {
            if (dept.Length > 0 && room.DepartmentId != departmentId)
            {
                p.Errors.Add($"แถว {row}: ห้อง {room.Name} ไม่ได้อยู่ในแผนก {departmentName}");
                ok = false;
            }
            if (gradeId is not null && room.GradeLevelId != gradeId)
            {
                p.Errors.Add($"แถว {row}: ห้อง {room.Name} ไม่ได้อยู่ในชั้นปี {gradeName}");
                ok = false;
            }
            if (ok)
            {
                departmentId = room.DepartmentId;
                departmentName = refs.Departments.Values.FirstOrDefault(d => d.Id == room.DepartmentId).Name ?? "";
                gradeId = room.GradeLevelId;
                gradeName = refs.Grades.Values.FirstOrDefault(g => g.Id == room.GradeLevelId).Name;
            }
        }
        return ok;
    }

    private static void ReadStudent(Person p, int row, string dept, string grade, string room, string year, string status, Refs refs)
    {
        var roomOk = TryResolveRoom(p, row, room, year, refs, out var found);
        var scopeOk = ResolveScope(p, row, dept, grade, found, room.Length > 0, refs, out var departmentId, out var gradeId, out var departmentName, out var gradeName);

        string statusName;
        if (status.Length > 0)
        {
            if (refs.Statuses.TryGetValue(status, out var s)) { p.StudentStatusId = s.Id; statusName = s.Name; }
            else { p.Errors.Add($"แถว {row}: ไม่พบ{HStatus} \"{status}\""); statusName = status; }
        }
        else
        {
            p.StudentStatusId = refs.StudyingStatusId;
            statusName = refs.Statuses.Values.FirstOrDefault(s => s.Id == refs.StudyingStatusId).Name ?? "";
        }

        if (roomOk && scopeOk)
        {
            p.DepartmentId = departmentId;
            p.GradeLevelId = gradeId;
            p.ClassroomId = found?.Id;
            p.AcademicYearId = found?.YearId;
            p.Detail = $"{found?.Name ?? gradeName ?? "ยังไม่กำหนดห้อง"} · {departmentName} · {statusName}";
        }
    }

    private static void ReadAssignment(Person p, int row, string dept, string grade, string room, string year, Refs refs)
    {
        if (!TryResolveRoom(p, row, room, year, refs, out var found)) return;
        if (!ResolveScope(p, row, dept, grade, found, room.Length > 0, refs, out var departmentId, out var gradeId, out var departmentName, out var gradeName)) return;
        p.Assignments.Add((departmentId, gradeId, found?.Id, DisplayText.Assignment(departmentName, gradeName, found?.Name)));
    }

    private async Task CheckExistingAsync(List<Person> people, string role)
    {
        var teacher = IsTeacher(role);
        var emails = people.Where(p => p.Email.Length > 0).Select(p => users.NormalizeEmail(p.Email)).Distinct().ToList();
        var codes = people.Where(p => p.Code.Length > 0).Select(p => p.Code).Distinct().ToList();

        var existingEmails = new HashSet<string>(StringComparer.Ordinal);
        foreach (var chunk in emails.Chunk(500))
        {
            var part = chunk.ToList();
            existingEmails.UnionWith(await db.Users.AsNoTracking().Where(u => part.Contains(u.NormalizedEmail!)).Select(u => u.NormalizedEmail!).ToListAsync());
        }
        var existingCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var chunk in codes.Chunk(500))
        {
            var part = chunk.ToList();
            existingCodes.UnionWith(teacher
                ? await db.TeacherProfiles.AsNoTracking().Where(t => part.Contains(t.EmployeeCode)).Select(t => t.EmployeeCode).ToListAsync()
                : await db.StudentProfiles.AsNoTracking().Where(s => part.Contains(s.StudentCode)).Select(s => s.StudentCode).ToListAsync());
        }

        foreach (var p in people)
        {
            if (p.Email.Length > 0 && existingEmails.Contains(users.NormalizeEmail(p.Email)))
                p.Errors.Add($"แถว {p.Rows[0]}: อีเมลนี้มีในระบบแล้ว");
            if (p.Code.Length > 0 && existingCodes.Contains(p.Code))
                p.Errors.Add($"แถว {p.Rows[0]}: รหัสนี้มีในระบบแล้ว");
        }
    }

    private async Task CheckPasswordsAsync(List<Person> people)
    {
        var probe = new ApplicationUser();
        foreach (var p in people.Where(p => !string.IsNullOrEmpty(p.Password)))
        {
            foreach (var validator in users.PasswordValidators)
            {
                var result = await validator.ValidateAsync(users, probe, p.Password!);
                if (!result.Succeeded)
                    p.Errors.Add($"แถว {p.Rows[0]}: {UserManagementService.IdentityMessage(result)}");
            }
        }
    }

    private static string GeneratePassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ", lower = "abcdefghijkmnpqrstuvwxyz", digits = "23456789";
        var chars = RandomNumberGenerator.GetItems<char>(upper + lower + digits, 12);
        chars[0] = RandomNumberGenerator.GetItems<char>(upper, 1)[0];
        chars[1] = RandomNumberGenerator.GetItems<char>(lower, 1)[0];
        chars[2] = RandomNumberGenerator.GetItems<char>(digits, 1)[0];
        RandomNumberGenerator.Shuffle<char>(chars);
        return new string(chars);
    }

    private static byte[] BuildCredentialsFile(List<(string Name, string Email, string Code, string Password)> rows, string role)
    {
        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("รหัสผ่านเริ่มต้น");
        string[] headers = [HName, HEmail, IsTeacher(role) ? HStaffCode : HStudentCode, "รหัสผ่านเริ่มต้น"];
        for (var i = 0; i < headers.Length; i++) StyleHeader(ws.Cell(1, i + 1).SetValue(headers[i]));
        for (var r = 0; r < rows.Count; r++)
        {
            ws.Cell(r + 2, 1).SetValue(rows[r].Name);
            ws.Cell(r + 2, 2).SetValue(rows[r].Email);
            ws.Cell(r + 2, 3).SetValue(rows[r].Code);
            ws.Cell(r + 2, 4).SetValue(rows[r].Password);
        }
        ws.Columns().AdjustToContents();
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
