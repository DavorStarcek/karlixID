using KarlixID.Web.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading.Tasks;

public class HomeController : Controller
{
    private readonly EmailService _email;

    public HomeController(EmailService email)
    {
        _email = email;
    }

    //otvori https://localhost:44385/test-mail
    [HttpGet("/test-mail")]
    public async Task<IActionResult> TestMail()
    {
        await _email.SendAsync("mstaracm@gmail.com", "KarlixID test", "<b>Pozdrav!</b><br>Ovo je testni mail iz KarlixID sustava.");
        return Content("Mail poslan!");
    }
}
