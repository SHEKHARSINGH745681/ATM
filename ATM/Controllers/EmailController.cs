using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ATM.Models;

namespace ATM.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class EmailController : ControllerBase
    {
        private readonly EmailSettings _emailSettings;

       public EmailController(IOptions<EmailSettings> emailSettings)
        {
            _emailSettings = emailSettings.Value;
        }

        [HttpPost("send")]
        public IActionResult SendEmail([FromQuery] string toEmail, [FromQuery] string subject, [FromQuery] string message)
        {
            if (string.IsNullOrEmpty(toEmail) || string.IsNullOrEmpty(subject) || string.IsNullOrEmpty(message))
            {
                return BadRequest(new { Message = "Please provide all required parameters (toEmail, subject, and message)." });
            }

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("SHEKHAR", _emailSettings.SenderEmail));
            email.To.Add(new MailboxAddress(toEmail, toEmail)); 
            email.Subject = subject;

            var bodyBuilder = new BodyBuilder { TextBody = message };
            email.Body = bodyBuilder.ToMessageBody();

            using (var smtpClient = new SmtpClient())
            {
                smtpClient.Connect(_emailSettings.SMTPServer, _emailSettings.SMTPPort,
                    MailKit.Security.SecureSocketOptions.StartTls);

                smtpClient.Authenticate(_emailSettings.SenderEmail, _emailSettings.SenderPassword);

                smtpClient.Send(email);

                smtpClient.Disconnect(true);
            }
            return Ok(new { Message = "Email sent successfully!" });
        }
    }
}
