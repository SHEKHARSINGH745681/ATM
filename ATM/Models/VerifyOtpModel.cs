using System.ComponentModel.DataAnnotations;

namespace ATM.Models
{
        public class VerifyOtpModel    {
            [Key]
            public int OtpId { get; set; }
            public string Username { get; set; } = string.Empty;
            public string Otp { get; set; } = string.Empty;
            public DateTime CreatedOn { get; set; } = DateTime.UtcNow;  
            public DateTime UpdatedOn { get; set; } = DateTime.UtcNow;  
            public DateTime ExpirationTime { get; set; } // Expiration time for OTP
            public string CreatedBy { get; set; }
            public string UpdatedBy { get; set; }
            public bool IsDeleted { get; set; } = false;
            public bool IsActive { get; set; } = true;
        }
}
