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

        public async Task Invoke(HttpContext context, ApplicationDbContext db)
        {
            var host = context.Request.Host.Host.ToLower();

            var tenant = await db.Tenants.FirstOrDefaultAsync(t => t.Hostname == host);

            if (tenant != null)
            {
                context.Items["Tenant"] = tenant;
            }

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
