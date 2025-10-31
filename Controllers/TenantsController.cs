using KarlixID.Web.Data;
using KarlixID.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarlixID.Web.Controllers
{
    [Authorize(Policy = "GlobalAdminOnly")]
    public class TenantsController : Controller
    {
        private readonly ApplicationDbContext _db;

        public TenantsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // GET: /Tenants?q=&onlyActive=true
        public async Task<IActionResult> Index(string? q, bool? onlyActive)
        {
            ViewBag.FilterQ = q;
            ViewBag.FilterOnlyActive = onlyActive ?? false;

            var query = _db.Tenants.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(q))
            {
                var qq = q.Trim();
                query = query.Where(t =>
                    (t.Name != null && t.Name.Contains(qq)) ||
                    (t.Hostname != null && t.Hostname.Contains(qq)));
            }

            if (onlyActive == true)
            {
                query = query.Where(t => t.IsActive);
            }

            var data = await query
                .OrderBy(t => t.Name)
                .ToListAsync();

            return View(data);
        }

        // GET: /Tenants/Create
        public IActionResult Create()
        {
            return View(new Tenant { IsActive = true });
        }

        // POST: /Tenants/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Name,Hostname,IsActive")] Tenant model)
        {
            // osnovne validacije
            var name = model.Name?.Trim();
            var host = model.Hostname?.Trim();

            if (string.IsNullOrWhiteSpace(name))
                ModelState.AddModelError(nameof(Tenant.Name), "Naziv je obavezan.");

            if (string.IsNullOrWhiteSpace(host))
                ModelState.AddModelError(nameof(Tenant.Hostname), "Hostname je obavezan.");

            if (!string.IsNullOrWhiteSpace(host))
            {
                var exists = await _db.Tenants
                    .AnyAsync(t => t.Hostname.ToLower() == host.ToLower());
                if (exists)
                    ModelState.AddModelError(nameof(Tenant.Hostname), "Hostname već postoji.");
            }

            if (!ModelState.IsValid)
                return View(model);

            var entity = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = name!,
                Hostname = host!,
                IsActive = model.IsActive
            };

            _db.Tenants.Add(entity);
            await _db.SaveChangesAsync();

            TempData["Ok"] = "Tenant je dodan.";
            return RedirectToAction(nameof(Index));
        }

        // GET: /Tenants/Edit/{id}
        public async Task<IActionResult> Edit(Guid id)
        {
            if (id == Guid.Empty) return NotFound();

            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
            if (tenant == null) return NotFound();

            return View(tenant);
        }

        // POST: /Tenants/Edit/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Name,Hostname,IsActive")] Tenant model)
        {
            if (id != model.Id) return NotFound();

            var name = model.Name?.Trim();
            var host = model.Hostname?.Trim();

            if (string.IsNullOrWhiteSpace(name))
                ModelState.AddModelError(nameof(Tenant.Name), "Naziv je obavezan.");

            if (string.IsNullOrWhiteSpace(host))
                ModelState.AddModelError(nameof(Tenant.Hostname), "Hostname je obavezan.");

            if (!string.IsNullOrWhiteSpace(host))
            {
                var exists = await _db.Tenants
                    .AnyAsync(t => t.Id != model.Id && t.Hostname.ToLower() == host.ToLower());
                if (exists)
                    ModelState.AddModelError(nameof(Tenant.Hostname), "Hostname već postoji.");
            }

            if (!ModelState.IsValid)
                return View(model);

            var dbTenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == model.Id);
            if (dbTenant == null) return NotFound();

            dbTenant.Name = name!;
            dbTenant.Hostname = host!;
            dbTenant.IsActive = model.IsActive;

            await _db.SaveChangesAsync();

            TempData["Ok"] = "Tenant je ažuriran.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Tenants/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
            if (tenant == null) return NotFound();

            // (opcionalno) blokiraj brisanje ako ima korisnika:
            // var hasUsers = await _db.AspNetUsers.AnyAsync(u => u.TenantId == id);
            // if (hasUsers) { TempData["Err"] = "Tenant ima korisnike. Najprije ih prebacite/obrišite."; return RedirectToAction(nameof(Index)); }

            _db.Tenants.Remove(tenant);
            await _db.SaveChangesAsync();

            TempData["Ok"] = "Tenant je obrisan.";
            return RedirectToAction(nameof(Index));
        }

        // POST: /Tenants/ToggleActive/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(Guid id)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
            if (tenant == null) return NotFound();

            tenant.IsActive = !tenant.IsActive;
            await _db.SaveChangesAsync();

            TempData["Ok"] = tenant.IsActive ? "Tenant je aktiviran." : "Tenant je deaktiviran.";
            return RedirectToAction(nameof(Index));
        }
    }
}
