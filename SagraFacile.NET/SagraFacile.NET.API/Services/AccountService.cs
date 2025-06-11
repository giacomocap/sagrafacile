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

namespace SagraFacile.NET.API.Services;

// Inherit from BaseService
public class AccountService : BaseService, IAccountService
{
    private readonly UserManager<User> _userManager;
    private readonly SignInManager<User> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IConfiguration _configuration;
    private readonly ApplicationDbContext _context; // Inject DbContext
    // IHttpContextAccessor is now inherited from BaseService

    public AccountService(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor, // Inject accessor for base class
        ApplicationDbContext context) // Inject DbContext
        : base(httpContextAccessor) // Call base constructor
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _context = context; // Assign DbContext
    }

    // GetUserContext helper is now inherited from BaseService

    public async Task<AccountResult> RegisterUserAsync(RegisterDto registerDto)
    {
        // Get the context of the user making the request
        var (callerOrganizationId, isCallerSuperAdmin) = GetUserContext();

        int organizationIdToAssign; // Now non-nullable, must be determined

        if (isCallerSuperAdmin)
        {
            // SuperAdmin MUST provide OrganizationId in the DTO
            if (!registerDto.OrganizationId.HasValue)
            {
                return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "SuperAdmin must specify an OrganizationId when registering a user." } } };
            }
            organizationIdToAssign = registerDto.OrganizationId.Value;
        }
        else
        {
            // OrgAdmin registers users within their own organization
            if (!callerOrganizationId.HasValue)
            {
                // This shouldn't happen if authorization is set up correctly, but good to check.
                return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Registering user context is missing OrganizationId." } } };
            }
            organizationIdToAssign = callerOrganizationId.Value;

            // Optional: Double-check if OrgAdmin tries to specify a different OrgId in DTO (should ideally be ignored or error)
            if (registerDto.OrganizationId.HasValue && registerDto.OrganizationId.Value != organizationIdToAssign)
            {
                 return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "OrgAdmins cannot assign users to a different organization." } } };
            }
        }

        // Validate that the determined OrganizationId actually exists
        var organizationExists = await _context.Organizations.AnyAsync(o => o.Id == organizationIdToAssign);
        if (!organizationExists)
        {
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

        var identityResult = await _userManager.CreateAsync(user, registerDto.Password);

        if (identityResult.Succeeded)
        {
            // Optional: Assign default role
            // await _userManager.AddToRoleAsync(user, "DefaultRole");

            return new AccountResult { Succeeded = true, Data = new { UserId = user.Id } }; // Return basic info
        }
        else
        {
            return new AccountResult { Succeeded = false, Errors = identityResult.Errors };
        }
    }

    public async Task<AccountResult> UnassignUserFromRoleAsync(UnassignRoleDto unassignRoleDto)
    {
        var user = await _userManager.FindByIdAsync(unassignRoleDto.UserId);
        if (user == null)
        {
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"User with ID {unassignRoleDto.UserId} not found." } } };
        }

        // Check if role exists
        var roleExists = await _roleManager.RoleExistsAsync(unassignRoleDto.RoleName);
        if (!roleExists)
        {
             return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"Role '{unassignRoleDto.RoleName}' not found." } } };
        }

        // Check if the user is actually in the role before attempting removal
        var isInRole = await _userManager.IsInRoleAsync(user, unassignRoleDto.RoleName);
        if (!isInRole)
        {
            // User is not in the role, consider this a success as the desired state is achieved.
            // Or return a specific message/error if needed.
            return new AccountResult { Succeeded = true };
            // Alternative: return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"User is not assigned to role '{unassignRoleDto.RoleName}'." } } };
        }

        // Unassign role
        var identityResult = await _userManager.RemoveFromRoleAsync(user, unassignRoleDto.RoleName);

        if (identityResult.Succeeded)
        {
            return new AccountResult { Succeeded = true };
        }
        else
        {
            return new AccountResult { Succeeded = false, Errors = identityResult.Errors };
        }
    }

    public async Task<LoginResult> LoginUserAsync(LoginDto loginDto)
    {
        // 1. Find the user by email first
        var user = await _userManager.FindByEmailAsync(loginDto.Email);
        if (user == null)
        {
            // User not found - return generic error for security
            return new LoginResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Invalid login attempt." } } };
        }

        // 2. Check password using CheckPasswordSignInAsync
        var result = await _signInManager.CheckPasswordSignInAsync(
            user,
            loginDto.Password,
            lockoutOnFailure: true); // Enable lockout for security

        if (result.Succeeded)
        {
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
            }
            else if (result.IsNotAllowed)
            {
                // CheckPasswordSignInAsync doesn't check email confirmation directly here,
                // but IsNotAllowed could be set for other reasons by Identity configuration.
                // We already confirmed the email during seeding, so this is less likely.
                errorDescription = "Login not allowed.";
            }
            else if (result.RequiresTwoFactor)
            {
                errorDescription = "Two-factor authentication required.";
            }
            // If none of the above, it's likely just an incorrect password.

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

    public async Task<AccountResult> AssignUserToRoleAsync(AssignRoleDto assignRoleDto)
    {
        var user = await _userManager.FindByIdAsync(assignRoleDto.UserId);
        if (user == null)
        {
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"User with ID {assignRoleDto.UserId} not found." } } };
        }

        // Check if role exists before attempting assignment
        var roleExists = await _roleManager.RoleExistsAsync(assignRoleDto.RoleName);
        if (!roleExists)
        {
            // Optional: Create the role if it doesn't exist? Or return error.
            // For now, return error.
             return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"Role '{assignRoleDto.RoleName}' not found." } } };
            // Example: Create role if needed
            // var roleResult = await _roleManager.CreateAsync(new IdentityRole(assignRoleDto.RoleName));
            // if (!roleResult.Succeeded) return new AccountResult { Succeeded = false, Errors = roleResult.Errors };
        }

        // Assign role
        var identityResult = await _userManager.AddToRoleAsync(user, assignRoleDto.RoleName);

        if (identityResult.Succeeded)
        {
            return new AccountResult { Succeeded = true };
        }
        else
        {
            return new AccountResult { Succeeded = false, Errors = identityResult.Errors };
        }
    }

    // TODO: Add method to create roles: CreateRoleAsync(string roleName)

    public async Task<IEnumerable<UserDto>> GetUsersAsync()
    {
        // Use GetUserContext from BaseService
        var (userOrganizationId, isSuperAdmin) = GetUserContext();

        IQueryable<User> usersQuery = _userManager.Users;

        if (!isSuperAdmin)
        {
            // Non-SuperAdmin users should have an OrganizationId
            if (!userOrganizationId.HasValue)
            {
                 throw new InvalidOperationException("User organization context is missing for non-SuperAdmin.");
            }
            // Filter by the calling user's organization
            usersQuery = usersQuery.Where(u => u.OrganizationId == userOrganizationId.Value);
        }
        // Optionally include Organization details if needed for SuperAdmin view
        // .Include(u => u.Organization)

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
                // OrganizationName = user.Organization?.Name ?? string.Empty // Uncomment if needed & included
            });
        }

        return userDtos;
    }

    public async Task<UserDto?> GetUserByIdAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return null; // User not found
        }

        // Authorization Check
        var (callerOrganizationId, isCallerSuperAdmin) = GetUserContext();

        if (!isCallerSuperAdmin)
        {
            // OrgAdmins/other roles can only get users within their own organization
            if (!callerOrganizationId.HasValue)
            {
                // This indicates a potential issue with the caller's token or setup
                // Log this situation if possible
                return null; // Or throw an exception if this state is considered invalid
            }
            if (user.OrganizationId != callerOrganizationId.Value)
            {
                // User found, but caller is not authorized to view them (different org)
                // Return null as if the user wasn't found for security.
                return null;
            }
        }
        // SuperAdmins can get any user

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

        return userDto;
    }

    public async Task<AccountResult> UpdateUserAsync(string userId, UpdateUserDto updateUserDto)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"User with ID {userId} not found." } } };
        }

        // Authorization Check
        var (callerOrganizationId, isCallerSuperAdmin) = GetUserContext();

        if (!isCallerSuperAdmin)
        {
            // OrgAdmins can only update users within their own organization
            if (!callerOrganizationId.HasValue || user.OrganizationId != callerOrganizationId.Value)
            {
                // Return a generic "not found" or a specific "forbidden" error
                // Using "not found" might be slightly better for security (obscurity)
                 return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"User with ID {userId} not found." } } };
                // Or: return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Forbidden." } } };
            }
        }
        // SuperAdmins can update any user

        // Update user properties
        user.FirstName = updateUserDto.FirstName;
        user.LastName = updateUserDto.LastName;

        // Handle potential email/username change
        var oldEmail = user.Email;
        if (user.Email != updateUserDto.Email)
        {
            // Check if the new email is already taken by another user
            var existingUser = await _userManager.FindByEmailAsync(updateUserDto.Email);
            if (existingUser != null && existingUser.Id != user.Id)
            {
                return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"Email '{updateUserDto.Email}' is already taken." } } };
            }

            user.Email = updateUserDto.Email;
            user.UserName = updateUserDto.Email; // Keep UserName synced with Email
            // Consider if EmailConfirmed should be reset here if email changes
            // user.EmailConfirmed = false;
        }

        var identityResult = await _userManager.UpdateAsync(user);

        if (identityResult.Succeeded)
        {
            // If email changed, potentially trigger re-confirmation flow if implemented
            // if (oldEmail != user.Email) { /* Send confirmation email */ }

            return new AccountResult { Succeeded = true };
        }
        else
        {
            return new AccountResult { Succeeded = false, Errors = identityResult.Errors };
        }
    }

    public async Task<AccountResult> DeleteUserAsync(string userId)
    {
        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            // Return "not found" even if the user exists but is inaccessible, for security.
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"User with ID {userId} not found." } } };
        }

        // Authorization Check
        var (callerOrganizationId, isCallerSuperAdmin) = GetUserContext();
        var callerUserId = GetUserId(); // Get the ID of the user making the request

        // Prevent self-deletion
        if (user.Id == callerUserId)
        {
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Users cannot delete their own account." } } };
        }

        if (!isCallerSuperAdmin)
        {
            // OrgAdmins can only delete users within their own organization
            if (!callerOrganizationId.HasValue || user.OrganizationId != callerOrganizationId.Value)
            {
                 return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"User with ID {userId} not found." } } };
                // Or: return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Forbidden." } } };
            }
        }
        // SuperAdmins can delete any user (except themselves, checked above)

        // Perform deletion
        var identityResult = await _userManager.DeleteAsync(user);

        if (identityResult.Succeeded)
        {
            return new AccountResult { Succeeded = true };
        }
        else
        {
            // Log the errors if necessary
            return new AccountResult { Succeeded = false, Errors = identityResult.Errors };
        }
    }

    public async Task<IEnumerable<string>> GetRolesAsync()
    {
        // Retrieve all roles and select their names
        var roles = await _roleManager.Roles.Select(r => r.Name!).ToListAsync(); // Use null-forgiving operator as Role names should not be null
        return roles;
    }

    public async Task<AccountResult> CreateRoleAsync(CreateRoleDto createRoleDto)
    {
        // Check if role already exists
        var roleExists = await _roleManager.RoleExistsAsync(createRoleDto.RoleName);
        if (roleExists)
        {
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"Role '{createRoleDto.RoleName}' already exists." } } };
        }

        // Create the new role
        var role = new IdentityRole(createRoleDto.RoleName);
        var identityResult = await _roleManager.CreateAsync(role);

        if (identityResult.Succeeded)
        {
            return new AccountResult { Succeeded = true, Data = new { RoleName = role.Name } };
        }
        else
        {
            return new AccountResult { Succeeded = false, Errors = identityResult.Errors };
        }
    }

    public async Task<TokenResponseDto?> RefreshTokenAsync(string? refreshToken)
    {
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return null;
        }

        var user = await _userManager.Users.SingleOrDefaultAsync(u => u.RefreshToken == refreshToken);

        if (user == null || user.RefreshTokenExpiryTime <= DateTime.UtcNow)
        {
            return null; // Invalid token or expired
        }

        // Generate new tokens and update the user
        var tokenResponse = await GenerateAndSaveTokens(user);
        return tokenResponse;
    }

    private async Task<TokenResponseDto> GenerateAndSaveTokens(User user)
    {
        var jwtSettings = _configuration.GetSection("Jwt");
        var key = jwtSettings["Key"] ?? throw new InvalidOperationException("JWT Key not configured.");
        var issuer = jwtSettings["Issuer"] ?? throw new InvalidOperationException("JWT Issuer not configured.");
        var audience = jwtSettings["Audience"] ?? throw new InvalidOperationException("JWT Audience not configured.");
        var accessTokenDurationMinutes = jwtSettings.GetValue<int?>("AccessTokenDurationMinutes") ?? 15; // Default to 15 minutes
        var refreshTokenDurationDays = jwtSettings.GetValue<int?>("RefreshTokenDurationDays") ?? 7; // Default to 7 days


        if (string.IsNullOrEmpty(key) || key.Length < 32) // Basic check for key length
        {
             throw new InvalidOperationException("JWT Key is missing or too short (requires at least 32 characters). Ensure it is configured securely.");
        }

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        // Add claims
        var claims = new List<Claim>
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id), // Standard claim for user ID
            new Claim(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()), // Unique token identifier
            // Add custom claims as needed
            new Claim("firstName", user.FirstName),
            new Claim("lastName", user.LastName),
            new Claim("organizationId", user.OrganizationId.ToString()) // Store OrganizationId
        };

        // Add roles to claims
        var roles = await _userManager.GetRolesAsync(user);
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role))); // Use ClaimTypes.Role

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
        var randomNumber = new byte[64]; // Increased size for more entropy
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }
}
