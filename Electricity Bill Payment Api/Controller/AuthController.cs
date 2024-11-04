using Electricity_Bill_Payment_Api.Model;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Electricity_Bill_Payment_Api.Controller
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _config;

        public AuthController(ApplicationDbContext context, IConfiguration config)
        {
            _context = context;
            _config = config;
        }

        [HttpPost("signup")]
        public async Task<IActionResult> SignUp(AuthenticationRequest request)
        {
            var userExists = await _context.Users.AnyAsync(u => u.Username == request.Username);
            if (userExists)
                return BadRequest("Username already exists");

            var user = new User
            {
                Username = request.Username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            return Ok("User registered successfully");
        }

        [HttpPost("signin")]
        public async Task<IActionResult> SignIn(AuthenticationRequest request)
        {
            try
            {
                Console.WriteLine("SignIn method started.");

                var user = await _context.Users.SingleOrDefaultAsync(u => u.Username == request.Username);

                if (user == null)
                {
                    Console.WriteLine("User not found.");
                    return Unauthorized("Invalid credentials: user not found.");
                }

                bool isPasswordValid;

                try
                {
                    Console.WriteLine("Attempting password verification.");
                    isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
                    Console.WriteLine("Password verification successful.");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Password verification error: {ex.Message}");
                    return StatusCode(500, "An error occurred while verifying the password.");
                }

                if (!isPasswordValid)
                {
                    Console.WriteLine("Incorrect password.");
                    return Unauthorized("Invalid credentials: incorrect password.");
                }

                var token = GenerateJwtToken(user);
                Console.WriteLine("Token generation successful.");

                return Ok(new
                {
                    Message = "Login successful",
                    Token = token
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred during login: {ex.Message}");
                return StatusCode(500, "An error occurred while processing your request.");
            }
        }



        [Authorize]
        [HttpPost("signout")]
        public IActionResult SignOut()
        {
            // No server-side action is required for sign-out in JWT-based authentication.
            return Ok("User signed out successfully");
        }

        private string GenerateJwtToken(User user)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(_config["JWT:Secret"]);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new Claim[]
                {
                new Claim(ClaimTypes.Name, user.Id.ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Username)
                }),
                Expires = DateTime.UtcNow.AddHours(1),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }
    }
}
