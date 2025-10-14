using Microsoft.AspNetCore.Mvc;
using KarlixID.Web.Models;
using KarlixID.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace KarlixID.Web.Controllers
{
    public class HomeController : Controller
    {
        public async Task<IActionResult> Index([FromServices] ApplicationDbContext db)
        {
            var tenants = await db.Tenants
                                  .OrderBy(t => t.Name)
                                  .Take(50)
                                  .ToListAsync();
            return View(tenants);
        }


        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = HttpContext.TraceIdentifier });
        }
    }
}
