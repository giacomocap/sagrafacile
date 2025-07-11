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
using System.Web;
using Microsoft.AspNetCore.WebUtilities; // Added for WebEncoders
using SagraFacile.NET.API.Models.Results;

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
    private readonly IEmailService _emailService;
    // IHttpContextAccessor is now inherited from BaseService

    public AccountService(
        UserManager<User> userManager,
        SignInManager<User> signInManager,
        RoleManager<IdentityRole> roleManager,
        IConfiguration configuration,
        IHttpContextAccessor httpContextAccessor, // Inject accessor for base class
        ApplicationDbContext context, // Inject DbContext
        ILogger<AccountService> logger, // Inject ILogger
        IEmailService emailService)
        : base(httpContextAccessor) // Call base constructor
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _configuration = configuration;
        _context = context; // Assign DbContext
        _logger = logger; // Assign ILogger
        _emailService = emailService;
    }

    // GetUserContext helper is now inherited from BaseService

    public async Task<AccountResult> RegisterUserAsync(RegisterDto registerDto)
    {
        _logger.LogInformation("Attempting to register user with email {Email}.", registerDto.Email);

        var appMode = _configuration["APP_MODE"] ?? _configuration["AppSettings:AppMode"];
        var isSaaSMode = appMode == "saas";
        var isApiCallAuthenticated = _httpContextAccessor.HttpContext?.User?.Identity?.IsAuthenticated ?? false; // Check if the call is from an already logged-in user

        // Check for pending invitations before allowing public sign-up
        var pendingInvitation = await _context.UserInvitations
            .FirstOrDefaultAsync(i => i.Email == registerDto.Email && !i.IsUsed && i.ExpiryDate > DateTime.UtcNow);

        if (pendingInvitation != null)
        {
            _logger.LogWarning("Registration blocked for {Email} due to a pending invitation.", registerDto.Email);
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "An invitation has already been sent to this email address. Please use the link in the invitation email to register." } } };
        }

        // SaaS Public Sign-up Flow
        if (isSaaSMode && !isApiCallAuthenticated)
        {
            _logger.LogInformation("Executing SaaS public sign-up flow for {Email}.", registerDto.Email);
            var user = new User
            {
                UserName = registerDto.Email,
                Email = registerDto.Email,
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                OrganizationId = null, // No organization assigned on initial sign-up
                EmailConfirmed = false // Requires email confirmation
            };

            var identityResult = await _userManager.CreateAsync(user, registerDto.Password);
            if (!identityResult.Succeeded)
            {
                _logger.LogWarning("SaaS user creation failed for {Email}. Errors: {Errors}", registerDto.Email, string.Join(", ", identityResult.Errors.Select(e => e.Description)));
                return new AccountResult { Succeeded = false, Errors = identityResult.Errors };
            }

            _logger.LogInformation("SaaS user {Email} created successfully with ID {UserId}. Generating confirmation token.", user.Email, user.Id);

            // Send confirmation email
            try
            {
                var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
                var tokenBytes = Encoding.UTF8.GetBytes(token);
                var urlSafeToken = WebEncoders.Base64UrlEncode(tokenBytes); // Generate a URL-safe token

                var frontendBaseUrl = _configuration["FRONTEND_BASE_URL"] ?? _configuration["AppSettings:BaseUrl"] ?? "https://app.sagrafacile.it";
                var confirmationLink = $"{frontendBaseUrl}/conferma-email?userId={user.Id}&token={urlSafeToken}";

                await _emailService.SendEmailAsync(
                    user.Email,
                    "Conferma la tua registrazione a SagraFacile",
                    $"<h1>Benvenuto in SagraFacile!</h1><p>Per favore, conferma il tuo indirizzo email cliccando sul seguente link: <a href='{confirmationLink}'>Conferma Email</a></p>"
                );
                _logger.LogInformation("Email confirmation link sent to {Email}.", user.Email);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to send confirmation email to {Email}. The user was created but needs to confirm their email manually.", registerDto.Email);
                // Even if email fails, the user is created. They can request a new confirmation link later.
                // We still return a success message to the user, but with a note about the email.
                return new AccountResult { Succeeded = true, Data = new { UserId = user.Id, Message = "Registration successful. Please check your email to confirm your account. If you don't receive an email, please contact support." } };
            }

            return new AccountResult { Succeeded = true, Data = new { UserId = user.Id, Message = "Registration successful. Please check your email to confirm your account." } };
        }
        else // This is the Admin-initiated Registration Flow (for both Self-Hosted and SaaS)
        {
            _logger.LogInformation("Executing admin-initiated registration flow for {Email}.", registerDto.Email);
            var (callerOrganizationId, isCallerSuperAdmin) = GetUserContext(); // Get user context here
            Guid? organizationIdToAssign;

            if (isCallerSuperAdmin)
            {
                if (!registerDto.OrganizationId.HasValue)
                {
                    return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "SuperAdmin must specify an OrganizationId." } } };
                }
                organizationIdToAssign = registerDto.OrganizationId.Value;
            }
            else
            {
                if (!callerOrganizationId.HasValue)
                {
                    return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Admin user context is missing OrganizationId." } } };
                }
                organizationIdToAssign = callerOrganizationId.Value;
            }

            var organizationExists = await _context.Organizations.AnyAsync(o => o.Id == organizationIdToAssign.Value);
            if (!organizationExists)
            {
                return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = $"Organization with ID {organizationIdToAssign.Value} not found." } } };
            }

            var adminCreatedUser = new User
            {
                UserName = registerDto.Email,
                Email = registerDto.Email,
                FirstName = registerDto.FirstName,
                LastName = registerDto.LastName,
                OrganizationId = organizationIdToAssign.Value,
                EmailConfirmed = true // Admin-created users are confirmed by default
            };

            var adminCreateResult = await _userManager.CreateAsync(adminCreatedUser, registerDto.Password);
            if (adminCreateResult.Succeeded)
            {
                _logger.LogInformation("Admin-created user {Email} registered successfully in organization {OrganizationId}.", adminCreatedUser.Email, adminCreatedUser.OrganizationId);
                return new AccountResult { Succeeded = true, Data = new { UserId = adminCreatedUser.Id } };
            }
            else
            {
                _logger.LogWarning("Admin-initiated registration failed for {Email}. Errors: {Errors}", registerDto.Email, string.Join(", ", adminCreateResult.Errors.Select(e => e.Description)));
                return new AccountResult { Succeeded = false, Errors = adminCreateResult.Errors };
            }
        }
    } // Close the else block for admin-initiated flow

    public async Task<LoginResult> LoginUserAsync(LoginDto loginDto)
    {
        _logger.LogInformation("Attempting login for user with email {Email}.", loginDto.Email);

        var user = await _userManager.FindByEmailAsync(loginDto.Email);
        if (user == null)
        {
            _logger.LogWarning("Login failed for {Email}: User not found.", loginDto.Email);
            return new LoginResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Invalid login attempt." } } };
        }

        // Check if email is confirmed before checking password
        if (!user.EmailConfirmed)
        {
            _logger.LogWarning("Login failed for {Email}: Email not confirmed.", loginDto.Email);
            return new LoginResult { Succeeded = false, IsNotAllowed = true, Errors = new List<IdentityError> { new IdentityError { Description = "Email not confirmed. Please check your inbox for the confirmation link." } } };
        }

        var result = await _signInManager.CheckPasswordSignInAsync(user, loginDto.Password, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            _logger.LogInformation("User {Email} logged in successfully. Generating tokens.", loginDto.Email);
            var tokenResponse = await GenerateAndSaveTokens(user);
            return new LoginResult { Succeeded = true, Data = tokenResponse };
        }
        else
        {
            string errorDescription = "Invalid login attempt.";
            if (result.IsLockedOut)
            {
                errorDescription = "Account locked out.";
                _logger.LogWarning("Login failed for {Email}: Account locked out.", loginDto.Email);
            }
            else if (result.IsNotAllowed)
            {
                // This case might be redundant now due to the explicit check above, but kept for completeness
                errorDescription = "Login not allowed.";
                _logger.LogWarning("Login failed for {Email}: Login not allowed (by SignInManager).", loginDto.Email);
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

    public async Task<AccountResult> ConfirmEmailAsync(string userId, string token)
    {
        _logger.LogInformation("Attempting to confirm email for user {UserId}.", userId);
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Email confirmation failed: UserId or token is missing.");
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "User ID and token must be provided." } } };
        }

        var user = await _userManager.FindByIdAsync(userId);
        if (user == null)
        {
            _logger.LogWarning("Email confirmation failed: User with ID {UserId} not found.", userId);
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "User not found." } } };
        }

        try
        {
            var decodedTokenBytes = WebEncoders.Base64UrlDecode(token);
            var originalToken = Encoding.UTF8.GetString(decodedTokenBytes);

            var result = await _userManager.ConfirmEmailAsync(user, originalToken);
            if (result.Succeeded)
            {
                _logger.LogInformation("Email confirmed successfully for user {UserId}.", userId);
                return new AccountResult { Succeeded = true };
            }
            else
            {
                _logger.LogWarning("Email confirmation failed for user {UserId}. Errors: {Errors}", userId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return new AccountResult { Succeeded = false, Errors = result.Errors };
            }
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Failed to decode Base64Url token for user {UserId}.", userId);
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Invalid token format." } } };
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
                OrganizationId = user.OrganizationId ?? Guid.Empty, // Populate OrganizationId, default to Guid.Empty if null
                Roles = roles.ToList()
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
            OrganizationId = user.OrganizationId ?? Guid.Empty, // Include OrganizationId, default to Guid.Empty if null
            Roles = roles.ToList()
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
        var appMode = _configuration["APP_MODE"] ?? _configuration["AppSettings:AppMode"];
        var isSaaSMode = appMode == "saas";
        if (isSaaSMode)
        {
            // Perform soft deletion for SaaS mode
            _logger.LogInformation("SaaS Mode: Soft-deleting user {UserId}.", userId);

            user.Status = UserStatus.PendingDeletion;
            user.DeletionScheduledAt = DateTime.UtcNow.AddDays(30); // 30-day grace period for anonymization
            user.RefreshToken = null; // Invalidate refresh token
            user.RefreshTokenExpiryTime = null;
            user.EmailConfirmed = false; // Prevent login
            user.LockoutEnd = DateTimeOffset.MaxValue; // Lock the account permanently

            var softDeleteResult = await _userManager.UpdateAsync(user);

            if (softDeleteResult.Succeeded)
            {
                _logger.LogInformation("User {UserId} soft-deleted successfully. Anonymization scheduled for {DeletionDate}.", userId, user.DeletionScheduledAt);
                return new AccountResult { Succeeded = true };
            }
            else
            {
                _logger.LogError("Failed to soft-delete user {UserId}. Errors: {Errors}", userId, string.Join(", ", softDeleteResult.Errors.Select(e => e.Description)));
                return new AccountResult { Succeeded = false, Errors = softDeleteResult.Errors };
            }
        }
        else
        {
            // Perform hard deletion for self-hosted mode
            _logger.LogInformation("Self-Hosted Mode: Hard-deleting user {UserId}.", userId);
            var hardDeleteResult = await _userManager.DeleteAsync(user);

            if (hardDeleteResult.Succeeded)
            {
                _logger.LogInformation("User {UserId} hard-deleted successfully.", userId);
                return new AccountResult { Succeeded = true };
            }
            else
            {
                _logger.LogError("Failed to hard-delete user {UserId}. Errors: {Errors}", userId, string.Join(", ", hardDeleteResult.Errors.Select(e => e.Description)));
                return new AccountResult { Succeeded = false, Errors = hardDeleteResult.Errors };
            }
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
            new Claim("lastName", user.LastName)
        };

        // Only add organizationId claim if it's not null
        if (user.OrganizationId.HasValue)
        {
            claims.Add(new Claim("organizationId", user.OrganizationId.Value.ToString()));
        }

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

    public async Task<AccountResult> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto)
    {
        _logger.LogInformation("Password reset requested for email {Email}.", forgotPasswordDto.Email);
        var user = await _userManager.FindByEmailAsync(forgotPasswordDto.Email);
        if (user == null)
        {
            // Don't reveal that the user does not exist.
            _logger.LogWarning("Password reset request for non-existent email {Email}.", forgotPasswordDto.Email);
            return new AccountResult { Succeeded = true };
        }

        var token = await _userManager.GeneratePasswordResetTokenAsync(user);
        var tokenBytes = Encoding.UTF8.GetBytes(token);
        var urlSafeToken = WebEncoders.Base64UrlEncode(tokenBytes);

        var frontendBaseUrl = _configuration["FRONTEND_BASE_URL"] ?? _configuration["AppSettings:BaseUrl"] ?? "https://app.sagrafacile.it";
        var resetLink = $"{frontendBaseUrl}/reset-password?userId={user.Id}&token={urlSafeToken}";

        try
        {
            await _emailService.SendEmailAsync(
                user.Email,
                "Reset della tua password per SagraFacile",
                $"<h1>Reset Password</h1><p>Per resettare la tua password, clicca sul seguente link: <a href='{resetLink}'>Reset Password</a></p><p>Se non hai richiesto tu il reset, ignora questa email.</p>"
            );
            _logger.LogInformation("Password reset link sent to {Email}.", user.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send password reset email to {Email}.", user.Email);
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Could not send reset email. Please try again later." } } };
        }

        return new AccountResult { Succeeded = true };
    }

    public async Task<AccountResult> ResetPasswordAsync(ResetPasswordDto resetPasswordDto)
    {
        _logger.LogInformation("Attempting to reset password for user {UserId}.", resetPasswordDto.UserId);
        var user = await _userManager.FindByIdAsync(resetPasswordDto.UserId);
        if (user == null)
        {
            // Don't reveal that the user does not exist
            _logger.LogWarning("Password reset attempt for non-existent user ID {UserId}.", resetPasswordDto.UserId);
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Invalid password reset request." } } };
        }

        try
        {
            var decodedTokenBytes = WebEncoders.Base64UrlDecode(resetPasswordDto.Token);
            var originalToken = Encoding.UTF8.GetString(decodedTokenBytes);

            var result = await _userManager.ResetPasswordAsync(user, originalToken, resetPasswordDto.Password);

            if (result.Succeeded)
            {
                _logger.LogInformation("Password for user {UserId} has been reset successfully.", resetPasswordDto.UserId);
                return new AccountResult { Succeeded = true };
            }
            else
            {
                _logger.LogWarning("Password reset failed for user {UserId}. Errors: {Errors}", resetPasswordDto.UserId, string.Join(", ", result.Errors.Select(e => e.Description)));
                return new AccountResult { Succeeded = false, Errors = result.Errors };
            }
        }
        catch (FormatException ex)
        {
            _logger.LogError(ex, "Failed to decode Base64Url password reset token for user {UserId}.", resetPasswordDto.UserId);
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Invalid token format." } } };
        }
    }

    public async Task<AccountResult> InviteUserAsync(UserInvitationRequestDto invitationRequestDto)
    {
        _logger.LogInformation("Attempting to invite user with email {Email}.", invitationRequestDto.Email);
        var (callerOrganizationId, isCallerSuperAdmin) = GetUserContext();
        if (!callerOrganizationId.HasValue)
        {
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "User context is missing OrganizationId." } } };
        }

        var existingUser = await _userManager.FindByEmailAsync(invitationRequestDto.Email);
        if (existingUser != null)
        {
            // Smart re-invitation: Check if this is a soft-deleted user that can be restored
            if (existingUser.Status == UserStatus.PendingDeletion && 
                existingUser.OrganizationId == callerOrganizationId.Value)
            {
                _logger.LogInformation("Found soft-deleted user {UserId} with email {Email}. Attempting to restore user instead of creating invitation.", existingUser.Id, invitationRequestDto.Email);
                
                // Restore the soft-deleted user
                existingUser.Status = UserStatus.Active;
                existingUser.DeletionScheduledAt = null;
                existingUser.EmailConfirmed = true;
                existingUser.LockoutEnd = null; // Remove lockout
                existingUser.RefreshToken = null; // Clear old refresh token for security
                existingUser.RefreshTokenExpiryTime = null;

                // Update roles to match the invitation request
                var currentRoles = await _userManager.GetRolesAsync(existingUser);
                var rolesToAdd = invitationRequestDto.Roles.Except(currentRoles).ToList();
                var rolesToRemove = currentRoles.Except(invitationRequestDto.Roles).ToList();

                if (rolesToAdd.Any())
                {
                    await _userManager.AddToRolesAsync(existingUser, rolesToAdd);
                }

                if (rolesToRemove.Any())
                {
                    await _userManager.RemoveFromRolesAsync(existingUser, rolesToRemove);
                }

                var restoreResult = await _userManager.UpdateAsync(existingUser);
                if (restoreResult.Succeeded)
                {
                    _logger.LogInformation("User {UserId} with email {Email} has been successfully restored from soft-deletion.", existingUser.Id, invitationRequestDto.Email);
                    
                    // Send restoration notification email
                    try
                    {
                        await _emailService.SendEmailAsync(
                            existingUser.Email,
                            "Il tuo account SagraFacile è stato ripristinato",
                            $"<h1>Account Ripristinato</h1><p>Il tuo account SagraFacile è stato ripristinato e puoi nuovamente accedere al sistema. I tuoi ruoli sono stati aggiornati come richiesto dall'amministratore.</p><p>Puoi accedere utilizzando la tua password esistente.</p>"
                        );
                        _logger.LogInformation("Restoration notification email sent to {Email}.", existingUser.Email);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send restoration notification email to {Email}. User was restored successfully.", existingUser.Email);
                    }

                    return new AccountResult { 
                        Succeeded = true, 
                        Data = new { 
                            UserId = existingUser.Id, 
                            Message = "User account has been restored from deletion and roles have been updated.",
                            Action = "UserRestored"
                        } 
                    };
                }
                else
                {
                    _logger.LogError("Failed to restore user {UserId}. Errors: {Errors}", existingUser.Id, string.Join(", ", restoreResult.Errors.Select(e => e.Description)));
                    return new AccountResult { Succeeded = false, Errors = restoreResult.Errors };
                }
            }
            else
            {
                // User exists and is not soft-deleted, or belongs to different organization
                _logger.LogWarning("Cannot invite user {Email}: User already exists and is not eligible for restoration.", invitationRequestDto.Email);
                return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "A user with this email already exists." } } };
            }
        }

        // No existing user found, proceed with normal invitation flow
        _logger.LogInformation("No existing user found for {Email}. Creating new invitation.", invitationRequestDto.Email);
        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var invitation = new UserInvitation
        {
            Email = invitationRequestDto.Email,
            OrganizationId = callerOrganizationId.Value,
            Roles = string.Join(",", invitationRequestDto.Roles),
            Token = token,
            ExpiryDate = DateTime.UtcNow.AddDays(7),
            IsUsed = false
        };

        _context.UserInvitations.Add(invitation);
        await _context.SaveChangesAsync();

        var frontendBaseUrl = _configuration["FRONTEND_BASE_URL"] ?? _configuration["AppSettings:BaseUrl"] ?? "https://app.sagrafacile.it";
        var invitationLink = $"{frontendBaseUrl}/accetta-invito?token={token}";

        try
        {
            await _emailService.SendEmailAsync(
                invitation.Email,
                "Sei stato invitato a SagraFacile",
                $"<h1>Invito a SagraFacile</h1><p>Sei stato invitato a unirti a un'organizzazione su SagraFacile. Clicca sul seguente link per accettare l'invito e creare il tuo account: <a href='{invitationLink}'>Accetta Invito</a></p>"
            );
            _logger.LogInformation("Invitation email sent to {Email}.", invitation.Email);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to send invitation email to {Email}. Invitation was created but email failed.", invitation.Email);
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Could not send invitation email. Please try again later." } } };
        }

        return new AccountResult { 
            Succeeded = true, 
            Data = new { 
                Message = "Invitation sent successfully.",
                Action = "InvitationSent"
            } 
        };
    }

    public async Task<AccountResult> AcceptInvitationAsync(AcceptInvitationDto acceptDto)
    {
        var invitation = await _context.UserInvitations
            .FirstOrDefaultAsync(i => i.Token == acceptDto.Token && !i.IsUsed && i.ExpiryDate > DateTime.UtcNow);

        if (invitation == null)
        {
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Invalid or expired invitation token." } } };
        }

        var user = new User
        {
            UserName = invitation.Email,
            Email = invitation.Email,
            FirstName = acceptDto.FirstName,
            LastName = acceptDto.LastName,
            OrganizationId = invitation.OrganizationId,
            EmailConfirmed = true
        };

        var result = await _userManager.CreateAsync(user, acceptDto.Password);
        if (!result.Succeeded)
        {
            return new AccountResult { Succeeded = false, Errors = result.Errors };
        }

        var roles = invitation.Roles.Split(',');
        await _userManager.AddToRolesAsync(user, roles);

        invitation.IsUsed = true;
        await _context.SaveChangesAsync();

        return new AccountResult { Succeeded = true, Data = new { UserId = user.Id } };
    }

    public async Task<ServiceResult<InvitationDetailsDto>> GetInvitationDetailsAsync(string token)
    {
        var invitation = await _context.UserInvitations
            .Include(i => i.Organization)
            .FirstOrDefaultAsync(i => i.Token == token && !i.IsUsed && i.ExpiryDate > DateTime.UtcNow);

        if (invitation == null || invitation.Organization == null)
        {
            return ServiceResult<InvitationDetailsDto>.Fail("Invalid or expired invitation token.");
        }

        var details = new InvitationDetailsDto
        {
            Email = invitation.Email,
            OrganizationName = invitation.Organization.Name
        };

        return ServiceResult<InvitationDetailsDto>.Ok(details);
    }

    public async Task<IEnumerable<PendingInvitationDto>> GetPendingInvitationsAsync()
    {
        _logger.LogInformation("Fetching pending invitations.");
        var (callerOrganizationId, isCallerSuperAdmin) = GetUserContext();

        if (!callerOrganizationId.HasValue)
        {
            _logger.LogError("User organization context is missing when fetching pending invitations.");
            throw new InvalidOperationException("User organization context is missing.");
        }

        var pendingInvitations = await _context.UserInvitations
            .Where(i => i.OrganizationId == callerOrganizationId.Value && !i.IsUsed && i.ExpiryDate > DateTime.UtcNow)
            .Select(i => new PendingInvitationDto
            {
                Id = i.Id,
                Email = i.Email,
                Roles = i.Roles,
                ExpiryDate = i.ExpiryDate,
                InvitedAt = i.CreatedAt
            })
            .ToListAsync();

        _logger.LogInformation("Successfully fetched {InvitationCount} pending invitations.", pendingInvitations.Count);
        return pendingInvitations;
    }

    public async Task<AccountResult> RevokeInvitationAsync(Guid invitationId)
    {
        _logger.LogInformation("Attempting to revoke invitation with ID {InvitationId}.", invitationId);
        var (callerOrganizationId, isCallerSuperAdmin) = GetUserContext();

        if (!callerOrganizationId.HasValue)
        {
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "User context is missing OrganizationId." } } };
        }

        var invitation = await _context.UserInvitations
            .FirstOrDefaultAsync(i => i.Id == invitationId && i.OrganizationId == callerOrganizationId.Value && !i.IsUsed);

        if (invitation == null)
        {
            _logger.LogWarning("Invitation with ID {InvitationId} not found or already used.", invitationId);
            return new AccountResult { Succeeded = false, Errors = new List<IdentityError> { new IdentityError { Description = "Invitation not found or already used." } } };
        }

        _context.UserInvitations.Remove(invitation);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Invitation with ID {InvitationId} revoked successfully.", invitationId);
        return new AccountResult { Succeeded = true };
    }
}
