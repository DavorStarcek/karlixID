using Microsoft.AspNetCore.Identity;
using System;

namespace KarlixID.Web.Models
{
    public class ApplicationUser : IdentityUser
    {
        public Guid? TenantId { get; set; }      // nullable – kako smo i dogovorili
        public string? DisplayName { get; set; } // opcionalno

        // po želji: dodatna polja…
        // public DateTime? LastLoginUtc { get; set; }
    }
}
