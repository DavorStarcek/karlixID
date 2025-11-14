using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using System.Linq;
using static OpenIddict.Abstractions.OpenIddictConstants;

namespace KarlixID.Web.Controllers
{
    public class AuthorizationController : Controller
    {
        // GET/POST /connect/authorize
        [HttpGet("~/connect/authorize"), HttpPost("~/connect/authorize")]
        [IgnoreAntiforgeryToken] // OIDC middleware rješava CSRF
        public IActionResult Authorize()
        {
            var request = HttpContext.GetOpenIddictServerRequest()
                          ?? throw new InvalidOperationException("OIDC request unavailable.");

            // Ako nije prijavljen → redirect na login i vrati se natrag na ovaj authorize zahtjev
            if (!(User?.Identity?.IsAuthenticated ?? false))
            {
                // pripremi parametre (query ili form) u točnom nullable obliku
                IEnumerable<KeyValuePair<string, string?>> pairs =
                    Request.HasFormContentType
                        ? Request.Form.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value.ToString()))
                        : Request.Query.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value.ToString()));

                var redirectUri = (Request.PathBase + Request.Path + QueryString.Create(pairs)).ToString();

                return Challenge(
                    authenticationSchemes: new[] { IdentityConstants.ApplicationScheme },
                    properties: new AuthenticationProperties { RedirectUri = redirectUri }
                );
            }

            // Složi OpenIddict identity (schema mora biti OpenIddict server)
            var identity = new ClaimsIdentity(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);

            // sub/name/email/roles
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity!.Name!;
            var name = User.Identity!.Name ?? sub;
            var email = User.FindFirstValue(ClaimTypes.Email);

            identity.AddClaim(new Claim(Claims.Subject, sub)
                .SetDestinations(Destinations.AccessToken, Destinations.IdentityToken));

            identity.AddClaim(new Claim(Claims.Name, name)
                .SetDestinations(Destinations.AccessToken, Destinations.IdentityToken));

            if (!string.IsNullOrEmpty(email))
            {
                identity.AddClaim(new Claim(Claims.Email, email)
                    .SetDestinations(Destinations.AccessToken, Destinations.IdentityToken));
            }

            // Role iz postojećeg korisničkog principal-a
            foreach (var role in User.Claims.Where(c => c.Type == ClaimTypes.Role))
            {
                identity.AddClaim(new Claim(Claims.Role, role.Value)
                    .SetDestinations(Destinations.AccessToken, Destinations.IdentityToken));
            }

            // 🔹 TENANT CLAIM – NOVO
            var tenantClaim =
                User.Claims.FirstOrDefault(c => c.Type == "tenant")
                ?? User.Claims.FirstOrDefault(c => c.Type == "tenant_id")
                ?? User.Claims.FirstOrDefault(c => c.Type == "tenant:name");

            if (tenantClaim is not null && !string.IsNullOrWhiteSpace(tenantClaim.Value))
            {
                // U OpenIddict tokene ga zovemo "tenant"
                identity.AddClaim(new Claim("tenant", tenantClaim.Value)
                    .SetDestinations(Destinations.AccessToken, Destinations.IdentityToken));
            }

            // Scope-ovi iz zahtjeva ili default
            var requested = request.GetScopes(); // ImmutableArray<string>
            var scopes = !requested.IsDefaultOrEmpty
                ? requested.ToArray()
                : new[] { Scopes.OpenId, Scopes.Profile, Scopes.Email };

            identity.SetScopes(scopes);

            var principal = new ClaimsPrincipal(identity);
            return SignIn(principal, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // POST /connect/logout (optional – ako ti treba vlastiti endpoint)
        [HttpPost("~/connect/logout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);
            return SignOut(OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
        }

        // GET /connect/userinfo (informativno)
        [HttpGet("~/connect/userinfo")]
        [Authorize]
        public IActionResult Userinfo()
        {
            var claims = new Dictionary<string, object?>
            {
                [Claims.Subject] = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.Identity?.Name,
                [Claims.Name] = User.Identity?.Name,
                [Claims.Email] = User.FindFirstValue(ClaimTypes.Email)
            };
            return Ok(claims);
        }
    }
}
