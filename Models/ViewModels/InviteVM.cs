using System;
using System.ComponentModel.DataAnnotations;

namespace KarlixID.Web.Models.ViewModels
{
    public class InviteCreateVM
    {
        [Required, EmailAddress, Display(Name = "Email korisnika")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Tenant")]
        public Guid? TenantId { get; set; }

        [Display(Name = "Dodijeli ulogu")]
        public string? RoleName { get; set; }

        [Display(Name = "Vrijedi (dana)")]
        [Range(1, 60)]
        public int ValidDays { get; set; } = 7;
    }

    public class InviteAcceptVM
    {
        public string Token { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty; // samo info

        [Required, DataType(DataType.Password), Display(Name = "Nova lozinka")]
        public string Password { get; set; } = string.Empty;

        [Required, DataType(DataType.Password), Display(Name = "Potvrdi lozinku")]
        [Compare("Password", ErrorMessage = "Lozinke se ne podudaraju.")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
