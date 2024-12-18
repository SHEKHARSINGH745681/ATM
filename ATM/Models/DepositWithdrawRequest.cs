using System.ComponentModel.DataAnnotations;

namespace ATM.Models
{
    public class DepositWithdrawRequest
    {
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than zero.")]
        public decimal Amount { get; set; }
    }
}
