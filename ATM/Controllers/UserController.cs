using ATM.Controllers.Enum;
using ATM.Data;
using ATM.Models;
using iText.Kernel.Pdf;
using iText.Layout;
using iText.Layout.Element;
using iText.Layout.Properties;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Npgsql.Internal;
using System.Reflection.Metadata;
using System.Security.Claims;
using Document = iText.Layout.Document;
using Table = iText.Layout.Element.Table;

namespace ATM.Controllers
{
    [Authorize(Roles = "User,Admin")]
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;
        public UserController(ApplicationDbContext context, UserManager<IdentityUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }


        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance()
        {
            var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userId == null)
            {
                return Unauthorized(new { Message = "User not authenticated." });
            }

            var user = await _context.Users
                .Where(u => u.UserName == userId)
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new { Message = "User not found." });
            }

            var totalBalance = await _context.Balances
                .Where(b => b.UserId == user.Id)
                .SumAsync(b => b.Amount);

            return Ok(new
            {
                UserName = user.UserName,
                Balance = totalBalance
            });
        }

        [HttpPost("Credit")]
        public async Task<IActionResult> Deposit([FromBody] DepositWithdrawRequest request)
        {
            var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);

            var use = await _context.Users.Where(x => x.UserName == userId).FirstOrDefaultAsync();

            if (userId == null)
            {
                return Unauthorized(new { Message = "User not found or not authenticated." });
            }
            Balance bal = new Balance
            {
                Amount = request.Amount,  // Credit is a positive amount
                UserId = use.Id,
                CreatedAt = DateTime.UtcNow,  // Store the UTC time for when the credit happens
                TransactionType = TransactionType.Credit  // Set the transaction type to Credit
            };

            await _context.AddAsync(bal);
            await _context.SaveChangesAsync();

            var balance = await _context.Balances
                .Include(b => b.User)
                .Where(b => b.User.Id == use.Id)
                .ToArrayAsync();

            return Ok(new
            {
                Message = $"Credit successfully. {balance.First().User.UserName}, Your Total Amount: {balance.Sum(x => x.Amount)}",
                CreditAmount = request.Amount,
                TransactionDate = bal.CreatedAt  // Return the transaction date (UTC)
            });
        }


        [HttpPost("Debit")]
        public async Task<IActionResult> Debit([FromBody] DepositWithdrawRequest request)
        {
            var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return Unauthorized(new { Message = "User not found or not authenticated." });
            }

            var use = await _context.Users.Where(x => x.UserName == userId).FirstOrDefaultAsync();



            var balanceRecords = await _context.Balances
                .Include(b => b.User)
                .Where(b => b.User.Id == use.Id)
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

            Balance bal = new Balance
            {
                Amount = -request.Amount,
                UserId = use.Id,
                CreatedAt = DateTime.UtcNow,
                TransactionType = TransactionType.Debit
            };

            // Save the transaction
            await _context.Balances.AddAsync(bal);
            await _context.SaveChangesAsync();

            totalBalance -= request.Amount;

            return Ok(new
            {
                Message = $"Debit successful. {use.UserName}, Your Remaining Total Amount: {totalBalance}",
                DebitedAmount = request.Amount,
                TransactionDate = bal.CreatedAt
            });
        }

        [HttpGet("History")]
        public async Task<IActionResult> GetTransactionHistory()
        {
            var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { Message = "User not found or not authenticated." });
            }

            var use = await _context.Users
                                    .Where(x => x.UserName == userId)
                                    .FirstOrDefaultAsync();

            if (use == null)
            {
                return NotFound(new { Message = "User not found." });
            }

            var transactions = await _context.Balances
                                              .Where(b => b.User.Id == use.Id)
                                              .OrderBy(b => b.CreatedAt) 
                                              .Select(b => new
                                              {
                                                  b.Amount,
                                                  TransactionType = b.TransactionType == 0 ? "Credit" : "Debit",
                                                  b.CreatedAt
                                              })
                                              .ToListAsync();

            decimal currentBalance = 0;
            var transactionHistory = new List<object>();

            foreach (var transaction in transactions)
            {
                currentBalance += transaction.TransactionType == "Credit" ? transaction.Amount : -transaction.Amount;

                transactionHistory.Add(new
                {
                    transaction.Amount,
                    transaction.TransactionType,
                    transaction.CreatedAt,
                    TotalBalance = currentBalance
                });
            }

            if (transactionHistory.Count == 0)
            {
                return Ok(new { Message = "No transactions found for this user.", Transactions = new List<object>() });
            }

            return Ok(new
            {
                Message = "Transaction history retrieved successfully.",
                Transactions = transactionHistory
            });
        }

        [HttpGet("PDF")]
        public async Task<IActionResult> GetHistory()
        {
            var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { Message = "User not found or not authenticated." });
            }

            var user = await _context.Users
                                     .Where(x => x.UserName == userId)
                                     .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new { Message = "User not found." });
            }

            var transactions = await _context.Balances
                                              .Where(b => b.User.Id == user.Id)
                                              .OrderBy(b => b.CreatedAt)
                                              .Select(b => new
                                              {
                                                  b.Amount,
                                                  TransactionType = b.TransactionType == 0 ? "Credit" : "Debit",
                                                  b.CreatedAt
                                              })
                                              .ToListAsync();

            decimal currentBalance = 0;
            var transactionHistory = new List<object>();

            foreach (var transaction in transactions)
            {
                currentBalance += transaction.TransactionType == "Credit" ? transaction.Amount : -transaction.Amount;

                transactionHistory.Add(new
                {
                    transaction.Amount,
                    transaction.TransactionType,
                    transaction.CreatedAt,
                    TotalBalance = currentBalance
                });
            }

            if (transactionHistory.Count == 0)
            {
                return Ok(new { Message = "No transactions found for this user.", Transactions = new List<object>() });
            }

            // Generate PDF
            using var memoryStream = new MemoryStream();
            using (var writer = new PdfWriter(memoryStream))
            {
                var pdfDoc = new PdfDocument(writer);
                var document = new Document(pdfDoc);

                // Add content to the PDF
                document.Add(new Paragraph("Transaction History").SetBold().SetFontSize(18));
                document.Add(new Paragraph($"User: {user.UserName}").SetFontSize(12));
                string currentDate = DateTime.Now.ToString("dd-MM-yyyy");
                document.Add(new Paragraph($"Date: {currentDate}").SetFontSize(12));
                document.Add(new Paragraph("\n"));

                var table = new Table(UnitValue.CreatePercentArray(new float[] { 3, 3, 3, 3 })).UseAllAvailableWidth();

                // Add table headers
                table.AddHeaderCell("Amount (Rs.)");
                table.AddHeaderCell("Transaction Type");
                table.AddHeaderCell("Date");
                table.AddHeaderCell("Total Balance");

                // Add transactions to the table
                foreach (var transaction in transactionHistory)
                {
                    table.AddCell(transaction.GetType().GetProperty("Amount").GetValue(transaction).ToString());
                    table.AddCell(transaction.GetType().GetProperty("TransactionType").GetValue(transaction).ToString());
                    table.AddCell(transaction.GetType().GetProperty("CreatedAt").GetValue(transaction).ToString());
                    table.AddCell(transaction.GetType().GetProperty("TotalBalance").GetValue(transaction).ToString());
                }


                document.Add(table);
                document.Close();
            }

            // Return the PDF as a file
            var fileName = "TransactionHistory.pdf";
            return File(memoryStream.ToArray(), "application/pdf", fileName);
        }



    }
}



