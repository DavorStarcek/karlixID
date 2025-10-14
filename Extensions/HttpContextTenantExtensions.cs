using Microsoft.AspNetCore.Http;
using System;

namespace KarlixID.Web.Extensions
{
    public static class HttpContextTenantExtensions
    {
        public static Guid? GetTenantId(this HttpContext ctx)
        {
            if (ctx.Items.TryGetValue("TenantId", out var v) && v is Guid g)
                return g;
            return null;
        }

        public static string? GetTenantName(this HttpContext ctx)
        {
            if (ctx.Items.TryGetValue("TenantName", out var v) && v is string s)
                return s;
            return null;
        }
    }
}
