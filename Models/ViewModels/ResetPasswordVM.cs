using System.ComponentModel.DataAnnotations;

namespace KarlixID.Web.Models.ViewModels
{
    public class ResetPasswordVM
    {
        public string UserId { get; set; } = default!;

        [Required, MinLength(6)]
        public string NewPassword { get; set; } = default!;
    }
}
