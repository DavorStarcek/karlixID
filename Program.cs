using KarlixID.Web.Data;
using KarlixID.Web.Middleware;
using KarlixID.Web.Models;
using KarlixID.Web.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using Microsoft.AspNetCore.DataProtection;
using OpenIddict.Abstractions;
using static OpenIddict.Abstractions.OpenIddictConstants;
// OPTIONAL za reverse proxy (Cloudflare/Nginx/HAProxy):
// using Microsoft.AspNetCore.HttpOverrides;

var builder = WebApplication.CreateBuilder(args);

// 📌 CONNECTION STRING
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
{
    options.UseSqlServer(connectionString);
    // Omogući OpenIddict-u da koristi isti DbContext
    options.UseOpenIddict();
});

// 📌 Identity setup
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

// 📌 Mapiranje claim tipova na OIDC standard
builder.Services.Configure<IdentityOptions>(o =>
{
    o.ClaimsIdentity.UserIdClaimType = Claims.Subject; // "sub"
    o.ClaimsIdentity.UserNameClaimType = Claims.Name;  // "name"
    o.ClaimsIdentity.RoleClaimType = Claims.Role;      // "role"
    o.ClaimsIdentity.EmailClaimType = Claims.Email;    // "email"
});

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

// 📌 Authorization politike (novi API u .NET 8+)
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("GlobalAdminOnly", policy =>
        policy.RequireRole(AppRoleInfo.GlobalAdmin))
    .AddPolicy("TenantAdminOrGlobal", policy =>
        policy.RequireRole(AppRoleInfo.GlobalAdmin, AppRoleInfo.TenantAdmin));

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

// ✅✅ DATA PROTECTION — stabilni auth cookies kroz restarte/IIS recycle
var keysPath = Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    "KarlixID", "keys"
);
Directory.CreateDirectory(keysPath);

builder.Services
    .AddDataProtection()
    .PersistKeysToFileSystem(new DirectoryInfo(keysPath))
    .SetApplicationName("KarlixID");

// ============ OpenIddict 7.1 ============

builder.Services.AddOpenIddict()
    // Core koristi EF Core + naš ApplicationDbContext
    .AddCore(options =>
    {
        options.UseEntityFrameworkCore()
               .UseDbContext<ApplicationDbContext>();
    })
    // Server dio (issuer)
    .AddServer(options =>
    {
        // Endpointi — u 7.1 iz kutije: authorize/token/introspect (nema zasebnog logout/userinfo)
        options
            .SetAuthorizationEndpointUris("/connect/authorize")
            .SetTokenEndpointUris("/connect/token")
            .SetIntrospectionEndpointUris("/connect/introspect");

        // Tokovi (7.1 API)
        options
            .AllowAuthorizationCodeFlow()
            .AllowRefreshTokenFlow();

        // Scope-ovi — uključuju i offline_access
        options.RegisterScopes(Scopes.OpenId, Scopes.Profile, Scopes.Email, Scopes.Roles, Scopes.OfflineAccess);

        // Dev certifikati
        options
            .AddDevelopmentEncryptionCertificate()
            .AddDevelopmentSigningCertificate();

        // ASP.NET Core integracija
        options.UseAspNetCore()
            .EnableAuthorizationEndpointPassthrough()
            .EnableTokenEndpointPassthrough()
            // ⚠️ samo za dev (nije za produkciju bez HTTPS-a)
            .DisableTransportSecurityRequirement();

        // Debug (lakše vidjeti payload)
        options.DisableAccessTokenEncryption();
    })
    // Validation (ako ova ista app želi validirati svoje tokene)
    .AddValidation(options =>
    {
        options.UseLocalServer();   // validiraj protiv lokalnog issuer-a
        options.UseAspNetCore();
    });

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

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();

// // OPTIONAL: ako si iza reverse proxy-a, ovo ide prije auth (skini komentar ako treba)
// app.UseForwardedHeaders();

// 📌 Multitenant Middleware
app.UseTenantResolver();

app.UseAuthentication();
app.UseAuthorization();

// ✅ Jedna default ruta
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// ✅ Razor Pages (Identity UI rute: /Identity/Account/…)
app.MapRazorPages();

// 📌 Seed uloga, admina i OpenIddict klijenata
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var context = services.GetRequiredService<ApplicationDbContext>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var appManager = services.GetRequiredService<IOpenIddictApplicationManager>();

    // Ako koristiš EF migracije, možeš aktivirati:
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

    // ===== OpenIddict klijenti =====

    // SPA (public, PKCE)
    if (await appManager.FindByClientIdAsync("karlixid_spa") is null)
    {
        await appManager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "karlixid_spa",
            DisplayName = "Karlix SPA",
            ClientType = ClientTypes.Public,
            ConsentType = ConsentTypes.Implicit, // za brzi start

            RedirectUris =
            {
                new Uri("https://localhost:5173/callback")
            },
            PostLogoutRedirectUris =
            {
                new Uri("https://localhost:5173/")
            },
            Permissions =
            {
                // endpoints
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,

                // grant types
                Permissions.GrantTypes.AuthorizationCode,
                Permissions.GrantTypes.RefreshToken,

                // response types
                Permissions.ResponseTypes.Code,

                // scopes
                Scopes.OpenId,
                Scopes.Profile,
                Scopes.Email,
                Scopes.Roles,
                Scopes.OfflineAccess
            },
            Requirements =
            {
                Requirements.Features.ProofKeyForCodeExchange
            }
        });
    }

    // MVC/confidential (dev primjer)
    if (await appManager.FindByClientIdAsync("karlix_mvc") is null)
    {
        await appManager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "karlix_mvc",
            ClientSecret = "super-tajna-rijec-za-dev",
            DisplayName = "Karlix MVC",
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,

            RedirectUris =
            {
                new Uri("https://localhost:5003/signin-oidc")
            },
            PostLogoutRedirectUris =
            {
                new Uri("https://localhost:5003/signout-callback-oidc")
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

    // ✅ Karlix Portal (MVC u produkciji na karlix.eu)
    if (await appManager.FindByClientIdAsync("karlix_portal") is null)
    {
        await appManager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "karlix_portal",
            ClientSecret = "super-tajna-rijec-za-dev",
            DisplayName = "Karlix Portal (MVC)",
            ClientType = ClientTypes.Confidential,
            ConsentType = ConsentTypes.Implicit,

            // Production URL-ovi:
            //RedirectUris =
            //{
            //    new Uri("https://karlix.eu/signin-oidc")
            //},
            //PostLogoutRedirectUris =
            //{
            //    new Uri("https://karlix.eu/")
            //},
            // Ako želiš testirati lokalno, možeš dodati i ovo:
            RedirectUris = { new Uri("https://localhost:5003/signin-oidc"), new Uri("https://karlix.eu/signin-oidc") },
            PostLogoutRedirectUris = { new Uri("https://localhost:5003/"), new Uri("https://karlix.eu/") },

            Permissions =
            {
                Permissions.Endpoints.Authorization,
                Permissions.Endpoints.Token,

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
