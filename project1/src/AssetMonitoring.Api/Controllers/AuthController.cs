using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

namespace AssetMonitoring.Api.Controllers;

public sealed record LoginRequest(string UserName, string Password);
public sealed record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, string Role);

[ApiController, Route("api/auth")]
public sealed class AuthController(IConfiguration configuration) : ControllerBase
{
    [HttpPost("login"), AllowAnonymous]
    public ActionResult<LoginResponse> Login(LoginRequest request)
    {
        var role = request.UserName.Equals("admin", StringComparison.OrdinalIgnoreCase) ? "Admin" :
            request.UserName.Equals("engineer", StringComparison.OrdinalIgnoreCase) ? "Engineer" : "Viewer";
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Password)) return BadRequest(new { message = "Username and password are required." });
        var expires = DateTimeOffset.UtcNow.AddHours(8);
        var claims = new[] { new Claim(JwtRegisteredClaimNames.Sub, request.UserName), new Claim(ClaimTypes.Name, request.UserName), new Claim(ClaimTypes.Role, role) };
        var jwt = configuration.GetSection("Jwt");
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt["SigningKey"]!)), SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(jwt["Issuer"], jwt["Audience"], claims, expires: expires.UtcDateTime, signingCredentials: credentials);
        return Ok(new LoginResponse(new JwtSecurityTokenHandler().WriteToken(token), expires, role));
    }
}
