using KarlixID.Web.Data;
using KarlixID.Web.Models;
using KarlixID.Web.Extensions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarlixID.Web.Controllers
{
    [Authorize(Policy = "TenantAdminOrGlobal")]
    public class TenantUsersController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;

        public TenantUsersController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager)
        {
            _db = db;
            _userManager = userManager;
            _roleManager = roleManager;
        }

        // GET: /TenantUsers
        public async Task<IActionResult> Index()
        {
            var myTenantId = HttpContext.GetTenantId();

            IQueryable<ApplicationUser> q = _userManager.Users;

            if (!User.IsInRole(Roles.GlobalAdmin))
            {
                if (myTenantId == null) return Forbid();
                q = q.Where(u => u.TenantId == myTenantId);
            }

            var users = await q
                .Select(u => new TenantUserRow
                {
                    Id = u.Id,
                    Email = u.Email!,
                    UserName = u.UserName!,
                    TenantId = u.TenantId
                })
                .OrderBy(x => x.Email)
                .ToListAsync();

            return View(users);
        }

        // GET: /TenantUsers/Create
        public IActionResult Create()
        {
            return View(new CreateTenantUserVM());
        }

        // POST: /TenantUsers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateTenantUserVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var myTenantId = HttpContext.GetTenantId();

            // GlobalAdmin može kreirati usera za bilo koji tenant; TenantAdmin samo za svoj
            var targetTenant = vm.TenantId ?? myTenantId;
            if (!User.IsInRole(Roles.GlobalAdmin))
            {
                if (myTenantId == null || targetTenant != myTenantId) return Forbid();
            }
            if (targetTenant == null) { ModelState.AddModelError("", "Tenant nije definiran."); return View(vm); }

            var user = new ApplicationUser
            {
                UserName = vm.Email,
                Email = vm.Email,
                EmailConfirmed = false,
                TenantId = targetTenant.Value
            };

            // privremena lozinka
            var tempPassword = string.IsNullOrWhiteSpace(vm.Password) ? "TempPass123!" : vm.Password!;
            var result = await _userManager.CreateAsync(user, tempPassword);

            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
                return View(vm);
            }

            // uloga TenantAdmin (po izboru)
            if (vm.MakeTenantAdmin)
            {
                if (!await _roleManager.RoleExistsAsync(Roles.TenantAdmin))
                    await _roleManager.CreateAsync(new IdentityRole(Roles.TenantAdmin));
                await _userManager.AddToRoleAsync(user, Roles.TenantAdmin);
            }

            TempData["msg"] = $"Korisnik {vm.Email} kreiran.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /TenantUsers/ResetPassword/{id}
        public async Task<IActionResult> ResetPassword(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // sigurnosna provjera tenant scope-a
            if (!User.IsInRole(Roles.GlobalAdmin))
            {
                var myTenantId = HttpContext.GetTenantId();
                if (myTenantId == null || user.TenantId != myTenantId) return Forbid();
            }

            return View(new ResetPasswordVM { UserId = id, Email = user.Email! });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordVM vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var user = await _userManager.FindByIdAsync(vm.UserId);
            if (user == null) return NotFound();

            if (!User.IsInRole(Roles.GlobalAdmin))
            {
                var myTenantId = HttpContext.GetTenantId();
                if (myTenantId == null || user.TenantId != myTenantId) return Forbid();
            }

            // reset bez email slanja (nema SMTP) -> remove+add password
            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, vm.NewPassword);

            if (!result.Succeeded)
            {
                foreach (var e in result.Errors) ModelState.AddModelError("", e.Description);
                return View(vm);
            }

            TempData["msg"] = $"Lozinka resetirana za {user.Email}.";
            return RedirectToAction(nameof(Index));
        }
    }

    public class TenantUserRow
    {
        public string Id { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string UserName { get; set; } = default!;
        public Guid TenantId { get; set; }
    }

    public class CreateTenantUserVM
    {
        public Guid? TenantId { get; set; } // GlobalAdmin može birati; TenantAdmin ne mora slati

        public string Email { get; set; } = default!;
        public string? Password { get; set; } // ako prazno -> TempPass123!

        public bool MakeTenantAdmin { get; set; }
    }

    public class ResetPasswordVM
    {
        public string UserId { get; set; } = default!;
        public string Email { get; set; } = default!;
        public string NewPassword { get; set; } = default!;
    }
}
