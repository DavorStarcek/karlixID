using KarlixID.Web.Data;
using KarlixID.Web.Middleware;
using KarlixID.Web.Models;
using KarlixID.Web.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using OpenIddict.Abstractions;
using System.Globalization;
using static OpenIddict.Abstractions.OpenIddictConstants;

var builder = WebApplication.CreateBuilder(args);

// ========= DB =========
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    options.UseOpenIddict();
});

// ========= Identity =========
builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
{
    options.SignIn.RequireConfirmedAccount = false;
    options.Password.RequireDigit = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireLowercase = true;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders()
.AddDefaultUI();

// Mapiranje claim tipova na OIDC standard
builder.Services.Configure<IdentityOptions>(o =>
{
    o.ClaimsIdentity.UserIdClaimType = Claims.Subject; // "sub"
    o.ClaimsIdentity.UserNameClaimType = Claims.Name;  // "name"
    o.ClaimsIdentity.RoleClaimType = Claims.Role;      // "role"
    o.ClaimsIdentity.EmailClaimType = Claims.Email;    // "email"
});

// Cookie
builder.Services.ConfigureApplicationCookie(opts =>
{
    opts.LoginPath = "/Identity/Account/Login";
    opts.LogoutPath = "/Identity/Account/Logout";
    opts.AccessDeniedPath = "/Identity/Account/AccessDenied";
    opts.SlidingExpiration = true;
    opts.ExpireTimeSpan = TimeSpan.FromHours(8);
});

// Claims factory
builder.Services.AddScoped<IUserClaimsPrincipalFactory<ApplicationUser>, AppUserClaimsPrincipalFactory>();

// Authorization politike (NET 8)
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("GlobalAdminOnly", p => p.RequireRole(AppRoleInfo.GlobalAdmin))
    .AddPolicy("TenantAdminOrGlobal", p => p.RequireRole(AppRoleInfo.GlobalAdmin, AppRoleInfo.TenantAdmin));

// Ostali servisi
builder.Services.AddHttpContextAccessor();
builder.Services.AddTransient<EmailService>();
builder.Services.AddTransient<ExcelExportService>();

// Lokalizacija + MVC/Razor
builder.Services.AddLocalization(opts => { opts.ResourcesPath = "Resources"; });
builder.Services.AddControllersWithViews()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();
builder.Services.AddRazorPages();

// Data Protection ključevi
var keysPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "KarlixID", "keys"
);
Directory.CreateDirectory(keysPath);

builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("KarlixID");

// ========= OpenIddict 7.1 =========
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<ApplicationDbContext>();
    })
    .AddServer(options =>
    {
        options
            .SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetIntrospectionEndpointUris("/connect/introspect");

        options
            .AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow();

        options.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.Roles, Scopes.OfflineAccess);

        options
            .AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            // ⬇⬇⬇ MAKNUTO, neka OpenIddict sam odgovara na /connect/token
            // .EnableTokenEndpointPassthrough()
            .DisableTransportSecurityRequirement(); // DEV only

        options.DisableAccessTokenEncryption();
    })
    .AddValidation(options =>
    {
        options.UseLocalServer();
        options.UseAspNetCore();
    });

var app = builder.Build();

// ========= Pipeline =========
var supportedCultures = new[] { new CultureInfo("hr"), new CultureInfo("en") };
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new Microsoft.AspNetCore.Localization.RequestCulture("hr"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// Multitenant Middleware
app.UseTenantResolver();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapRazorPages();

// ========= Seed: uloge, admin i OpenIddict klijenti =========
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var appManager = services.GetRequiredService<IOpenIddictApplicationManager>();

    // context.Database.Migrate(); // ako želiš

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

    // ===== Karlix MVC (Portal) — DEV redirecti na https://localhost:5003 =====
    if (await appManager.FindByClientIdAsync("karlix_mvc") is null)
    {
        await appManager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "karlix_mvc",
            ClientSecret = "super-tajna-rijec-za-dev",
            DisplayName = "Karlix MVC (Portal)",
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,
            RedirectUris =
            {
                new Uri("https://localhost:5003/signin-oidc")
            },
            PostLogoutRedirectUris =
            {
                new Uri("https://localhost:5003/")
            },
            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,
                Permissions.Endpoints.Introspection,
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,
                Permissions.ResponseTypes.Code,
                Scopes.OpenId,
                Scopes.Profile,
                Scopes.Email,
                Scopes.Roles,
                Scopes.OfflineAccess
            }
        });
    }
}

app.Run();
