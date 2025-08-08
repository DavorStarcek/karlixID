using KarlixID.Web.Data;
using KarlixID.Web.Models;
using KarlixID.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarlixID.Web.Controllers
{
    [Authorize(Roles = "GlobalAdmin")]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ExcelExportService _excelExport;

        public AdminController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            ExcelExportService excelExport)
        {
            _db = db;
            _userManager = userManager;
            _excelExport = excelExport;
        }

        // GET: /Admin
        public async Task<IActionResult> Index()
        {
            var tenants = await _db.Tenants.OrderBy(t => t.Name).ToListAsync();
            return View(tenants);
        }

        // GET: /Admin/CreateTenant
        public IActionResult CreateTenant()
        {
            return View();
        }

        // POST: /Admin/CreateTenant
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateTenant(Tenant model)
        {
            if (ModelState.IsValid)
            {
                _db.Tenants.Add(model);
                await _db.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        // GET: /Admin/Users/{tenantId}
        public async Task<IActionResult> Users(Guid tenantId)
        {
            var users = await _userManager.Users
                .Where(u => u.TenantId == tenantId)
                .ToListAsync();

            ViewBag.TenantId = tenantId;
            return View(users);
        }

        // POST: /Admin/ExportUsers/{tenantId}
        [HttpPost]
        public async Task<IActionResult> ExportUsers(Guid tenantId)
        {
            var users = await _userManager.Users
                .Where(u => u.TenantId == tenantId)
                .ToListAsync();

            var file = _excelExport.ExportUsers(users);
            return File(file, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "korisnici.xlsx");
        }
    }
}
