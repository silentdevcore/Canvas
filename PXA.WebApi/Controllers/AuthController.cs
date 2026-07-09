using Canvas.Application.UseCases;
using Microsoft.AspNetCore.Mvc;

namespace Canvas.WebApi.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthenticateUserUseCase _authenticateUserUseCase;

    public AuthController(AuthenticateUserUseCase authenticateUserUseCase)
    {
        _authenticateUserUseCase = authenticateUserUseCase;
    }

    /// <summary>
    /// Authenticates a user and returns a JWT token.
    /// </summary>
    /// <param name="request">Login credentials</param>
    /// <returns>JWT token and user information</returns>
    [HttpPost("login")]
    [ProducesResponseType(typeof(LoginResponse), 200)]
    [ProducesResponseType(400)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        if (request == null || string.IsNullOrEmpty(request.Username) || string.IsNullOrEmpty(request.Password))
        {
            return BadRequest("Username and password are required");
        }

        try
        {
            var result = await _authenticateUserUseCase.ExecuteAsync(new AuthenticateUserRequest
            {
                Username = request.Username,
                Password = request.Password
            });

            if (!result.IsSuccess)
            {
                return Unauthorized(new { error = result.ErrorMessage });
            }

            return Ok(new LoginResponse
            {
                Token = result.Token!,
                User = new UserInfo
                {
                    Id = result.User!.Id,
                    Username = result.User.Username,
                    Email = result.User.Email,
                    Role = result.User.Role
                }
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "Internal server error", details = ex.Message });
        }
    }

    /// <summary>
    /// Validates a JWT token and returns current user information.
    /// </summary>
    /// <returns>Current user information</returns>
    [HttpGet("me")]
    [ProducesResponseType(typeof(UserInfo), 200)]
    [ProducesResponseType(401)]
    public async Task<IActionResult> GetCurrentUser()
    {
        var token = Request.Headers.Authorization.ToString().Replace("Bearer ", "");

        if (string.IsNullOrEmpty(token))
        {
            return Unauthorized("Token is required");
        }

        var userId = await _authenticateUserUseCase.GetUserIdFromTokenAsync(token);
        if (userId == null)
        {
            return Unauthorized("Invalid token");
        }

        var user = await _authenticateUserUseCase.GetUserByIdAsync(userId);
        if (user == null)
        {
            return Unauthorized("User not found");
        }

        return Ok(new UserInfo
        {
            Id = user.Id,
            Username = user.Username,
            Email = user.Email,
            Role = user.Role
        });
    }

    /// <summary>
    /// Logs out the current user (client-side token removal).
    /// </summary>
    [HttpPost("logout")]
    [ProducesResponseType(200)]
    public IActionResult Logout()
    {
        // In a real implementation, you might want to blacklist the token
        // For now, just return success (client should remove token)
        return Ok(new { message = "Logged out successfully" });
    }
}

public class LoginRequest
{
    public required string Username { get; set; }
    public required string Password { get; set; }
}

public class LoginResponse
{
    public required string Token { get; set; }
    public required UserInfo User { get; set; }
}

public class UserInfo
{
    public required string Id { get; set; }
    public required string Username { get; set; }
    public required string Email { get; set; }
    public required string Role { get; set; }
}