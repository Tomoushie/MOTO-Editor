using Microsoft.AspNetCore.Mvc;
using Moto.Marketplace.Api.Services;

namespace Moto.Marketplace.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountController : ControllerBase
{
    private readonly AccountService _accountService;

    public AccountController(AccountService accountService)
    {
        _accountService = accountService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var account = await _accountService.CreateAsync(request.Email, request.Password);
        return Ok(new { account.Id, account.Email });
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var token = await _accountService.AuthenticateAsync(request.Email, request.Password);
        if (token == null) return Unauthorized();
        return Ok(new { token });
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult GetMe()
    {
        var userId = User.FindFirst("sub")?.Value;
        var account = _accountService.GetById(userId!);
        return Ok(account);
    }
}

public class RegisterRequest { public string Email { get; set; } = ""; public string Password { get; set; } = ""; }
public class LoginRequest { public string Email { get; set; } = ""; public string Password { get; set; } = ""; }
