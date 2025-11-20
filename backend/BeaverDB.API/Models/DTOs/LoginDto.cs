namespace BeaverDB.API.Models.DTOs;

public class LoginDto
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}

public class LoginResponseDto
{
    public required string Token { get; set; }
    public required string Username { get; set; }
    public bool IsAdmin { get; set; }
}

public class CreateUserDto
{
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string Password { get; set; }
    public bool IsAdmin { get; set; }
}
