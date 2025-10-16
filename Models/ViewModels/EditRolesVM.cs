using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KarlixID.Web.Models.ViewModels
{
    public class EditRolesVM
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Display(Name = "Email korisnika")]
        public string Email { get; set; } = string.Empty;

        public List<RoleItem> Roles { get; set; } = new();

        // pomoćno: tko uređuje
        public bool EditorIsGlobalAdmin { get; set; }
    }

    public class RoleItem
    {
        public string Name { get; set; } = string.Empty;
        public bool Selected { get; set; }

        // kada je true → checkbox je read-only (disabled)
        public bool Locked { get; set; }
    }
}
