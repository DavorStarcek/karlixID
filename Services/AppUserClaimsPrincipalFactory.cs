using KarlixID.Web.Data;
using KarlixID.Web.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Options;
using System.Security.Claims;

namespace KarlixID.Web.Services
{
    public class AppUserClaimsPrincipalFactory :
        UserClaimsPrincipalFactory<ApplicationUser, IdentityRole>
    {
        private readonly ApplicationDbContext _db;

        public AppUserClaimsPrincipalFactory(
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IOptions<IdentityOptions> optionsAccessor,
            ApplicationDbContext db)
            : base(userManager, roleManager, optionsAccessor)
        {
            _db = db;
        }

        protected override async Task<ClaimsIdentity> GenerateClaimsAsync(ApplicationUser user)
        {
            var identity = await base.GenerateClaimsAsync(user);

            identity.AddClaim(new Claim("tenant_id", user.TenantId.ToString()));

            var tname = _db.Tenants.Where(t => t.Id == user.TenantId)
                                   .Select(t => t.Name)
                                   .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(tname))
                identity.AddClaim(new Claim("tenant_name", tname));

            return identity;
        }
    }
}
