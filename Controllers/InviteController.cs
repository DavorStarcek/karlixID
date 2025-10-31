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

        // =========================
        // LISTA POZIVNICA (Index)
        // =========================
        [Authorize(Policy = "TenantAdminOrGlobal")]
        public async Task<IActionResult> Index(string? q, Guid? tenantId, string? status)
        {
            var me = await _um.GetUserAsync(User);
            var editorIsGlobal = await _um.IsInRoleAsync(me!, AppRoleInfo.GlobalAdmin);

            ViewBag.Tenants = await _db.Tenants
                .OrderBy(t => t.Name)
                .Select(t => new { t.Id, t.Name })
                .ToListAsync();

            ViewBag.FilterQ = q;
            ViewBag.FilterTenantId = tenantId;
            ViewBag.FilterStatus = status ?? "active";

            var invQ = _db.Invites.AsNoTracking();

            if (!editorIsGlobal && me?.TenantId != null)
                invQ = invQ.Where(i => i.TenantId == me!.TenantId);

            if (!string.IsNullOrWhiteSpace(q))
                invQ = invQ.Where(i => i.Email.Contains(q));

            if (tenantId.HasValue)
                invQ = invQ.Where(i => i.TenantId == tenantId.Value);

            var now = DateTimeOffset.UtcNow;
            switch ((status ?? "active").ToLowerInvariant())
            {
                case "accepted":
                    invQ = invQ.Where(i => i.AcceptedAt != null);
                    break;
                case "expired":
                    invQ = invQ.Where(i => i.AcceptedAt == null && i.ExpiresAt <= now);
                    break;
                case "all":
                    break;
                default:
                    invQ = invQ.Where(i => i.AcceptedAt == null && i.ExpiresAt > now);
                    break;
            }

            var data = await (
                from i in invQ
                join t in _db.Tenants.AsNoTracking() on i.TenantId equals t.Id into gj
                from tt in gj.DefaultIfEmpty()
                orderby i.AcceptedAt != null, i.ExpiresAt descending
                select new
                {
                    i.Id,
                    i.Email,
                    TenantName = tt != null ? tt.Name : "(bez tenanta)",
                    i.RoleName,
                    i.Token,
                    i.ExpiresAt,
                    i.AcceptedAt,
                    i.CreatedBy,
                    i.CreatedAt
                }
            ).ToListAsync();

            return View(data);
        }

        // =========================
        // CREATE (GET)
        // =========================
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

        // =========================
        // CREATE (POST)
        // =========================
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

            // Token
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

            // Accept link
            var acceptUrl = Url.Action(
                "Accept", "Invite",
                new { token },
                protocol: Request.Scheme)!;

            var expiresUtcText = invite.ExpiresAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm");

            // HTML mail
            var html = $@"
            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f6f8fb;padding:24px;font-family:Segoe UI,Roboto,Arial,sans-serif"">
              <tr>
                <td align=""center"">
                  <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background:#ffffff;border-radius:12px;overflow:hidden"">
                    <tr>
                      <td style=""background:#0d6efd;color:#fff;padding:16px 24px;font-size:18px;font-weight:600"">
                        KarlixID • Pozivnica za pristup
                      </td>
                    </tr>
                    <tr>
                      <td style=""padding:24px;color:#333"">
                        <p style=""margin:0 0 12px"">Bok,</p>
                        <p style=""margin:0 0 16px"">
                          Pozvani ste da se pridružite sustavu <strong>KarlixID</strong>.
                          Kliknite gumb za aktivaciju računa i postavljanje lozinke.
                        </p>
                        <p style=""margin:0 0 4px""><strong>Email:</strong> {invite.Email}</p>
                        <p style=""margin:0 0 16px""><strong>Vrijedi do:</strong> {expiresUtcText} UTC</p>
                        <p style=""margin:0 0 16px;text-align:center"">
                          <a href=""{acceptUrl}"" style=""display:inline-block;background:#0d6efd;color:#fff;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:600"">
                            Aktiviraj račun
                          </a>
                        </p>
                        <p style=""margin:0 0 8px;font-size:12px;color:#666"">
                          Ako gumb ne radi, otvorite ovu poveznicu:
                        </p>
                        <p style=""word-break:break-all;font-size:12px;color:#666"">{acceptUrl}</p>
                      </td>
                    </tr>
                    <tr>
                      <td style=""background:#f1f3f9;color:#6b7280;padding:12px 24px;font-size:12px"">
                        Ovu poruku je generirao KarlixID sustav.
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>";

            await _email.SendAsync(invite.Email, "Pozivnica za KarlixID", html);

            TempData["Ok"] = "Pozivnica je poslana na email.";
            return RedirectToAction(nameof(Index));
        }

        // =========================
        // RESEND
        // =========================
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "TenantAdminOrGlobal")]
        public async Task<IActionResult> Resend(Guid id)
        {
            var me = await _um.GetUserAsync(User);
            var editorIsGlobal = await _um.IsInRoleAsync(me!, AppRoleInfo.GlobalAdmin);

            var inv = await _db.Invites.FirstOrDefaultAsync(i => i.Id == id);
            if (inv == null) return NotFound();

            if (!editorIsGlobal && me?.TenantId != inv.TenantId)
                return Forbid();

            if (inv.AcceptedAt != null)
            {
                TempData["Err"] = "Pozivnica je već prihvaćena.";
                return RedirectToAction(nameof(Index));
            }

            var acceptUrl = Url.Action("Accept", "Invite", new { token = inv.Token }, protocol: Request.Scheme)!;
            var expiresUtcText = inv.ExpiresAt.ToUniversalTime().ToString("yyyy-MM-dd HH:mm");

            // identičan HTML kao kod Create
            var html = $@"
            <table width=""100%"" cellpadding=""0"" cellspacing=""0"" style=""background:#f6f8fb;padding:24px;font-family:Segoe UI,Roboto,Arial,sans-serif"">
              <tr>
                <td align=""center"">
                  <table width=""600"" cellpadding=""0"" cellspacing=""0"" style=""background:#ffffff;border-radius:12px;overflow:hidden"">
                    <tr>
                      <td style=""background:#0d6efd;color:#fff;padding:16px 24px;font-size:18px;font-weight:600"">
                        KarlixID • Ponovno slanje pozivnice
                      </td>
                    </tr>
                    <tr>
                      <td style=""padding:24px;color:#333"">
                        <p style=""margin:0 0 12px"">Bok,</p>
                        <p style=""margin:0 0 16px"">
                          Podsjećamo vas da ste pozvani u sustav <strong>KarlixID</strong>.
                          Kliknite gumb ispod kako biste aktivirali svoj račun i postavili lozinku.
                        </p>
                        <p style=""margin:0 0 4px""><strong>Email:</strong> {inv.Email}</p>
                        <p style=""margin:0 0 16px""><strong>Vrijedi do:</strong> {expiresUtcText} UTC</p>
                        <p style=""margin:0 0 16px;text-align:center"">
                          <a href=""{acceptUrl}"" style=""display:inline-block;background:#0d6efd;color:#fff;text-decoration:none;padding:12px 20px;border-radius:8px;font-weight:600"">
                            Aktiviraj račun
                          </a>
                        </p>
                        <p style=""margin:0 0 8px;font-size:12px;color:#666"">
                          Ako gumb ne radi, otvorite ovu poveznicu:
                        </p>
                        <p style=""word-break:break-all;font-size:12px;color:#666"">{acceptUrl}</p>
                      </td>
                    </tr>
                    <tr>
                      <td style=""background:#f1f3f9;color:#6b7280;padding:12px 24px;font-size:12px"">
                        Ovu poruku je generirao KarlixID sustav.
                      </td>
                    </tr>
                  </table>
                </td>
              </tr>
            </table>";

            await _email.SendAsync(inv.Email, "Ponovno: pozivnica za KarlixID", html);

            TempData["Ok"] = "Pozivnica je ponovno poslana na email.";
            return RedirectToAction(nameof(Index));
        }

        // =========================
        // DELETE
        // =========================
        [HttpPost, ValidateAntiForgeryToken]
        [Authorize(Policy = "TenantAdminOrGlobal")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var me = await _um.GetUserAsync(User);
            var editorIsGlobal = await _um.IsInRoleAsync(me!, AppRoleInfo.GlobalAdmin);

            var inv = await _db.Invites.FirstOrDefaultAsync(i => i.Id == id);
            if (inv == null) return NotFound();

            if (!editorIsGlobal && me?.TenantId != inv.TenantId)
                return Forbid();

            _db.Invites.Remove(inv);
            await _db.SaveChangesAsync();

            TempData["Ok"] = "Pozivnica je obrisana.";
            return RedirectToAction(nameof(Index));
        }

        // =========================
        // ACCEPT (GET)
        // =========================
        [AllowAnonymous]
        public async Task<IActionResult> Accept(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
                return View("Invalid");

            var inv = await _db.Invites.AsNoTracking()
                .FirstOrDefaultAsync(i => i.Token == token);

            if (inv == null || inv.ExpiresAt <= DateTimeOffset.UtcNow || inv.AcceptedAt != null)
                return View("Invalid");

            var vm = new InviteAcceptVM
            {
                Token = token,
                Email = inv.Email
            };
            return View(vm);
        }

        // =========================
        // ACCEPT (POST)
        // =========================
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Accept(InviteAcceptVM model)
        {
            var inv = await _db.Invites.FirstOrDefaultAsync(i => i.Token == model.Token);
            if (inv == null)
                return View("Invalid");

            model.Email = inv.Email;

            if (inv.AcceptedAt != null)
                return View("Invalid");

            if (inv.ExpiresAt <= DateTimeOffset.UtcNow)
                return View("Expired");

            if (!ModelState.IsValid)
                return View(model);

            // Ako korisnik već postoji -> reset lozinke
            var existingUser = await _um.FindByEmailAsync(inv.Email);
            if (existingUser != null)
            {
                var resetToken = await _um.GeneratePasswordResetTokenAsync(existingUser);
                var resetRes = await _um.ResetPasswordAsync(existingUser, resetToken, model.Password);
                if (!resetRes.Succeeded)
                {
                    foreach (var e in resetRes.Errors) ModelState.AddModelError("", e.Description);
                    return View(model);
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
                foreach (var e in createRes.Errors) ModelState.AddModelError("", e.Description);
                return View(model);
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
