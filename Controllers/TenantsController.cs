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

        // GET: /Tenants
        public async Task<IActionResult> Index()
        {
            var data = await _db.Tenants
                .OrderBy(t => t.Name)
                .ToListAsync();

            return View(data);
        }

        // GET: /Tenants/Edit/{id}
        public async Task<IActionResult> Edit(Guid id)
        {
            if (id == Guid.Empty) return NotFound();

            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == id);
            if (tenant == null) return NotFound();

            return View(tenant);
        }

        // POST: /Tenants/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(Guid id, [Bind("Id,Name,Hostname,IsActive")] Tenant model)
        {
            if (id != model.Id) return NotFound();

            // jedinstveni Hostname (case-insensitive)
            var exists = await _db.Tenants
                .AnyAsync(t => t.Id != model.Id && t.Hostname.ToLower() == model.Hostname.ToLower());
            if (exists)
            {
                ModelState.AddModelError(nameof(Tenant.Hostname), "Hostname već postoji.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var dbTenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == model.Id);
            if (dbTenant == null) return NotFound();

            dbTenant.Name = model.Name;
            dbTenant.Hostname = model.Hostname;
            dbTenant.IsActive = model.IsActive;

            await _db.SaveChangesAsync();

            TempData["Ok"] = "Tenant je ažuriran.";
            return RedirectToAction(nameof(Index));
        }
    }
}
