using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BeaverDB.API.Data;
using BeaverDB.API.Models;
using BeaverDB.API.Models.DTOs;
using BeaverDB.API.Services;

namespace BeaverDB.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly BeaverDbContext _context;
    private readonly ITokenService _tokenService;
    private readonly IEncryptionService _encryptionService;
    private readonly ILogger<AuthController> _logger;

    public AuthController(
        BeaverDbContext context,
        ITokenService tokenService,
        IEncryptionService encryptionService,
        ILogger<AuthController> logger)
    {
        _context = context;
        _tokenService = tokenService;
        _encryptionService = encryptionService;
        _logger = logger;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponseDto>> Login([FromBody] LoginDto loginDto)
    {
        var user = await _context.Users
            .FirstOrDefaultAsync(u => u.Username == loginDto.Username);

        if (user == null || !_encryptionService.VerifyPassword(loginDto.Password, user.PasswordHash))
        {
            return Unauthorized(new { message = "Invalid username or password" });
        }

        var token = _tokenService.GenerateToken(user);

        return Ok(new LoginResponseDto
        {
            Token = token,
            Username = user.Username,
            IsAdmin = user.IsAdmin
        });
    }

    [HttpPost("init")]
    public async Task<ActionResult<LoginResponseDto>> InitializeAdmin([FromBody] CreateUserDto createUserDto)
    {
        // Check if any users exist
        if (await _context.Users.AnyAsync())
        {
            return BadRequest(new { message = "Admin user already exists" });
        }

        var user = new User
        {
            Username = createUserDto.Username,
            Email = createUserDto.Email,
            PasswordHash = _encryptionService.HashPassword(createUserDto.Password),
            IsAdmin = true
        };

        _context.Users.Add(user);
        await _context.SaveChangesAsync();

        var token = _tokenService.GenerateToken(user);

        _logger.LogInformation($"Admin user '{user.Username}' created successfully");

        return Ok(new LoginResponseDto
        {
            Token = token,
            Username = user.Username,
            IsAdmin = user.IsAdmin
        });
    }

    [HttpGet("check-init")]
    public async Task<ActionResult<bool>> CheckInitialization()
    {
        var hasUsers = await _context.Users.AnyAsync();
        return Ok(new { initialized = hasUsers });
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult> GetCurrentUser()
    {
        var username = User.Identity?.Name;
        if (username == null)
        {
            return Unauthorized();
        }

        var user = await _context.Users.FirstOrDefaultAsync(u => u.Username == username);
        if (user == null)
        {
            return NotFound();
        }

        return Ok(new
        {
            user.Id,
            user.Username,
            user.Email,
            user.IsAdmin
        });
    }
}
