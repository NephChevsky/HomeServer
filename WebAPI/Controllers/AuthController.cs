using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using WebAPI.DTO;

namespace WebAPI.Controllers
{
	[ApiController]
	[Route("api/[controller]")]
	public class AuthController(ILogger<ComputerController> logger, IConfiguration configuration) : ControllerBase
	{
		private readonly ILogger<ComputerController> _logger = logger;
		private readonly IConfiguration _configuration = configuration;

		[AllowAnonymous]
		[HttpPost("login")]
		public IActionResult Login([FromBody] AuthLoginRequest request)
		{
			_logger.LogInformation("Login endpoint was called");
			if (request.Username.Equals(_configuration["Auth:Username"], StringComparison.OrdinalIgnoreCase) && request.Password == _configuration["Auth:Password"])
			{
				var claims = new[]
				{
					new Claim(ClaimTypes.Name, request.Username),
					new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
				};

				var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]));
				var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

				var token = new JwtSecurityToken(
					issuer: _configuration["JwtSettings:Issuer"],
					audience: _configuration["JwtSettings:Audience"],
					claims: claims,
					expires: DateTime.Now.AddHours(1),
					signingCredentials: creds);

				_logger.LogInformation("{User} logged successfully", request.Username);
				return Ok(new AuthLoginResponse(new JwtSecurityTokenHandler().WriteToken(token)));
			}

			_logger.LogError("{User} failed to log in", request.Username);
			return Unauthorized();
		}

	}
}
