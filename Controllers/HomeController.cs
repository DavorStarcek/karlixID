using Microsoft.AspNetCore.Mvc;
using KarlixID.Web.Models;
using KarlixID.Web.Data;
using Microsoft.EntityFrameworkCore;

namespace KarlixID.Web.Controllers
{
    public class HomeController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
