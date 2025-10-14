using KarlixID.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace KarlixID.Web.Middleware
{
    public class TenantResolverMiddleware
    {
        private readonly RequestDelegate _next;

        public TenantResolverMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        // Napomena: dodatne parametre (npr. ApplicationDbContext) DI može injektirati u InvokeAsync
        public async Task InvokeAsync(HttpContext context, ApplicationDbContext db)
        {
            // Host bez porta (npr. "firma1.karlix.eu" ili "localhost")
            var host = context.Request.Host.Host?.ToLowerInvariant() ?? string.Empty;

            // Dopusti prolaz za statičke resurse i favicon bez tenanta
            var path = context.Request.Path.Value?.ToLowerInvariant() ?? string.Empty;
            if (path.StartsWith("/lib/") ||
                path.StartsWith("/css/") ||
                path.StartsWith("/js/") ||
                path.StartsWith("/identity/") ||
                path.Equals("/favicon.ico"))
            {
                await _next(context);
                return;
            }

            // Pokušaj naći aktivan tenant prema hostu
            var tenant = await db.Tenants
                                 .AsNoTracking()
                                 .FirstOrDefaultAsync(t => t.Hostname == host && t.IsActive);

            if (tenant == null)
            {
                // Jasna poruka kada tenant ne postoji ili je neaktivan
                context.Response.StatusCode = StatusCodes.Status404NotFound;
                await context.Response.WriteAsync("Tenant nije pronađen ili je deaktiviran.");
                return; // short-circuit
            }

            // Spremi u Items za ostatak requesta
            context.Items["Tenant"] = tenant;
            context.Items["TenantId"] = tenant.Id;     // ✅ dodano
            context.Items["TenantName"] = tenant.Name; // ✅ već koristimo u layoutu

            await _next(context);
        }
    }

    public static class TenantResolverMiddlewareExtensions
    {
        public static IApplicationBuilder UseTenantResolver(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<TenantResolverMiddleware>();
        }
    }
}
