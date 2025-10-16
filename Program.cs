using KarlixID.Web.Data;
using KarlixID.Web.Middleware;
using KarlixID.Web.Models;
using KarlixID.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.DataProtection;
// OPTIONAL za reverse proxy (Cloudflare/Nginx/HAProxy):
// using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// 📌 CONNECTION STRING
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));

// 📌 Identity setup
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

// 📌 Cookie settings
builder.Services.ConfigureApplicationCookie(opts =>
{
    opts.LoginPath = "/Identity/Account/Login";
    opts.LogoutPath = "/Identity/Account/Logout";
    opts.AccessDeniedPath = "/Identity/Account/AccessDenied";
    opts.SlidingExpiration = true;
    opts.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// 📌 Claims factory (tenant_id / tenant_name, itd.)
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AppUserClaimsPrincipalFactory>();

// 📌 Authorization politike
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("GlobalAdminOnly", p => p.RequireRole(AppRoleInfo.GlobalAdmin));
    options.AddPolicy("TenantAdminOrGlobal", p => p.RequireRole(AppRoleInfo.GlobalAdmin, AppRoleInfo.TenantAdmin));
});

// 📌 Middleware & Services
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<EmailService>();
builder.Services.AddTransient<ExcelExportService>();

// 📌 Lokalizacija
builder.Services.AddLocalization(opts => { opts.ResourcesPath = "Resources"; });

builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

// ✅ Razor Pages (za Identity UI)
builder.Services.AddRazorPages();


// ✅✅ DATA PROTECTION — spremanje ključeva na disk (stabilni auth cookies kroz restarte/IIS recycle)
var keysPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "KarlixID", "keys"
);
Directory.CreateDirectory(keysPath);

builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("KarlixID");

// // OPTIONAL ako si iza reverse proxy-a (Cloudflare/Nginx/HAProxy), uključi ForwardedHeaders:
// builder.Services.Configure<ForwardedHeadersOptions>(opts =>
// {
//     opts.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
// });

var app = builder.Build();

// 📌 Lokalizacija
var supportedCultures = new[] { new CultureInfo("hr"), new CultureInfo("en") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("hr"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.UseStaticFiles();
app.UseRouting();

// // OPTIONAL: ako si iza reverse proxy-a, ovo ide prije auth (skini komentar ako treba)
// app.UseForwardedHeaders();

// 📌 Multitenant Middleware
app.UseTenantResolver();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ✅ Razor Pages (Identity UI rute: /Identity/Account/...)
app.MapRazorPages();

// 📌 Seed uloga i admin korisnika
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();

    // Ako si strogo DB-first i ne koristiš EF migracije, ostavi zakomentirano:
    // context.Database.Migrate();

    // Role
    if (!await roleManager.RoleExistsAsync(AppRoleInfo.GlobalAdmin))
        await roleManager.CreateAsync(new IdentityRole(AppRoleInfo.GlobalAdmin));

    if (!await roleManager.RoleExistsAsync(AppRoleInfo.TenantAdmin))
        await roleManager.CreateAsync(new IdentityRole(AppRoleInfo.TenantAdmin));

    // GlobalAdmin
    var globalAdmin = await userManager.FindByEmailAsync("admin@karlix.eu");
    if (globalAdmin == null)
    {
        var user = new ApplicationUser
        {
            UserName = "admin@karlix.eu",
            Email = "admin@karlix.eu",
            EmailConfirmed = true,
            TenantId = Guid.Empty
        };

        var result = await userManager.CreateAsync(user, "Admin123!");
        if (result.Succeeded)
            await userManager.AddToRoleAsync(user, AppRoleInfo.GlobalAdmin);
    }

    // (Opcionalno) TenantAdmin za 'localhost'
    var localhostTenant = context.Tenants.FirstOrDefault(t => t.Hostname == "localhost");
    if (localhostTenant != null)
    {
        var taEmail = "tenant.admin@localhost";
        var ta = await userManager.FindByEmailAsync(taEmail);
        if (ta == null)
        {
            var u = new ApplicationUser
            {
                UserName = taEmail,
                Email = taEmail,
                EmailConfirmed = true,
                TenantId = localhostTenant.Id
            };
            var res = await userManager.CreateAsync(u, "TempPass123!");
            if (res.Succeeded)
                await userManager.AddToRoleAsync(u, AppRoleInfo.TenantAdmin);
        }
    }
}

app.Run();
