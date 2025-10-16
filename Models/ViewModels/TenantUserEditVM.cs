using System;
using System.ComponentModel.DataAnnotations;

namespace KarlixID.Web.Models.ViewModels
{
    public class TenantUserEditVM
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty; // readonly u formi

        [Display(Name = "Ime i prezime (DisplayName)")]
        [MaxLength(256)]
        public string? DisplayName { get; set; }

        [Display(Name = "Tenant")]
        public Guid? TenantId { get; set; } // GlobalAdmin može mijenjati, TenantAdmin zaključano

        // samo za prikaz u viewu
        public bool CanPickTenant { get; set; } = false;
        public string? TenantName { get; set; }
    }
}
