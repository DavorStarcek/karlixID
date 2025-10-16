using KarlixID.Web.Data;
using KarlixID.Web.Models;
using KarlixID.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarlixID.Web.Controllers
{
    [Authorize] // dodatno suzit ćemo po akcijama
    public class TenantUsersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _um;
        private readonly RoleManager<IdentityRole> _rm;

        public TenantUsersController(ApplicationDbContext db,
            UserManager<ApplicationUser> um,
            RoleManager<IdentityRole> rm)
        {
            _db = db;
            _um = um;
            _rm = rm;
        }

        // LISTA
        [Authorize(Policy = "TenantAdminOrGlobal")]
        public async Task<IActionResult> Index()
        {
            var me = await _um.GetUserAsync(User);
            var isGlobal = await _um.IsInRoleAsync(me!, AppRoleInfo.GlobalAdmin);

            // baza: Users + Tenants + Roles
            var q = from u in _db.Users.AsNoTracking()
                    join t in _db.Tenants.AsNoTracking() on u.TenantId equals t.Id into gj
                    from tt in gj.DefaultIfEmpty()
                    select new { u, tt };

            if (!isGlobal && me?.TenantId != null)
            {
                q = q.Where(x => x.u.TenantId == me!.TenantId);
            }

            var data = await q
                .OrderBy(x => x.tt!.Name)
                .ThenBy(x => x.u.Email)
                .Select(x => new TenantUserRow
                {
                    Id = x.u.Id,
                    Email = x.u.Email!,
                    UserName = x.u.UserName,
                    TenantId = x.u.TenantId,
                    TenantName = x.tt != null ? x.tt.Name : "(no tenant)",
                    EmailConfirmed = x.u.EmailConfirmed,
                    LockedOut = x.u.LockoutEnd != null && x.u.LockoutEnd > DateTimeOffset.UtcNow
                })
                .ToListAsync();

            // roles za svakog (odvojeno, da je store-agnostic)
            foreach (var row in data)
            {
                var usr = await _um.FindByIdAsync(row.Id);
                var roles = await _um.GetRolesAsync(usr!);
                row.RolesCsv = string.Join(", ", roles);
            }

            return View(data);
        }

        // CREATE (GET)
        [Authorize(Policy = "TenantAdminOrGlobal")]
        public async Task<IActionResult> Create()
        {
            var me = await _um.GetUserAsync(User);
            var isGlobal = await _um.IsInRoleAsync(me!, AppRoleInfo.GlobalAdmin);

            ViewBag.CanPickTenant = isGlobal;
            ViewBag.Tenants = await _db.Tenants
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();

            return View(new CreateTenantUserVM
            {
                TenantId = isGlobal ? null : me!.TenantId
            });
        }

        // CREATE (POST)
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "TenantAdminOrGlobal")]
        public async Task<IActionResult> Create(CreateTenantUserVM model)
        {
            var me = await _um.GetUserAsync(User);
            var isGlobal = await _um.IsInRoleAsync(me!, AppRoleInfo.GlobalAdmin);

            if (!ModelState.IsValid)
                return await Create();

            // Ako nije GlobalAdmin, forsiramo njegov tenant
            var tenantId = isGlobal ? model.TenantId : me!.TenantId;

            var user = new ApplicationUser
            {
                Email = model.Email,
                UserName = model.Email,
                EmailConfirmed = true,
                TenantId = tenantId ?? Guid.Empty // ako je null, stavi Guid.Empty
            };

            var result = await _um.CreateAsync(user, model.Password);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors)
                    ModelState.AddModelError("", e.Description);
                return await Create();
            }

            // Role
            if (isGlobal && model.MakeTenantAdmin)
            {
                if (!await _rm.RoleExistsAsync(AppRoleInfo.TenantAdmin))
                    await _rm.CreateAsync(new IdentityRole(AppRoleInfo.TenantAdmin));

                await _um.AddToRoleAsync(user, AppRoleInfo.TenantAdmin);
            }

            return RedirectToAction(nameof(Index));
        }

        // RESET PASSWORD (GET)
        [Authorize(Policy = "TenantAdminOrGlobal")]
        public async Task<IActionResult> ResetPassword(string id)
        {
            var me = await _um.GetUserAsync(User);
            var isGlobal = await _um.IsInRoleAsync(me!, AppRoleInfo.GlobalAdmin);

            var user = await _um.FindByIdAsync(id);
            if (user == null) return NotFound();

            // sigurnosna provjera tenant scope-a
            if (!isGlobal && me!.TenantId != user.TenantId)
                return Forbid();

            return View(new ResetPasswordVM { UserId = id });
        }

        // RESET PASSWORD (POST)
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "TenantAdminOrGlobal")]
        public async Task<IActionResult> ResetPassword(ResetPasswordVM model)
        {
            var me = await _um.GetUserAsync(User);
            var isGlobal = await _um.IsInRoleAsync(me!, AppRoleInfo.GlobalAdmin);

            var user = await _um.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            if (!isGlobal && me!.TenantId != user.TenantId)
                return Forbid();

            if (!ModelState.IsValid)
                return View(model);

            var token = await _um.GeneratePasswordResetTokenAsync(user);
            var res = await _um.ResetPasswordAsync(user, token, model.NewPassword);

            if (!res.Succeeded)
            {
                foreach (var e in res.Errors)
                    ModelState.AddModelError("", e.Description);
                return View(model);
            }

            return RedirectToAction(nameof(Index));
        }

        // LOCK/UNLOCK
        [Authorize(Policy = "TenantAdminOrGlobal")]
        public async Task<IActionResult> ToggleLock(string id)
        {
            var me = await _um.GetUserAsync(User);
            var isGlobal = await _um.IsInRoleAsync(me!, AppRoleInfo.GlobalAdmin);

            var user = await _um.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (!isGlobal && me!.TenantId != user.TenantId)
                return Forbid();

            var locked = user.LockoutEnd != null && user.LockoutEnd > DateTimeOffset.UtcNow;
            if (locked)
            {
                user.LockoutEnd = null;
            }
            else
            {
                user.LockoutEnd = DateTimeOffset.UtcNow.AddYears(100);
            }
            await _um.UpdateAsync(user);

            return RedirectToAction(nameof(Index));
        }
    }
}
