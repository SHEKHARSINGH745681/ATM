using ATM.Controllers.Enum;
using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ATM.Models
{
    public class Balance
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; }

        public decimal Amount { get; set; }

        public DateTime CreatedAt { get; set; }
        public TransactionType TransactionType { get; set; }

        [ForeignKey("UserId")]
        public IdentityUser? User { get; set; }
    }
}
