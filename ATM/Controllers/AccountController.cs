using ATM.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Win32;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Mail;
using System.Net;
using System.Security.Claims;
using System.Text;
using ATM.Data;
using Microsoft.EntityFrameworkCore;

namespace ATM.Controllers
{
    [Authorize]
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IConfiguration _configuration;

        public AccountController(ApplicationDbContext context, UserManager<IdentityUser> userManager, RoleManager<IdentityRole> roleManager, IConfiguration configuration)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _configuration = configuration;
        }

        [AllowAnonymous]
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] Register model)
        {
            var user = new IdentityUser { UserName = model.Username, Email = model.Email };
            var result = await _userManager.CreateAsync(user, model.Password);

            if (result.Succeeded)
            {
                //await _userManager.AddToRoleAsync(user, "User");
                return Ok(new { message = "User registered successfully" });
            }

            return BadRequest(result.Errors);
        }



        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] Login model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user == null)
            {
                return Unauthorized(new { message = "Invalid Username" });
            }

            var result = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!result)
            {
                return Unauthorized(new { message = "Invalid Password" });
            }

            // Check how many OTPs have been generated 

            var recentOtpAttempts = await _context.VerifyOtpModels
                .Where(v => v.Username == user.UserName && v.CreatedOn >= DateTime.UtcNow.AddMinutes(10) && v.IsActive && !v.IsDeleted)
                .CountAsync();

            if (recentOtpAttempts >= 5)
            {
                return BadRequest(new { message = "You have reached the maximum OTP generation limit. Please try again after 24 hours." });
            }

            var otp = new Random().Next(100000, 999999).ToString(); // 6-digit OTP

            var emailSettings = _configuration.GetSection("EmailSettings").Get<EmailSettings>();
            var mailMessage = new MailMessage
            {
                From = new MailAddress(emailSettings.SenderEmail),
                Subject = "Your OTP Code",
                Body = $"Your OTP code is: {otp}",
                IsBodyHtml = false
            };
            mailMessage.To.Add(user.Email);

            using (var smtpClient = new SmtpClient(emailSettings.SMTPServer, emailSettings.SMTPPort))
            {
                smtpClient.Credentials = new NetworkCredential(emailSettings.SenderEmail, emailSettings.SenderPassword);
                smtpClient.EnableSsl = emailSettings.EnableSSL;
                await smtpClient.SendMailAsync(mailMessage);
            }

            var existingOtp = await _context.VerifyOtpModels
                .Where(v => v.Username == user.UserName && v.IsActive && !v.IsDeleted)
                .FirstOrDefaultAsync();

            if (existingOtp != null)
            {
                existingOtp.IsActive = false;
                existingOtp.IsDeleted = true; 
                _context.VerifyOtpModels.Update(existingOtp);
            }

            var verifyOtpModel = new VerifyOtpModel
            {
                Username = user.UserName,
                Otp = otp,
                CreatedBy = user.Id,
                UpdatedBy = user.Id,
                CreatedOn = DateTime.UtcNow,
                UpdatedOn = DateTime.UtcNow,
                ExpirationTime = DateTime.UtcNow.AddMinutes(5), 
                IsActive = true
            };

            await _context.VerifyOtpModels.AddAsync(verifyOtpModel);
            await _context.SaveChangesAsync();

            return Ok(new { message = "OTP sent to your email", success = true });
        }


        [AllowAnonymous]
        [HttpPost("VerifyOTP")]
        public async Task<IActionResult> VerifyOTP ([FromBody] LoginOTP model)
        {
            // Find the user by username
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user == null)
            {
                return Unauthorized(new { message = "Invalid Username" });
            }

            // Check if the password is correct
            var passwordValid = await _userManager.CheckPasswordAsync(user, model.Password);
            if (!passwordValid)
            {
                return Unauthorized(new { message = "Invalid Password" });
            }

            var verifyOtp = await _context.VerifyOtpModels
                .Where(v => v.Username == model.Username && v.Otp == model.Otp && v.IsActive && !v.IsDeleted)
                .FirstOrDefaultAsync();

            if (verifyOtp == null)
            {
                return Unauthorized(new { message = "Invalid OTP" });
            }

            if (verifyOtp.ExpirationTime < DateTime.UtcNow)
            {
                return Unauthorized(new { message = "OTP has expired" });
            }

            var userRoles = await _userManager.GetRolesAsync(user);

            var authClaims = new List<Claim>
    {
        new Claim(JwtRegisteredClaimNames.Sub, user.UserName!),
        new Claim(ClaimTypes.NameIdentifier, user.Id),
        new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
    };

            authClaims.AddRange(userRoles.Select(role => new Claim(ClaimTypes.Role, role)));

            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                expires: DateTime.Now.AddMinutes(double.Parse(_configuration["Jwt:ExpiryMinutes"]!)),
                claims: authClaims,
                signingCredentials: new SigningCredentials(
                    new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!)),
                    SecurityAlgorithms.HmacSha256)
            );

            return Ok(new { token = new JwtSecurityTokenHandler().WriteToken(token) });
        }


        [AllowAnonymous]
        [HttpPost("add-role")]
        public async Task<IActionResult> AddRole([FromBody] string role)
        {
            if (!await _roleManager.RoleExistsAsync(role))
            {
                var result = await _roleManager.CreateAsync(new IdentityRole(role));
                if (result.Succeeded)
                {
                    return Ok(new { message = "Role added successfully" });
                }

                return BadRequest(result.Errors);
            }

            return BadRequest("Role already exists");
        }


        [AllowAnonymous]
        [HttpPost("assign-role")]
        public async Task<IActionResult> AssignRole([FromBody] UserRole model)
        {
            var user = await _userManager.FindByNameAsync(model.Username);
            if (user == null)
            {
                return BadRequest("User not found");
            }

            var result = await _userManager.AddToRoleAsync(user, model.Role);
            if (result.Succeeded)
            {
                return Ok(new { message = "Role assigned successfully" });
            }

            return BadRequest(result.Errors);
        }
    }
}
