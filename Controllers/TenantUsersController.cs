using KarlixID.Web.Data;
using KarlixID.Web.Models;
using KarlixID.Web.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarlixID.Web.Controllers
{
    [Authorize]
    public class TenantUsersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _um;
        private readonly RoleManager<IdentityRole> _rm;

        public TenantUsersController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> um,
            RoleManager<IdentityRole> rm)
        {
            _db = db;
            _um = um;
            _rm = rm;
        }

        // LISTA + search/filter
        // /TenantUsers?q=nešto&tenantId=GUID&onlyLocked=true
        [Authorize(Policy = "TenantAdminOrGlobal")]
        public async Task<IActionResult> Index(string? q, Guid? tenantId, bool? onlyLocked)
        {
            var me = await _um.GetUserAsync(User);
            var isGlobal = await _um.IsInRoleAsync(me!, AppRoleInfo.GlobalAdmin);

            // baza tenanata za filter dropdown
            ViewBag.Tenants = await _db.Tenants
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();

            ViewBag.FilterQ = q;
            ViewBag.FilterTenantId = tenantId;
            ViewBag.FilterOnlyLocked = onlyLocked ?? false;

            // Polazište – svi Identity useri
            var usersQ = _um.Users.AsNoTracking();

            // Scope: ako nije GlobalAdmin → vidi samo svoj tenant
            if (!isGlobal && me?.TenantId != null)
                usersQ = usersQ.Where(x => x.TenantId == me!.TenantId);

            // Primijeni filtere
            if (!string.IsNullOrWhiteSpace(q))
                usersQ = usersQ.Where(x =>
                    (x.Email != null && x.Email.Contains(q)) ||
                    (x.UserName != null && x.UserName.Contains(q)) ||
                    (x.DisplayName != null && x.DisplayName.Contains(q)));

            if (tenantId.HasValue)
                usersQ = usersQ.Where(x => x.TenantId == tenantId.Value);

            if (onlyLocked == true)
                usersQ = usersQ.Where(x => x.LockoutEnd != null && x.LockoutEnd > DateTimeOffset.UtcNow);

            // Left-join tenanata za prikaz naziva
            var qJoined =
                from u in usersQ
                join t in _db.Tenants.AsNoTracking() on u.TenantId equals t.Id into gj
                from tt in gj.DefaultIfEmpty()
                select new { u, tt };

            var data = await qJoined
                .OrderBy(x => x.tt != null ? x.tt.Name : "(no tenant)")
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

            // Dohvati role
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
            {
                ViewBag.CanPickTenant = isGlobal;
                ViewBag.Tenants = await _db.Tenants.OrderBy(t => t.Name)
                    .Select(t => new { t.Id, t.Name })
                    .ToListAsync();
                return View(model);
            }

            // Ako je GlobalAdmin → korisnik može biti bez tenanta (Guid.Empty).
            // Inače tenant je od prijavljenog admina.
            var targetTenantId = isGlobal ? Guid.Empty : (me!.TenantId ?? Guid.Empty);

            // ako je Global i odabrao TenantId u formi, koristi to
            if (isGlobal && model.TenantId.HasValue)
                targetTenantId = model.TenantId.Value;

            // lozinka
            var password = string.IsNullOrWhiteSpace(model.TempPassword)
                ? "Temp123!"
                : model.TempPassword!;

            var user = new ApplicationUser
            {
                Email = model.Email,
                UserName = model.Email,
                EmailConfirmed = true,
                TenantId = targetTenantId
            };

            var result = await _um.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                foreach (var e in result.Errors)
                    ModelState.AddModelError("", e.Description);

                ViewBag.CanPickTenant = isGlobal;
                ViewBag.Tenants = await _db.Tenants.OrderBy(t => t.Name)
                    .Select(t => new { t.Id, t.Name })
                    .ToListAsync();
                return View(model);
            }

            if (isGlobal && model.MakeTenantAdmin)
            {
                if (!await _rm.RoleExistsAsync(AppRoleInfo.TenantAdmin))
                    await _rm.CreateAsync(new IdentityRole(AppRoleInfo.TenantAdmin));

                await _um.AddToRoleAsync(user, AppRoleInfo.TenantAdmin);
            }

            TempData["Ok"] = "Korisnik je kreiran.";
            return RedirectToAction(nameof(Index));
        }

        // EDIT (GET)
        [Authorize(Policy = "TenantAdminOrGlobal")]
        public async Task<IActionResult> Edit(string id)
        {
            var me = await _um.GetUserAsync(User);
            var isGlobal = await _um.IsInRoleAsync(me!, AppRoleInfo.GlobalAdmin);

            var user = await _um.FindByIdAsync(id);
            if (user == null) return NotFound();

            // sigurnosna provjera
            if (!isGlobal && me!.TenantId != user.TenantId)
                return Forbid();

            ViewBag.Tenants = await _db.Tenants
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();

            var tenantName = await _db.Tenants
                .Where(t => t.Id == user.TenantId)
                .Select(t => t.Name)
                .FirstOrDefaultAsync();

            var vm = new TenantUserEditVM
            {
                UserId = user.Id,
                Email = user.Email ?? user.UserName ?? "",
                DisplayName = user.DisplayName,
                TenantId = user.TenantId,
                TenantName = tenantName,
                CanPickTenant = isGlobal
            };

            return View(vm);
        }

        // EDIT (POST)
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "TenantAdminOrGlobal")]
        public async Task<IActionResult> Edit(TenantUserEditVM model)
        {
            var me = await _um.GetUserAsync(User);
            var isGlobal = await _um.IsInRoleAsync(me!, AppRoleInfo.GlobalAdmin);

            var user = await _um.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            if (!isGlobal && me!.TenantId != user.TenantId)
                return Forbid();

            if (!ModelState.IsValid)
            {
                ViewBag.Tenants = await _db.Tenants
                    .OrderBy(t => t.Name)
                    .Select(t => new { t.Id, t.Name })
                    .ToListAsync();
                return View(model);
            }

            user.DisplayName = model.DisplayName;

            // Samo GlobalAdmin smije promijeniti tenant korisniku
            if (isGlobal)
                user.TenantId = model.TenantId ?? Guid.Empty;

            var res = await _um.UpdateAsync(user);
            if (!res.Succeeded)
            {
                foreach (var e in res.Errors)
                    ModelState.AddModelError("", e.Description);

                ViewBag.Tenants = await _db.Tenants
                    .OrderBy(t => t.Name)
                    .Select(t => new { t.Id, t.Name })
                    .ToListAsync();
                return View(model);
            }

            TempData["Ok"] = "Korisnik je ažuriran.";
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

            TempData["Ok"] = "Lozinka je resetirana.";
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
            user.LockoutEnd = locked ? null : DateTimeOffset.UtcNow.AddYears(100);

            await _um.UpdateAsync(user);

            TempData["Ok"] = locked ? "Korisnik je otključan." : "Korisnik je zaključan.";
            return RedirectToAction(nameof(Index));
        }
    }
}
