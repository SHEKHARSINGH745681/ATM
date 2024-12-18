using ATM.Data;
using ATM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

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

            Balance bal = new Balance();
            bal.Amount = request.Amount;
            bal.UserId = use.Id;

            await _context.AddAsync(bal);
            await _context.SaveChangesAsync();

            var balance = await _context.Balances
                .Include(b => b.User)
                .Where(b => b.User.Id == use.Id)
                .ToArrayAsync();

            return Ok(new
            {
                Message = $"Credit successfully. {balance.First().User.UserName}, Your Total Amount: {balance.Sum(x => x.Amount)}"
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

            if (use == null)
            {
                return NotFound(new { Message = "User not found in the system." });
            }

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
                UserId = use.Id
            };

            await _context.AddAsync(bal);
            await _context.SaveChangesAsync();

            totalBalance -= request.Amount;

            return Ok(new
            {
                Message = $"Debit successful. {use.UserName}, Your Remaining Total Amount: {totalBalance}"
            });
        }

        [HttpGet]
        public async Task<IActionResult> GetTransactionHistory()
        {
            var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);

            if (userId == null)
            {
                return BadRequest(new { Message = "User not Authenticated" });
            }
            var user = await _context.Users.Where(x => x.UserName == userId).FirstOrDefaultAsync();

            if (user == null)
            {
                return BadRequest(new { Message = "User Not Found" });
            }
            var transactionHistory = await _context.Balances
                .Where(b => b.UserId == user.Id)
                .OrderBy(b => b.CreatedAt)
                .Select(b => new
                {
                    Amount = b.Amount,
                    TransactionType = b.Amount > 0 ? "Credit" : "Debit",
                    Timestamp = b.CreatedAt
                })
                .ToListAsync();
            var RemainingBalance = transactionHistory.Sum(t => t.Amount);

            return Ok(new
            {
                UserName = user.UserName,
                RemainingBalance = RemainingBalance,
                transactionHistory = transactionHistory

            });

        }

    }
}
