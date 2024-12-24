namespace ATM.Models
{
    public class EmailSettings
    {
        public string? SMTPServer { get; set; }
        public int SMTPPort { get; set; }
        public string? SenderEmail { get; set; }
        public string? SenderPassword { get; set; }
        public bool EnableSSL { get; set; }
    }
}
