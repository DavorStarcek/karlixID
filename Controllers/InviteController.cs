using System.Security.Claims;
using KarlixID.Web.Data;
using KarlixID.Web.Models;
using KarlixID.Web.Models.ViewModels;
using KarlixID.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarlixID.Web.Controllers
{
    [Authorize]
    public class InviteController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _um;
        private readonly RoleManager<IdentityRole> _rm;
        private readonly EmailService _email;

        public InviteController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> um,
            RoleManager<IdentityRole> rm,
            EmailService email)
        {
            _db = db;
            _um = um;
            _rm = rm;
            _email = email;
        }

        // GET: /Invite/Create
        [Authorize(Policy = "TenantAdminOrGlobal")]
        public async Task<IActionResult> Create()
        {
            ViewBag.Tenants = await _db.Tenants
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();

            ViewBag.Roles = await _rm.Roles
                .OrderBy(r => r.Name)
                .Select(r => r.Name!)
                .ToListAsync();

            return View(new InviteCreateVM());
        }

        // POST: /Invite/Create
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "TenantAdminOrGlobal")]
        public async Task<IActionResult> Create(InviteCreateVM model)
        {
            ViewBag.Tenants = await _db.Tenants
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();

            ViewBag.Roles = await _rm.Roles
                .OrderBy(r => r.Name)
                .Select(r => r.Name!)
                .ToListAsync();

            if (!ModelState.IsValid)
                return View(model);

            // 64-znakovni token (hex) – stane u NVARCHAR(128)
            var tokenBytes = Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).ToArray();
            var token = BitConverter.ToString(tokenBytes).Replace("-", "").ToLowerInvariant();

            var me = await _um.GetUserAsync(User);

            var invite = new Invite
            {
                Id = Guid.NewGuid(),
                Email = model.Email.Trim(),
                TenantId = model.TenantId,
                RoleName = string.IsNullOrWhiteSpace(model.RoleName) ? null : model.RoleName,
                Token = token,
                ExpiresAt = DateTime.UtcNow.AddDays(model.ValidDays <= 0 ? 7 : model.ValidDays),
                CreatedBy = me?.Email ?? me?.Id ?? "system",
                CreatedAt = DateTime.UtcNow
            };

            _db.Invites.Add(invite);
            await _db.SaveChangesAsync();

            var acceptUrl = Url.Action(
                "Accept", "Invite",
                new { token },
                protocol: Request.Scheme,
                host: Request.Host.ToString()
            );

            // Uređen HTML mail
            var html = $@"
<div style='font-family:Segoe UI,Arial,sans-serif; background:#f6f8fb; padding:30px'>
  <div style='max-width:600px;margin:auto;background:#fff;border-radius:8px;padding:30px;box-shadow:0 2px 8px rgba(0,0,0,.08)'>
    <h2 style='color:#1f2937;text-align:center;margin:0 0 16px'>Pozivnica za KarlixID</h2>
    <p style='color:#374151;font-size:15px'>
      Pozvani ste u sustav <strong>KarlixID</strong>. Kliknite na gumb za aktivaciju računa i postavljanje lozinke.
    </p>
    <p style='text-align:center;margin:28px 0'>
      <a href='{acceptUrl}' style='background:#2563eb;color:#fff;padding:12px 22px;border-radius:6px;text-decoration:none;display:inline-block'>
        Aktiviraj račun
      </a>
    </p>
    <p style='color:#6b7280;font-size:14px'>
      Ako gumb ne radi, otvorite ovaj link u pregledniku:<br/>
      <a href='{acceptUrl}' style='color:#2563eb'>{acceptUrl}</a>
    </p>
    <hr style='border:none;border-top:1px solid #e5e7eb;margin:22px 0'/>
    <p style='color:#9ca3af;font-size:13px;margin:0'>
      Link vrijedi do <strong>{invite.ExpiresAt:yyyy-MM-dd HH:mm} UTC</strong>.<br/>
      Pošiljatelj: <strong>{(me?.Email ?? "KarlixID")}</strong>
    </p>
  </div>
  <p style='text-align:center;color:#9ca3af;font-size:12px;margin-top:10px'>
    © {DateTime.UtcNow.Year} KarlixID
  </p>
</div>";

            await _email.SendAsync(invite.Email, "Pozivnica za KarlixID", html);

            TempData["Ok"] = $"Pozivnica za {invite.Email} je poslana.";
            return RedirectToAction("Index", "TenantUsers");
        }

        // GET: /Invite/Accept?token=...
        [AllowAnonymous]
        public async Task<IActionResult> Accept(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return View("Invalid");

            var inv = await _db.Invites.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Token == token);

            if (inv == null)
                return View("Invalid");

            if (inv.AcceptedAt != null)
                return View("Invalid");

            if (inv.ExpiresAt <= DateTime.UtcNow)
                return View("Expired");

            return View(new InviteAcceptVM
            {
                Token = token,
                Email = inv.Email
            });
        }

        // POST: /Invite/Accept
        // POST: /Invite/Accept
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(InviteAcceptVM model)
        {
            // Uvijek učitaj pozivnicu da bi popunio model.Email kod grešaka
            var inv = await _db.Invites.FirstOrDefaultAsync(i => i.Token == model.Token);
            if (inv == null)
                return View("Invalid");

            // vrati email u model (jer input za email je disabled i ne šalje se iz forme)
            model.Email = inv.Email;

            if (inv.AcceptedAt != null)
                return View("Invalid");

            if (inv.ExpiresAt <= DateTime.UtcNow)
                return View("Expired");

            if (!ModelState.IsValid)
                return View(model); // sada prikazuje email i greške

            // Ako korisnik već postoji
            var existingUser = await _um.FindByEmailAsync(inv.Email);
            if (existingUser != null)
            {
                var resetToken = await _um.GeneratePasswordResetTokenAsync(existingUser);
                var resetRes = await _um.ResetPasswordAsync(existingUser, resetToken, model.Password);
                if (!resetRes.Succeeded)
                {
                    foreach (var e in resetRes.Errors)
                        ModelState.AddModelError("", e.Description);
                    return View(model); // email ostaje prikazan
                }

                existingUser.EmailConfirmed = true;
                await _um.UpdateAsync(existingUser);

                inv.AcceptedAt = DateTime.UtcNow;
                await _db.SaveChangesAsync();

                TempData["Ok"] = "Račun je aktiviran. Možete se prijaviti.";
                return RedirectToPage("/Account/Login", new { area = "Identity" });
            }

            // Novi korisnik
            var user = new ApplicationUser
            {
                Email = inv.Email,
                UserName = inv.Email,
                EmailConfirmed = true,
                TenantId = inv.TenantId ?? Guid.Empty
            };

            var createRes = await _um.CreateAsync(user, model.Password);
            if (!createRes.Succeeded)
            {
                foreach (var e in createRes.Errors)
                    ModelState.AddModelError("", e.Description);
                return View(model); // email ostaje prikazan
            }

            if (!string.IsNullOrWhiteSpace(inv.RoleName))
            {
                if (!await _rm.RoleExistsAsync(inv.RoleName))
                    await _rm.CreateAsync(new IdentityRole(inv.RoleName));
                await _um.AddToRoleAsync(user, inv.RoleName);
            }

            await _um.AddClaimAsync(user, new Claim("name", user.Email!));

            inv.AcceptedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            TempData["Ok"] = "Račun je uspješno aktiviran. Možete se prijaviti.";
            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

    }
}
