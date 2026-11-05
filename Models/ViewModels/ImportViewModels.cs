namespace CollegeActivityCalendar.Models.ViewModels;

public class ImportRowView
{
    /// <summary>แถวใน Excel เช่น "2" หรือ "2, 3" เมื่อครูหนึ่งคนมีหลายแถว</summary>
    public string Rows { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Detail { get; set; } = string.Empty;
    public string PasswordText { get; set; } = string.Empty;
    public List<string> Errors { get; set; } = [];
}

public class ImportPreviewResponse
{
    public string RoleText { get; set; } = string.Empty;
    public int Total { get; set; }
    public int ValidCount { get; set; }
    public int ErrorCount { get; set; }
    public List<ImportRowView> Rows { get; set; } = [];
}

public class ImportResultResponse
{
    public int Imported { get; set; }
    /// <summary>รหัสผ่านที่ระบบสุ่มให้ มีให้ดาวน์โหลดครั้งเดียวตอนนำเข้าเสร็จ ไม่ได้เก็บไว้ในระบบ</summary>
    public string? CredentialsFileBase64 { get; set; }
    public string? CredentialsFileName { get; set; }
    public int GeneratedPasswords { get; set; }
}

public class UserImportPageViewModel
{
    public string Role { get; set; } = "Student";
}
