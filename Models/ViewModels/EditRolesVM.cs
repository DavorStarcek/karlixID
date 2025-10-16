using System.ComponentModel.DataAnnotations;

namespace KarlixID.Web.Models.ViewModels
{
    public class EditRolesVM
    {
        [Required]
        public string UserId { get; set; } = string.Empty;

        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        // Sve dostupne role u sustavu (po imenu)
        public List<string> AvailableRoles { get; set; } = new();

        // Trenutno odabrane role (checkbox binding)
        public List<string> SelectedRoles { get; set; } = new();
    }
}
