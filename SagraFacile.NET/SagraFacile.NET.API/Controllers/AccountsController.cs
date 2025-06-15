using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Services.Interfaces;
using System; // Added for Exception handling
using System.Collections.Generic; // Added for IEnumerable
using System.Linq;
using System.Threading.Tasks;

namespace SagraFacile.NET.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AccountsController : ControllerBase
{
    private readonly IAccountService _accountService;
    private readonly ILogger<AccountsController> _logger; // Optional: Add logging

    public AccountsController(IAccountService accountService, ILogger<AccountsController> logger)
    {
        _accountService = accountService;
        _logger = logger;
    }

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(RegisterDto registerDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _accountService.RegisterUserAsync(registerDto);

        if (result.Succeeded)
        {
            _logger.LogInformation("User registered successfully: {Email}", registerDto.Email);
            // Return the data provided by the service (e.g., UserId)
            return Ok(result.Data ?? new { Message = "Registration successful" });
        }
        else
        {
            _logger.LogWarning("User registration failed for: {Email}", registerDto.Email);
            foreach (var error in result.Errors ?? Enumerable.Empty<Microsoft.AspNetCore.Identity.IdentityError>())
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return BadRequest(ModelState);
        }
    }

    [HttpPost("login")]
    [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login(LoginDto loginDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _accountService.LoginUserAsync(loginDto);

        if (result.Succeeded && result.Data is TokenResponseDto tokenResponse)
        {
            _logger.LogInformation("User logged in successfully: {Email}", loginDto.Email);
            return Ok(tokenResponse);
        }
        else
        {
            _logger.LogWarning("Login failed for user: {Email}. LockedOut: {IsLockedOut}, NotAllowed: {IsNotAllowed}",
                loginDto.Email, result.IsLockedOut, result.IsNotAllowed);

            if (result.IsLockedOut)
            {
                return BadRequest(new { Message = "User account locked out." });
            }
            if (result.IsNotAllowed)
            {
                 return BadRequest(new { Message = "Login not allowed. Account may require confirmation." });
            }
            // For general failures, return Unauthorized
            return Unauthorized(new { Message = "Invalid login attempt." });
        }
    }

    [HttpPost("assign-role")]
    [Authorize(Roles = "SuperAdmin")] // TODO: Define roles properly and secure this endpoint
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)] // If user or role not found
    public async Task<IActionResult> AssignRole(AssignRoleDto assignRoleDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var result = await _accountService.AssignUserToRoleAsync(assignRoleDto);

        if (result.Succeeded)
        {
            _logger.LogInformation("Role '{RoleName}' assigned successfully to user '{UserId}'.", assignRoleDto.RoleName, assignRoleDto.UserId);
            return Ok(new { Message = $"Role '{assignRoleDto.RoleName}' assigned successfully to user '{assignRoleDto.UserId}'." });
        }
        else
        {
            _logger.LogWarning("Failed to assign role '{RoleName}' to user '{UserId}'.", assignRoleDto.RoleName, assignRoleDto.UserId);
            foreach (var error in result.Errors ?? Enumerable.Empty<Microsoft.AspNetCore.Identity.IdentityError>())
            {
                // Check if it's a "not found" error to return 404
                if (error.Description.Contains("not found", StringComparison.OrdinalIgnoreCase))
                {
                    return NotFound(new { Message = error.Description });
                }
                ModelState.AddModelError(string.Empty, error.Description);
            }
            // Return 400 for other validation errors
            return BadRequest(ModelState);
        }
    }

    [HttpPost("unassign-role")]
    [Authorize(Roles = "SuperAdmin")] // Only SuperAdmins can unassign roles
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)] // If user or role not found
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UnassignRole([FromBody] UnassignRoleDto unassignRoleDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _accountService.UnassignUserFromRoleAsync(unassignRoleDto);

            if (result.Succeeded)
            {
                _logger.LogInformation("Role '{RoleName}' unassigned successfully from user '{UserId}'.", unassignRoleDto.RoleName, unassignRoleDto.UserId);
                return Ok(new { Message = $"Role '{unassignRoleDto.RoleName}' unassigned successfully from user '{unassignRoleDto.UserId}'." });
            }
            else
            {
                _logger.LogWarning("Failed to unassign role '{RoleName}' from user '{UserId}'.", unassignRoleDto.RoleName, unassignRoleDto.UserId);
                bool isNotFound = false;
                foreach (var error in result.Errors ?? Enumerable.Empty<Microsoft.AspNetCore.Identity.IdentityError>())
                {
                    // Check if it's a "not found" error (user or role)
                    if (error.Description.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    {
                        isNotFound = true;
                        _logger.LogWarning("Unassign role failed for user {UserId}: {ErrorDescription}", unassignRoleDto.UserId, error.Description);
                    }
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                if (isNotFound)
                {
                    return NotFound(new { Message = $"User or Role not found." }); // Generic not found
                }
                else
                {
                    // Return 400 for other Identity errors
                    return BadRequest(ModelState);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while unassigning role {RoleName} from user {UserId}.", unassignRoleDto.RoleName, unassignRoleDto.UserId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }


    [HttpGet] // Route: GET /api/Accounts
    [Authorize(Roles = "Admin,SuperAdmin")] // Only Admins and SuperAdmins can list users
    [ProducesResponseType(typeof(IEnumerable<UserDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<UserDto>>> GetUsers()
    {
        try
        {
            var users = await _accountService.GetUsersAsync();
            return Ok(users);
        }
        catch (UnauthorizedAccessException ex) // Catch potential exceptions from BaseService
        {
             _logger.LogWarning(ex, "Unauthorized access attempt in GetUsers.");
             // Return Forbidden because the user is authenticated but lacks org claim or similar issue
             return Forbid();
        }
        catch (InvalidOperationException ex) // Catch potential exceptions from BaseService
        {
             _logger.LogError(ex, "Invalid operation in GetUsers, possibly missing user context.");
             // Return 500 as it indicates an unexpected state
             return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving user context.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while getting users.");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    [HttpGet("{userId}")] // Route: GET /api/Accounts/{userId}
    [Authorize(Roles = "Admin,SuperAdmin")] // Admins can get user details
    [ProducesResponseType(typeof(UserDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    public async Task<ActionResult<UserDto>> GetUserById(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(new { Message = "User ID cannot be empty." });
        }

        try
        {
            var userDto = await _accountService.GetUserByIdAsync(userId);

            if (userDto == null)
            {
                // Service returns null if user not found OR if caller is not authorized (e.g., Admin trying to access other org)
                _logger.LogWarning("User with ID {UserId} not found or access denied.", userId);
                return NotFound(new { Message = $"User with ID {userId} not found or access denied." });
            }

            _logger.LogInformation("Retrieved user details for {UserId}.", userId);
            return Ok(userDto);
        }
        catch (UnauthorizedAccessException ex) // Catch potential exceptions from BaseService (though service handles most auth)
        {
             _logger.LogWarning(ex, "Unauthorized access attempt during GetUserById for {UserId}.", userId);
             return Forbid(); // Should ideally be handled by service returning null, but catch just in case
        }
        catch (InvalidOperationException ex) // Catch potential exceptions from BaseService
        {
             _logger.LogError(ex, "Invalid operation in GetUserById for {UserId}, possibly missing user context.", userId);
             return StatusCode(StatusCodes.Status500InternalServerError, "An error occurred while retrieving user context.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while getting user {UserId}.", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    [HttpPut("{userId}")] // Route: PUT /api/Accounts/{userId}
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> UpdateUser(string userId, [FromBody] UpdateUserDto updateUserDto)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(new { Message = "User ID cannot be empty." });
        }

        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _accountService.UpdateUserAsync(userId, updateUserDto);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {UserId} updated successfully.", userId);
                return NoContent(); // Standard success response for PUT update
            }
            else
            {
                _logger.LogWarning("Failed to update user {UserId}.", userId);
                bool isNotFound = false;
                foreach (var error in result.Errors ?? Enumerable.Empty<Microsoft.AspNetCore.Identity.IdentityError>())
                {
                    // Check for "not found" errors returned by the service (covers user not existing or Admin trying to access outside their org)
                    if (error.Description.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    {
                        isNotFound = true;
                        // Log the specific reason if needed, but return 404
                        _logger.LogWarning("Update failed for user {UserId}: {ErrorDescription}", userId, error.Description);
                    }
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                if (isNotFound)
                {
                    return NotFound(new { Message = $"User with ID {userId} not found or access denied." });
                }
                else
                {
                    // Return 400 for other validation errors (e.g., email taken)
                    return BadRequest(ModelState);
                }
            }
        }
        catch (UnauthorizedAccessException ex) // Catch potential exceptions from BaseService (though service handles most auth)
        {
             _logger.LogWarning(ex, "Unauthorized access attempt during UpdateUser for {UserId}.", userId);
             return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while updating user {UserId}.", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    [HttpDelete("{userId}")] // Route: DELETE /api/Accounts/{userId}
    [Authorize(Roles = "Admin,SuperAdmin")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)] // For self-deletion attempt or other validation errors
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> DeleteUser(string userId)
    {
        if (string.IsNullOrEmpty(userId))
        {
            return BadRequest(new { Message = "User ID cannot be empty." });
        }

        try
        {
            var result = await _accountService.DeleteUserAsync(userId);

            if (result.Succeeded)
            {
                _logger.LogInformation("User {UserId} deleted successfully.", userId);
                return NoContent(); // Standard success response for DELETE
            }
            else
            {
                _logger.LogWarning("Failed to delete user {UserId}.", userId);
                bool isNotFound = false;
                bool isSelfDeleteAttempt = false;
                foreach (var error in result.Errors ?? Enumerable.Empty<Microsoft.AspNetCore.Identity.IdentityError>())
                {
                    // Check for specific error messages from the service
                    if (error.Description.Contains("not found", StringComparison.OrdinalIgnoreCase))
                    {
                        isNotFound = true;
                        _logger.LogWarning("Delete failed for user {UserId}: {ErrorDescription}", userId, error.Description);
                    }
                    else if (error.Description.Contains("cannot delete their own account", StringComparison.OrdinalIgnoreCase))
                    {
                        isSelfDeleteAttempt = true;
                        _logger.LogWarning("Delete failed for user {UserId}: Self-deletion attempt.", userId);
                    }
                    ModelState.AddModelError(string.Empty, error.Description);
                }

                if (isNotFound)
                {
                    // Covers user not existing or Admin trying to access outside their org
                    return NotFound(new { Message = $"User with ID {userId} not found or access denied." });
                }
                else if (isSelfDeleteAttempt)
                {
                    // Return 400 Bad Request for trying to delete self
                    return BadRequest(new { Message = "Users cannot delete their own account." });
                }
                else
                {
                    // Return 400 for other Identity errors during deletion
                    return BadRequest(ModelState);
                }
            }
        }
        catch (UnauthorizedAccessException ex) // Catch potential exceptions from BaseService
        {
             _logger.LogWarning(ex, "Unauthorized access attempt during DeleteUser for {UserId}.", userId);
             return Forbid();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while deleting user {UserId}.", userId);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    [HttpGet("roles")] // Route: GET /api/Accounts/roles
    [Authorize(Roles = "Admin,SuperAdmin")] // Restrict to admins
    [ProducesResponseType(typeof(IEnumerable<string>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<IEnumerable<string>>> GetRoles()
    {
        try
        {
            var roles = await _accountService.GetRolesAsync();
            return Ok(roles);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while getting roles.");
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }

    [HttpPost("roles")] // Route: POST /api/Accounts/roles
    [Authorize(Roles = "SuperAdmin")] // Only SuperAdmins can create roles
    [ProducesResponseType(typeof(object), StatusCodes.Status201Created)] // Return role name on success
    [ProducesResponseType(StatusCodes.Status400BadRequest)] // For validation errors or if role exists
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateRole([FromBody] CreateRoleDto createRoleDto)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        try
        {
            var result = await _accountService.CreateRoleAsync(createRoleDto);

            if (result.Succeeded)
            {
                _logger.LogInformation("Role '{RoleName}' created successfully.", createRoleDto.RoleName);
                // Return 201 Created with the role details (or just the name)
                return CreatedAtAction(nameof(GetRoles), result.Data); // Point to the GetRoles endpoint
            }
            else
            {
                _logger.LogWarning("Failed to create role '{RoleName}'.", createRoleDto.RoleName);
                foreach (var error in result.Errors ?? Enumerable.Empty<Microsoft.AspNetCore.Identity.IdentityError>())
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
                // Return 400 for role already exists or other Identity errors
                return BadRequest(ModelState);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "An unexpected error occurred while creating role {RoleName}.", createRoleDto.RoleName);
            return StatusCode(StatusCodes.Status500InternalServerError, "An unexpected error occurred.");
        }
    }


    // TODO: Add endpoints for User Management (Get User by ID?) using _accountService
    // TODO: Add Logout endpoint if needed (depends on auth mechanism - JWT stateless vs cookies)

    [HttpPost("refresh-token")]
    [ProducesResponseType(typeof(TokenResponseDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequestDto request)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var tokenResponse = await _accountService.RefreshTokenAsync(request.RefreshToken);

        if (tokenResponse == null)
        {
            _logger.LogWarning("Invalid refresh token attempt.");
            return Unauthorized(new { Message = "Invalid refresh token." });
        }

        _logger.LogInformation("Token refreshed successfully for user: {Email}", tokenResponse.Email);
        return Ok(tokenResponse);
    }
}
