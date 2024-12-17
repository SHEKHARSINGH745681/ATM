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

        [HttpGet]
        public IActionResult Get()
        {
            return Ok("You have accessed the User controller.");
        }


        [HttpGet("balance")]
        public async Task<IActionResult> GetBalance()
        {
            var userId = _userManager.GetUserId(User);

            var balance = await _context.Balances
                .Include(b => b.User)
                .FirstOrDefaultAsync(b => b.UserId == userId);

            if (balance == null)
            {
                return NotFound(new { Message = "Balance not found for the user." });
            }

            return Ok(new
            {
                UserName = balance.User.UserName,
                Balance = balance.Amount
            });
        }


        [HttpPost("deposit")]
        public async Task<IActionResult> Deposit([FromBody] DepositWithdrawRequest request)
        {
            // Get the user ID directly from the claims
            var userId = User.FindFirstValue(System.Security.Claims.ClaimTypes.NameIdentifier);

             var use = await _context.Users.Where(x=>x.UserName== userId).FirstOrDefaultAsync();

            if (userId == null)
            {
                return Unauthorized(new { Message = "User not found or not authenticated." });
            }

            // Create a new Balance object and set its properties
            Balance bal = new Balance();
            bal.Amount = request.Amount;
            bal.UserId = use.Id;

            // Add the balance entry to the database and save changes
            await _context.AddAsync(bal);
            await _context.SaveChangesAsync();

            // Retrieve the balance for the logged-in user
            var balance = await _context.Balances
                .Include(b => b.User)
                .Where(b => b.User.Id == use.Id)
                .ToArrayAsync();

            // Return success response
            return Ok(new
            {
                Message = $"Deposit successfully. {balance.First().User.UserName}, Your Total Amount: {balance.Sum(x => x.Amount)}"
            });
        }


        // Get the current user's ID from the logged-in user
        //var userId = _userManager.GetUserAsync();


        //Balance bal = new Balance();
        //bal.Amount = request.Amount;
        //bal.UserId = userId;

        //    var user = await _userManager.GetUserAsync(User); // 'User' is a built-in property that gets the current logged-in user
        //    if (user != null)
        //    {
        //        Balance bal = new Balance();
        //        bal.Amount = request.Amount;
        //        bal.UserId = user.Id;

        //        await _context.AddAsync(bal);
        //        await _context.SaveChangesAsync();

        //        var balance = await _context.Balances
        //            .Include(b => b.User)
        //            .Where(b => b.User.Id == user.Id)
        //            .ToArrayAsync();

        //        return Ok(new { Message = $"Deposit Sucessfully {balance.First().User.UserName} Your Amount {balance.Sum(x => x.Amount)}" });
        //    }

        //}
        //if (balance == null)
        //{
        //    return NotFound(new { Message = "Balance not found for the user." });
        //}

        //if (request.Amount <= 0)
        //{
        //    return BadRequest(new { Message = "Deposit amount must be greater than zero." });
        //}



        // Withdraw API
        [HttpPost("withdraw")]
            public async Task<IActionResult> Withdraw([FromBody] DepositWithdrawRequest request)
            {
                // Get the current user's ID
                var userId = _userManager.GetUserId(User);

                // Fetch the user's balance
                var balance = await _context.Balances
                    .Include(b => b.User)
                    .FirstOrDefaultAsync(b => b.UserId == userId);

                if (balance == null)
                {
                    return NotFound(new { Message = "Balance not found for the user." });
                }

                if (request.Amount <= 0)
                {
                    return BadRequest(new { Message = "Withdrawal amount must be greater than zero." });
                }

                if (balance.Amount < request.Amount)
                {
                    return BadRequest(new { Message = "Insufficient balance for the withdrawal." });
                }

                balance.Amount -= request.Amount;

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    UserName = balance.User.UserName,
                    Balance = balance.Amount
                });
            }

        }
    
}
