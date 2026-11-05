using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json.Serialization;
using System.Text.Unicode;
using CollegeActivityCalendar.Data;
using CollegeActivityCalendar.Infrastructure;
using CollegeActivityCalendar.Models.Entities;
using CollegeActivityCalendar.Repositories;
using CollegeActivityCalendar.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.WebEncoders;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("ไม่พบ ConnectionStrings:DefaultConnection")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.User.RequireUniqueEmail = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(10);
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders()
    .AddClaimsPrincipalFactory<AppClaimsPrincipalFactory>();

builder.Services.Configure<SecurityStampValidatorOptions>(o => o.ValidationInterval = TimeSpan.FromMinutes(5));

builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromHours(10);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    // AJAX ต้องได้รหัสสถานะจริง ไม่ใช่ redirect ไปหน้า HTML
    options.Events.OnRedirectToLogin = ctx =>
    {
        if (ctx.Request.Headers.XRequestedWith == "XMLHttpRequest") ctx.Response.StatusCode = StatusCodes.Status401Unauthorized;
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = ctx =>
    {
        if (ctx.Request.Headers.XRequestedWith == "XMLHttpRequest") ctx.Response.StatusCode = StatusCodes.Status403Forbidden;
        else ctx.Response.Redirect(ctx.RedirectUri);
        return Task.CompletedTask;
    };
});

builder.Services.AddAuthorization(options =>
{
    // ทุกหน้าต้องเข้าสู่ระบบ ยกเว้นที่ระบุ [AllowAnonymous]
    options.FallbackPolicy = new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build();
});

builder.Services.AddAntiforgery(o => o.HeaderName = "RequestVerificationToken");

builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
        options.Filters.Add<AjaxExceptionFilter>();
        var m = options.ModelBindingMessageProvider;
        m.SetValueMustNotBeNullAccessor(_ => "กรุณากรอกข้อมูล");
        m.SetAttemptedValueIsInvalidAccessor((_, field) => $"ค่าของ {field} ไม่ถูกต้อง");
        m.SetValueIsInvalidAccessor(_ => "ค่าไม่ถูกต้อง");
        m.SetMissingBindRequiredValueAccessor(field => $"กรุณาระบุ {field}");
        m.SetUnknownValueIsInvalidAccessor(field => $"ค่าของ {field} ไม่ถูกต้อง");
        m.SetNonPropertyAttemptedValueIsInvalidAccessor(_ => "ค่าไม่ถูกต้อง");
        m.SetNonPropertyUnknownValueIsInvalidAccessor(() => "ค่าไม่ถูกต้อง");
        m.SetNonPropertyValueMustBeANumberAccessor(() => "กรุณากรอกเป็นตัวเลข");
        m.SetValueMustBeANumberAccessor(field => $"{field} ต้องเป็นตัวเลข");
    })
    .AddJsonOptions(o =>
    {
        o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
        o.JsonSerializerOptions.Converters.Add(new TimeOnlyJsonConverter());
    });

// แสดงภาษาไทยใน HTML โดยไม่ encode เป็น &#xxxx;
builder.Services.Configure<WebEncoderOptions>(o => o.TextEncoderSettings = new TextEncoderSettings(UnicodeRanges.All));

builder.Services.AddMemoryCache();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<IClock, CollegeClock>();
builder.Services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
builder.Services.AddScoped<IEventRepository, EventRepository>();
builder.Services.AddScoped<IUserScopeProvider, UserScopeProvider>();
builder.Services.AddScoped<IEventAccessService, EventAccessService>();
builder.Services.AddScoped<ILookupService, LookupService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<IAcademicStructureService, AcademicStructureService>();
builder.Services.AddScoped<IMasterDataService, MasterDataService>();
builder.Services.AddScoped<IUserManagementService, UserManagementService>();
builder.Services.AddScoped<IUserImportService, UserImportService>();
builder.Services.AddScoped<IStudentPromotionService, StudentPromotionService>();
builder.Services.AddScoped<IDashboardService, DashboardService>();
builder.Services.AddScoped<IReportService, ReportService>();
builder.Services.AddScoped<ISettingsService, SettingsService>();
builder.Services.AddScoped<DbSeeder>();
builder.Services.AddHostedService<ScheduledEventPublisher>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
    if (app.Configuration.GetValue("Database:AutoMigrate", false))
    {
        logger.LogInformation("กำลังอัปเดตฐานข้อมูล (Database:AutoMigrate)");
        await db.Database.MigrateAsync();
    }
    if (app.Configuration.GetValue("Seed:Enabled", true))
    {
        if (await db.Database.CanConnectAsync() && !(await db.Database.GetPendingMigrationsAsync()).Any())
            await scope.ServiceProvider.GetRequiredService<DbSeeder>().SeedAsync();
        else
            logger.LogWarning("ข้ามการ Seed: ยังเชื่อมต่อฐานข้อมูลไม่ได้ หรือยังไม่ได้รัน Migration");
    }
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
    app.UseHttpsRedirection();
}
else
{
    app.UseDeveloperExceptionPage();
}

// ใช้ culture แบบกลาง ป้องกันปี พ.ศ. ของ th-TH ทำให้ค่าวันที่ ISO ในฟอร์มผิด การแสดงผลไทยอยู่ที่ ThaiDate
var culture = CultureInfo.GetCultureInfo("en-US");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = [culture],
    SupportedUICultures = [culture]
});

app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");
app.UseStaticFiles();
app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute("areas", "{area:exists}/{controller=MasterData}/{action=Index}/{id?}");
app.MapControllerRoute("default", "{controller=Dashboard}/{action=Index}/{id?}");

app.Run();

public partial class Program;
