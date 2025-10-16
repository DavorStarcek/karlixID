using System;
using System.ComponentModel.DataAnnotations;

namespace KarlixID.Web.Models.ViewModels
{
    public class CreateTenantUserVM
    {
        [Required, EmailAddress, Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Dodijeli ulogu TenantAdmin")]
        public bool MakeTenantAdmin { get; set; } = false;

        // Privremena lozinka (ako je prazno, generirat ćemo u kontroleru)
        [Display(Name = "Privremena lozinka (opcionalno)")]
        public string? TempPassword { get; set; }

        // ✅ Dodano: TenantId (nullable) — null = global user
        [Display(Name = "Tenant")]
        public Guid? TenantId { get; set; }
    }
}
