using System;
using System.ComponentModel.DataAnnotations;

namespace KarlixID.Web.Models.ViewModels
{
    public class CreateTenantUserVM
    {
        [Required, EmailAddress]
        public string Email { get; set; } = default!;

        [Required, MinLength(6)]
        public string Password { get; set; } = default!;

        // Ako ne proslijediš, u kontroleru ćemo staviti current tenant.
        public Guid? TenantId { get; set; }

        // Samo GlobalAdmin smije postaviti TenantAdmin ulogu.
        public bool MakeTenantAdmin { get; set; }
    }
}
