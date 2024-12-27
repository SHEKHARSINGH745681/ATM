using System.ComponentModel.DataAnnotations;

namespace ATM.Models
{
    public class ResetPasswordRequest
    {
        [Required(ErrorMessage = "Old password is required.")]
        public string? OldPassword { get; set; }

        [Required(ErrorMessage ="New password is required.")]
        [StringLength(6 ,MinimumLength = 6 , ErrorMessage ="New Password Should be 6 Digit Long.")]
        public string? NewPassword { get; set; }
    }
}
