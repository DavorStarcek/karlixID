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
        [Authorize(Policy = "GlobalAdminOnly")]
        public async Task<IActionResult> Index(string? q, bool? onlyActive)
        {
            ViewBag.FilterQ = q;
            ViewBag.FilterOnlyActive = onlyActive ?? false;

            var query = _db.Tenants.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(q))
            {
                query = query.Where(t =>
                    (t.Name != null && t.Name.Contains(q)) ||
                    (t.Hostname != null && t.Hostname.Contains(q)));
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
            if (string.IsNullOrWhiteSpace(model.Name))
                ModelState.AddModelError(nameof(Tenant.Name), "Naziv je obavezan.");

            if (string.IsNullOrWhiteSpace(model.Hostname))
                ModelState.AddModelError(nameof(Tenant.Hostname), "Hostname je obavezan.");

            if (!string.IsNullOrWhiteSpace(model.Hostname))
            {
                var exists = await _db.Tenants
                    .AnyAsync(t => t.Hostname.ToLower() == model.Hostname.Trim().ToLower());
                if (exists)
                    ModelState.AddModelError(nameof(Tenant.Hostname), "Hostname već postoji.");
            }

            if (!ModelState.IsValid)
                return View(model);

            var entity = new Tenant
            {
                Id = Guid.NewGuid(),
                Name = model.Name.Trim(),
                Hostname = model.Hostname.Trim(),
                IsActive = model.IsActive
            };

            _db.Tenants.Add(entity);
            await _db.SaveChangesAsync();

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

            if (string.IsNullOrWhiteSpace(model.Name))
                ModelState.AddModelError(nameof(Tenant.Name), "Naziv je obavezan.");

            if (string.IsNullOrWhiteSpace(model.Hostname))
                ModelState.AddModelError(nameof(Tenant.Hostname), "Hostname je obavezan.");

            var exists = await _db.Tenants
                .AnyAsync(t => t.Id != model.Id && t.Hostname.ToLower() == model.Hostname.Trim().ToLower());
            if (exists)
                ModelState.AddModelError(nameof(Tenant.Hostname), "Hostname već postoji.");

            if (!ModelState.IsValid)
                return View(model);

            var dbTenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == model.Id);
            if (dbTenant == null) return NotFound();

            dbTenant.Name = model.Name.Trim();
            dbTenant.Hostname = model.Hostname.Trim();
            dbTenant.IsActive = model.IsActive;

            await _db.SaveChangesAsync();
            
            return RedirectToAction(nameof(Index));
        }

        // POST: /Tenants/Delete/{id}
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(Guid id)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
            if (tenant == null) return NotFound();

            // (opcionalno) provjeri ima li korisnika vezanih na ovaj tenant i spriječi brisanje
            // var hasUsers = await _db.AspNetUsers.AnyAsync(u => u.TenantId == id);
            // if (hasUsers) { TempData["Err"] = "Tenant ima korisnike. Najprije ih prebacite/obrišite."; return RedirectToAction(nameof(Index)); }

            _db.Tenants.Remove(tenant);
            await _db.SaveChangesAsync();

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

            return RedirectToAction(nameof(Index));
        }
    }
}
