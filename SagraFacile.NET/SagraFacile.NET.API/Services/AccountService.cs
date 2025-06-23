using Microsoft.AspNetCore.Identity;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models; // Assuming User model is here
using SagraFacile.NET.API.Services.Interfaces;
using Microsoft.IdentityModel.Tokens; // Added for JWT
using System.IdentityModel.Tokens.Jwt; // Added for JwtSecurityTokenHandler
using System.Security.Claims; // Added for Claims
using System.Text; // Added for Encoding
using Microsoft.EntityFrameworkCore; // Added for ToListAsync, AnyAsync
using SagraFacile.NET.API.Data; // Added for ApplicationDbContext
using System.Security.Cryptography; // Added for RandomNumberGenerator
using Microsoft.Extensions.Logging; // Added for ILogger

namespace SagraFacile.NET.API.Services;

// Inherit from BaseService
public class AccountService : BaseService, IAccountService
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context; // Inject DbContext
    private readonly ILogger<AccountService> _logger; // Inject ILogger
    // IHttpContextAccessor is now inherited from BaseService

    public AccountService(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor, // Inject accessor for base class
        ApplicationDbContext context, // Inject DbContext
        ILogger<AccountService> logger) // Inject ILogger
        : base(httpContextAccessor) // Call base constructor
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _context = context; // Assign DbContext
        _logger = logger; // Assign ILogger
    }

    // GetUserContext helper is now inherited from BaseService

    public async Task<AccountResult> RegisterUserAsync(RegisterDto registerDto)
    {
        _logger.LogInformation("Attempting to register user with email {Email}.", registerDto.Email);

        // Get the context of the user making the request
        var (callerOrganizationId, isCallerSuperAdmin) = GetUserContext();

        int organizationIdToAssign; // Now non-nullable, must be determined

        if (isCallerSuperAdmin)
        {
            _logger.LogDebug("Caller is SuperAdmin. Checking for OrganizationId in DTO.");
            // SuperAdmin MUST provide OrganizationId in the DTO
            if (!registerDto.OrganizationId.HasValue)
            {
                _logger.LogWarning("SuperAdmin registration failed: OrganizationId not provided in DTO.");
                return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "SuperAdmin must specify an OrganizationId when registering a user." } } };
            }
            organizationIdToAssign = registerDto.OrganizationId.Value;
            _logger.LogDebug("User will be assigned to OrganizationId: {OrganizationId}.", organizationIdToAssign);
        }
        else
        {
            _logger.LogDebug("Caller is not SuperAdmin. Assigning user to caller's organization.");
            // Admin registers users within their own organization
            if (!callerOrganizationId.HasValue)
            {
                _logger.LogError("Registering user context is missing OrganizationId for non-SuperAdmin caller.");
                return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Registering user context is missing OrganizationId." } } };
            }
            organizationIdToAssign = callerOrganizationId.Value;
            _logger.LogDebug("User will be assigned to caller's OrganizationId: {OrganizationId}.", organizationIdToAssign);

            // Optional: Double-check if Admin tries to specify a different OrgId in DTO (should ideally be ignored or error)
            if (registerDto.OrganizationId.HasValue && registerDto.OrganizationId.Value != organizationIdToAssign)
            {
                _logger.LogWarning("OrgAdmin attempted to assign user to a different organization ({AttemptedOrgId}) than their own ({CallerOrgId}).", registerDto.OrganizationId.Value, organizationIdToAssign);
                return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "OrgAdmins cannot assign users to a different organization." } } };
            }
        }

        // Validate that the determined OrganizationId actually exists
        _logger.LogDebug("Checking if OrganizationId {OrganizationId} exists.", organizationIdToAssign);
        var organizationExists = await _context.Organizations.AnyAsync(o => o.Id == organizationIdToAssign);
        if (!organizationExists)
        {
            _logger.LogWarning("Organization with ID {OrganizationId} not found during user registration.", organizationIdToAssign);
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"Organization with ID {organizationIdToAssign} not found." } } };
        }

        // Proceed with user creation
        var user = new User
        {
            UserName = registerDto.Email, // Use Email as UserName
            Email = registerDto.Email,
            FirstName = registerDto.FirstName,
            LastName = registerDto.LastName,
            OrganizationId = organizationIdToAssign // Assign the validated OrganizationId
            // EmailConfirmed = true; // Consider email confirmation flow
        };

        _logger.LogInformation("Creating user {Email} in organization {OrganizationId}.", user.Email, user.OrganizationId);
        var identityResult = await _userManager.CreateAsync(user, registerDto.Password);

        if (identityResult.Succeeded)
        {
            _logger.LogInformation("User {Email} registered successfully with ID {UserId}.", user.Email, user.Id);
            // Optional: Assign default role
            // await _userManager.AddToRoleAsync(user, "DefaultRole");

            return new AccountResult { Succeeded = true, Data = new { UserId = user.Id } }; // Return basic info
        }
        else
        {
            _logger.LogWarning("User registration failed for {Email}. Errors: {Errors}", registerDto.Email, string.Join(", ", identityResult.Errors.Select(e => e.Description)));
            return new AccountResult { Succeeded = false, Errors = identityResult.Errors };
        }
    }

    public async Task<LoginResult> LoginUserAsync(LoginDto loginDto)
    {
        _logger.LogInformation("Attempting login for user with email {Email}.", loginDto.Email);

        // 1. Find the user by email first
        var user = await _userManager.FindByEmailAsync(loginDto.Email);
        if (user == null)
        {
            _logger.LogWarning("Login failed for {Email}: User not found.", loginDto.Email);
            return new LoginResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Invalid login attempt." } } };
        }

        // 2. Check password using CheckPasswordSignInAsync
        _logger.LogDebug("Checking password for user {UserId}.", user.Id);
        var result = await _signInManager.CheckPasswordSignInAsync(
            user,
            loginDto.Password,
            lockoutOnFailure: true); // Enable lockout for security

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} logged in successfully. Generating tokens.", loginDto.Email);
            // Password is correct, proceed to generate token
            var tokenResponse = await GenerateAndSaveTokens(user);

            return new LoginResult
            {
                Succeeded = true,
                Data = tokenResponse // Return TokenResponseDto
            };
        }
        else
        {
            // Password check failed, provide feedback based on SignInResult
            string errorDescription = "Invalid login attempt."; // Default message
            if (result.IsLockedOut)
            {
                errorDescription = "Account locked out.";
                _logger.LogWarning("Login failed for {Email}: Account locked out.", loginDto.Email);
            }
            else if (result.IsNotAllowed)
            {
                errorDescription = "Login not allowed.";
                _logger.LogWarning("Login failed for {Email}: Login not allowed.", loginDto.Email);
            }
            else if (result.RequiresTwoFactor)
            {
                errorDescription = "Two-factor authentication required.";
                _logger.LogWarning("Login failed for {Email}: Two-factor authentication required.", loginDto.Email);
            }
            else
            {
                _logger.LogWarning("Login failed for {Email}: Invalid credentials.", loginDto.Email);
            }

            return new LoginResult
            {
                Succeeded = false,
                IsLockedOut = result.IsLockedOut,
                IsNotAllowed = result.IsNotAllowed,
                RequiresTwoFactor = result.RequiresTwoFactor,
                Errors = new List<IdentityError> { new IdentityError { Description = errorDescription } }
            };
        }
    }

    public async Task<AccountResult> AssignRolesAsync(AssignRolesDto assignRolesDto)
    {
        _logger.LogInformation("Attempting to set roles for user {UserId}.", assignRolesDto.UserId);
        var user = await _userManager.FindByIdAsync(assignRolesDto.UserId);
        if (user == null)
        {
            _logger.LogWarning("Assign roles failed: User with ID {UserId} not found.", assignRolesDto.UserId);
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"User with ID {assignRolesDto.UserId} not found." } } };
        }

        // Ensure all requested roles actually exist to prevent partial success states.
        foreach (var roleName in assignRolesDto.RoleNames)
        {
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                _logger.LogWarning("Assign roles failed: Role '{RoleName}' not found.", roleName);
                return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"Role '{roleName}' not found." } } };
            }
        }

        var currentRoles = await _userManager.GetRolesAsync(user);
        
        // Roles to add are the ones in the DTO that are not already assigned to the user.
        var rolesToAdd = assignRolesDto.RoleNames.Except(currentRoles).ToList();
        
        // Roles to remove are the ones currently assigned that are NOT in the DTO.
        var rolesToRemove = currentRoles.Except(assignRolesDto.RoleNames).ToList();

        IdentityResult addResult = IdentityResult.Success;
        IdentityResult removeResult = IdentityResult.Success;

        if (rolesToAdd.Any())
        {
            _logger.LogInformation("Adding roles {Roles} to user {UserId}.", string.Join(", ", rolesToAdd), assignRolesDto.UserId);
            addResult = await _userManager.AddToRolesAsync(user, rolesToAdd);
        }

        if (rolesToRemove.Any())
        {
            _logger.LogInformation("Removing roles {Roles} from user {UserId}.", string.Join(", ", rolesToRemove), assignRolesDto.UserId);
            removeResult = await _userManager.RemoveFromRolesAsync(user, rolesToRemove);
        }

        if (addResult.Succeeded && removeResult.Succeeded)
        {
            _logger.LogInformation("Roles for user {UserId} updated successfully.", assignRolesDto.UserId);
            return new AccountResult { Succeeded = true };
        }
        else
        {
            var errors = addResult.Errors.Concat(removeResult.Errors);
            _logger.LogError("Failed to update roles for user {UserId}. Errors: {Errors}", assignRolesDto.UserId, string.Join(", ", errors.Select(e => e.Description)));
            return new AccountResult { Succeeded = false, Errors = errors };
        }
    }

    public async Task<IEnumerable<UserDto>> GetUsersAsync()
    {
        _logger.LogInformation("Fetching all users.");
        // Use GetUserContext from BaseService
        var (userOrganizationId, isSuperAdmin) = GetUserContext();

        IQueryable<User> usersQuery = _userManager.Users;

        if (!isSuperAdmin)
        {
            _logger.LogDebug("Caller is not SuperAdmin. Filtering users by OrganizationId: {OrganizationId}.", userOrganizationId);
            // Non-SuperAdmin users should have an OrganizationId
            if (!userOrganizationId.HasValue)
            {
                _logger.LogError("User organization context is missing for non-SuperAdmin when fetching users.");
                throw new InvalidOperationException("User organization context is missing for non-SuperAdmin.");
            }
            // Filter by the calling user's organization
            usersQuery = usersQuery.Where(u => u.OrganizationId == userOrganizationId.Value);
        }
        else
        {
            _logger.LogDebug("Caller is SuperAdmin. Fetching all users across organizations.");
        }

        var users = await usersQuery.ToListAsync();
        var userDtos = new List<UserDto>();

        foreach (var user in users)
        {
            var roles = await _userManager.GetRolesAsync(user);
            userDtos.Add(new UserDto
            {
                Id = user.Id,
                FirstName = user.FirstName,
                LastName = user.LastName,
                Email = user.Email ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed,
                Roles = roles.ToList(),
                OrganizationId = user.OrganizationId // Populate OrganizationId
            });
        }
        _logger.LogInformation("Successfully fetched {UserCount} users.", userDtos.Count);
        return userDtos;
    }

    public async Task<UserDto?> GetUserByIdAsync(string userId)
    {
        _logger.LogInformation("Fetching user with ID {UserId}.", userId);
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("User with ID {UserId} not found.", userId);
            return null; // User not found
        }

        // Authorization Check
        var (callerOrganizationId, isCallerSuperAdmin) = GetUserContext();

        if (!isCallerSuperAdmin)
        {
            _logger.LogDebug("Caller is not SuperAdmin. Checking organization access for user {UserId}.", userId);
            // OrgAdmins/other roles can only get users within their own organization
            if (!callerOrganizationId.HasValue)
            {
                _logger.LogError("Caller organization context is missing for non-SuperAdmin when fetching user {UserId}.", userId);
                return null; // Or throw an exception if this state is considered invalid
            }
            if (user.OrganizationId != callerOrganizationId.Value)
            {
                _logger.LogWarning("Unauthorized access attempt: User {CallerUserId} from Org {CallerOrgId} tried to access user {TargetUserId} from Org {TargetOrgId}.", GetUserId(), callerOrganizationId.Value, userId, user.OrganizationId);
                return null;
            }
        }
        else
        {
            _logger.LogDebug("Caller is SuperAdmin. Access granted for user {UserId}.", userId);
        }

        // User found and authorized, map to DTO
        var roles = await _userManager.GetRolesAsync(user);
        var userDto = new UserDto
        {
            Id = user.Id,
            FirstName = user.FirstName,
            LastName = user.LastName,
            Email = user.Email ?? string.Empty,
            EmailConfirmed = user.EmailConfirmed,
            Roles = roles.ToList(),
            OrganizationId = user.OrganizationId // Include OrganizationId
        };
        _logger.LogInformation("Successfully fetched user {UserId}.", userId);
        return userDto;
    }

    public async Task<AccountResult> UpdateUserAsync(string userId, UpdateUserDto updateUserDto)
    {
        _logger.LogInformation("Attempting to update user with ID {UserId}.", userId);
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Update user failed: User with ID {UserId} not found.", userId);
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"User with ID {userId} not found." } } };
        }

        // Authorization Check
        var (callerOrganizationId, isCallerSuperAdmin) = GetUserContext();

        if (!isCallerSuperAdmin)
        {
            _logger.LogDebug("Caller is not SuperAdmin. Checking organization access for updating user {UserId}.", userId);
            if (!callerOrganizationId.HasValue || user.OrganizationId != callerOrganizationId.Value)
            {
                _logger.LogWarning("Unauthorized update attempt: User {CallerUserId} from Org {CallerOrgId} tried to update user {TargetUserId} from Org {TargetOrgId}.", GetUserId(), callerOrganizationId.Value, userId, user.OrganizationId);
                return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"User with ID {userId} not found." } } };
            }
        }
        else
        {
            _logger.LogDebug("Caller is SuperAdmin. Access granted for updating user {UserId}.", userId);
        }

        // Update user properties only if they are provided in the DTO
        _logger.LogDebug("Updating properties for user {UserId}.", userId);
        bool hasChanges = false;

        if (!string.IsNullOrWhiteSpace(updateUserDto.FirstName) && user.FirstName != updateUserDto.FirstName)
        {
            user.FirstName = updateUserDto.FirstName;
            hasChanges = true;
        }

        if (!string.IsNullOrWhiteSpace(updateUserDto.LastName) && user.LastName != updateUserDto.LastName)
        {
            user.LastName = updateUserDto.LastName;
            hasChanges = true;
        }

        // Handle potential email/username change
        var oldEmail = user.Email;
        if (!string.IsNullOrWhiteSpace(updateUserDto.Email) && user.Email != updateUserDto.Email)
        {
            _logger.LogInformation("User {UserId} email changed from {OldEmail} to {NewEmail}. Checking for existing email.", userId, oldEmail, updateUserDto.Email);
            // Check if the new email is already taken by another user
            var existingUser = await _userManager.FindByEmailAsync(updateUserDto.Email);
            if (existingUser != null && existingUser.Id != user.Id)
            {
                _logger.LogWarning("Update user failed: New email '{NewEmail}' is already taken by another user.", updateUserDto.Email);
                return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"Email '{updateUserDto.Email}' is already taken." } } };
            }

            user.Email = updateUserDto.Email;
            user.UserName = updateUserDto.Email; // Keep UserName synced with Email
            hasChanges = true;
        }

        // If no actual changes were made, we can return success early.
        if (!hasChanges)
        {
            _logger.LogInformation("No properties were changed for user {UserId}.", userId);
            return new AccountResult { Succeeded = true };
        }

        var identityResult = await _userManager.UpdateAsync(user);

        if (identityResult.Succeeded)
        {
            _logger.LogInformation("User {UserId} updated successfully.", userId);
            return new AccountResult { Succeeded = true };
        }
        else
        {
            _logger.LogError("Failed to update user {UserId}. Errors: {Errors}", userId, string.Join(", ", identityResult.Errors.Select(e => e.Description)));
            return new AccountResult { Succeeded = false, Errors = identityResult.Errors };
        }
    }

    public async Task<AccountResult> DeleteUserAsync(string userId)
    {
        _logger.LogInformation("Attempting to delete user with ID {UserId}.", userId);
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Delete user failed: User with ID {UserId} not found.", userId);
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"User with ID {userId} not found." } } };
        }

        // Authorization Check
        var (callerOrganizationId, isCallerSuperAdmin) = GetUserContext();
        var callerUserId = GetUserId(); // Get the ID of the user making the request

        // Prevent self-deletion
        if (user.Id == callerUserId)
        {
            _logger.LogWarning("Delete user failed: User {UserId} attempted to delete their own account.", userId);
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Users cannot delete their own account." } } };
        }

        if (!isCallerSuperAdmin)
        {
            _logger.LogDebug("Caller is not SuperAdmin. Checking organization access for deleting user {UserId}.", userId);
            if (!callerOrganizationId.HasValue || user.OrganizationId != callerOrganizationId.Value)
            {
                _logger.LogWarning("Unauthorized delete attempt: User {CallerUserId} from Org {CallerOrgId} tried to delete user {TargetUserId} from Org {TargetOrgId}.", GetUserId(), callerOrganizationId.Value, userId, user.OrganizationId);
                return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"User with ID {userId} not found." } } };
            }
        }
        else
        {
            _logger.LogDebug("Caller is SuperAdmin. Access granted for deleting user {UserId}.", userId);
        }

        // Perform deletion
        _logger.LogInformation("Deleting user {UserId}.", userId);
        var identityResult = await _userManager.DeleteAsync(user);

        if (identityResult.Succeeded)
        {
            _logger.LogInformation("User {UserId} deleted successfully.", userId);
            return new AccountResult { Succeeded = true };
        }
        else
        {
            _logger.LogError("Failed to delete user {UserId}. Errors: {Errors}", userId, string.Join(", ", identityResult.Errors.Select(e => e.Description)));
            return new AccountResult { Succeeded = false, Errors = identityResult.Errors };
        }
    }

    public async Task<IEnumerable<string>> GetRolesAsync()
    {
        _logger.LogInformation("Fetching all roles.");
        var roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync();
        _logger.LogInformation("Successfully fetched {RoleCount} roles.", roles.Count);
        return roles;
    }

    public async Task<AccountResult> CreateRoleAsync(CreateRoleDto createRoleDto)
    {
        _logger.LogInformation("Attempting to create role '{RoleName}'.", createRoleDto.RoleName);
        // Check if role already exists
        var roleExists = await _roleManager.RoleExistsAsync(createRoleDto.RoleName);
        if (roleExists)
        {
            _logger.LogWarning("Create role failed: Role '{RoleName}' already exists.", createRoleDto.RoleName);
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"Role '{createRoleDto.RoleName}' already exists." } } };
        }

        // Create the new role
        var role = new IdentityRole(createRoleDto.RoleName);
        var identityResult = await _roleManager.CreateAsync(role);

        if (identityResult.Succeeded)
        {
            _logger.LogInformation("Role '{RoleName}' created successfully.", createRoleDto.RoleName);
            return new AccountResult { Succeeded = true, Data = new { RoleName = role.Name } };
        }
        else
        {
            _logger.LogError("Failed to create role '{RoleName}'. Errors: {Errors}", createRoleDto.RoleName, string.Join(", ", identityResult.Errors.Select(e => e.Description)));
            return new AccountResult { Succeeded = false, Errors = identityResult.Errors };
        }
    }

    public async Task<TokenResponseDto?> RefreshTokenAsync(string? refreshToken)
    {
        _logger.LogInformation("Attempting to refresh token.");
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogWarning("Refresh token failed: Provided refresh token is null or empty.");
            return null;
        }

        var user = await _userManager.Users.SingleOrDefaultAsync(u => u.RefreshToken == refreshToken);

        if (user == null)
        {
            _logger.LogWarning("Refresh token failed: No user found for the provided refresh token.");
            return null; // Invalid token
        }

        if (user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            _logger.LogWarning("Refresh token failed for user {UserId}: Refresh token has expired.", user.Id);
            return null; // Expired token
        }

        // Generate new tokens and update the user
        _logger.LogInformation("Refresh token valid for user {UserId}. Generating new tokens.", user.Id);
        var tokenResponse = await GenerateAndSaveTokens(user);
        _logger.LogInformation("New tokens generated and saved for user {UserId}.", user.Id);
        return tokenResponse;
    }

    private async Task<TokenResponseDto> GenerateAndSaveTokens(User user)
    {
        // Use JWT_SECRET directly from configuration, consistent with Program.cs validation
        var key = _configuration["JWT_SECRET"] ?? _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT_SECRET not configured. Check environment variables.");
        // Use JWT_ISSUER and JWT_AUDIENCE directly from configuration, consistent with .env and docker-compose
        var issuer = _configuration["JWT_ISSUER"] ?? _configuration["Jwt:Issuer"] ?? throw new InvalidOperationException("JWT_ISSUER not configured. Check environment variables.");
        var audience = _configuration["JWT_AUDIENCE"] ?? _configuration["Jwt:Audience"] ?? throw new InvalidOperationException("JWT_AUDIENCE not configured. Check environment variables.");
        var accessTokenDurationMinutes = _configuration.GetValue<int?>("Jwt:AccessTokenDurationMinutes") ?? 15; // Default to 15 minutes
        var refreshTokenDurationDays = _configuration.GetValue<int?>("Jwt:RefreshTokenDurationDays") ?? 7; // Default to 7 days

        if (string.IsNullOrEmpty(key) || key.Length < 32)
        {
            _logger.LogCritical("JWT_SECRET is missing or too short. This is a configuration error. It must be at least 32 characters long.");
            throw new InvalidOperationException("JWT_SECRET is missing or too short (requires at least 32 characters). Ensure it is configured securely in environment variables.");
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // Add claims
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id),
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new Claim("firstName", user.FirstName),
            new Claim("lastName", user.LastName),
            new Claim("organizationId", user.OrganizationId.ToString())
        };

        // Add roles to claims
        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        var accessTokenExpiryTime = DateTime.UtcNow.AddMinutes(accessTokenDurationMinutes);
        var accessToken = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: accessTokenExpiryTime,
            signingCredentials: credentials);

        var newRefreshToken = GenerateRefreshTokenString();
        var newRefreshTokenExpiryTime = DateTime.UtcNow.AddDays(refreshTokenDurationDays);

        user.RefreshToken = newRefreshToken;
        user.RefreshTokenExpiryTime = newRefreshTokenExpiryTime;
        await _userManager.UpdateAsync(user);

        _logger.LogDebug("Tokens generated and user {UserId} updated with new refresh token.", user.Id);
        return new TokenResponseDto
        {
            AccessToken = new JwtSecurityTokenHandler().WriteToken(accessToken),
            AccessTokenExpiryTime = accessTokenExpiryTime,
            RefreshToken = newRefreshToken,
            RefreshTokenExpiryTime = newRefreshTokenExpiryTime,
            UserId = user.Id,
            Email = user.Email ?? string.Empty
        };
    }

    private static string GenerateRefreshTokenString()
    {
        using var rng = RandomNumberGenerator.Create();
        var randomNumber = new byte[64];
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
