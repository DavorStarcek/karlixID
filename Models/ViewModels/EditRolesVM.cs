using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace KarlixID.Web.Models.ViewModels
{
    public class EditRolesVM
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        // ✔ Lista svih uloga s checkboxom Selected
        public List<RoleItem> Roles { get; set; } = new();

        // ✔ Jedna uloga
        public class RoleItem
        {
            public string Name { get; set; } = string.Empty;
            public bool Selected { get; set; }
        }
    }
}
