using ATM.Models;
using MailKit.Net.Smtp;
using MimeKit;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ATM.Controllers.Enum;
using ATM.Data;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authorization;

namespace ATM.Controllers
{
    [Authorize(Roles = "User,Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class AlertController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly EmailSettings _emailSettings;

        public AlertController(ApplicationDbContext context, IOptions<EmailSettings> emailSettings)
        {
            _context = context;
            _emailSettings = emailSettings.Value;
        }

        [HttpPost("debit")]
        public async Task<IActionResult> DebitAndSendEmail([FromBody] DepositWithdrawRequest request)
        {
            var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Unauthorized(new { Message = "User not found or not authenticated." });
            }

            var user = await _context.Users.Where(x => x.UserName == userId).FirstOrDefaultAsync();
            if (user == null)
            {
                return NotFound(new { Message = "User not found." });
            }

            var balanceRecords = await _context.Balances
                .Include(b => b.User)
                .Where(b => b.User.Id == user.Id)
                .ToArrayAsync();

            var totalBalance = balanceRecords.Sum(x => x.Amount);

            if (request.Amount <= 0)
            {
                return BadRequest(new { Message = "Debit amount must be greater than zero." });
            }

            if (totalBalance < request.Amount)
            {
                return BadRequest(new { Message = "Insufficient balance for the Debit." });
            }

            Balance balance = new Balance
            {
                Amount = -request.Amount,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                TransactionType = TransactionType.Debit
            };

            await _context.Balances.AddAsync(balance);
            await _context.SaveChangesAsync();

            totalBalance -= request.Amount;

            SendDebitAlertEmail(user.Email, (double)request.Amount, (double)totalBalance);

            return Ok(new
            {
                Message = $"Debit successful. {user.UserName}, Your Remaining Total Amount: {totalBalance}",
                DebitedAmount = request.Amount,
                TransactionDate = balance.CreatedAt
            });
        }

        private void SendDebitAlertEmail(string userEmail, double debitedAmount, double remainingBalance)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("ATM Service", _emailSettings.SenderEmail));
            email.To.Add(new MailboxAddress(userEmail, userEmail));
            email.Subject = "Debit Alert";

            var message = $"Dear User, \n\n" +
                          $"An amount of {debitedAmount} has been debited from your account.\n" +
                          $"Your remaining balance is {remainingBalance}.\n\n" +
                          $"Thank you for banking with us.";

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
        }

        [HttpPost("credit")]
        public async Task<IActionResult> CreditAndSendEmail([FromBody] DepositWithdrawRequest request)
        {
            var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Unauthorized(new { Message = "User not found or not authenticated." });
            }

            var user = await _context.Users.Where(x => x.UserName == userId).FirstOrDefaultAsync();
            if (user == null)
            {
                return NotFound(new { Message = "User not found." });
            }

            var balanceRecords = await _context.Balances
                .Include(b => b.User)
                .Where(b => b.User.Id == user.Id)
                .ToArrayAsync();

            var totalBalance = balanceRecords.Sum(x => x.Amount);

            if (request.Amount <= 0)
            {
                return BadRequest(new { Message = "Credit amount must be greater than zero." });
            }

            Balance balance = new Balance
            {
                Amount = request.Amount,
                UserId = user.Id,
                CreatedAt = DateTime.UtcNow,
                TransactionType = TransactionType.Credit
            };

            // Save the credit transaction
            await _context.Balances.AddAsync(balance);
            await _context.SaveChangesAsync();

            totalBalance += request.Amount;

            // Send a credit alert email
            SendCreditAlertEmail(user.Email, (double)request.Amount, (double)totalBalance);

            return Ok(new
            {
                Message = $"Credit successful. {user.UserName}, Your New Total Amount: {totalBalance}",
                CreditedAmount = request.Amount,
                TransactionDate = balance.CreatedAt
            });
        }

        private void SendCreditAlertEmail(string userEmail, double creditedAmount, double totalBalance)
        {
            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("ATM Service", _emailSettings.SenderEmail));
            email.To.Add(new MailboxAddress(userEmail, userEmail));
            email.Subject = "Credit Alert";

            var message = $"Dear CardHolder, \n\n" +
                          $"This is to inform you that, \n\n" +
                          $"An amount of {creditedAmount} has been credited to your account.\n" +
                          $"Your new balance is {totalBalance}.\n\n" +
                          $"Thank you for banking with us.";

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
        }
    }
}
