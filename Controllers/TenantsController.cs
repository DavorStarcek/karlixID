using KarlixID.Web.Data;
using KarlixID.Web.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace KarlixID.Web.Controllers
{
    [Authorize(Roles = AppRoleInfo.GlobalAdmin)]
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
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .ToListAsync();

            return View(data);
        }
    }
}
