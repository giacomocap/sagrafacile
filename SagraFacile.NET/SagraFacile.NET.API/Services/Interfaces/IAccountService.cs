using Microsoft.AspNetCore.Identity;
using SagraFacile.NET.API.DTOs;
using SagraFacile.NET.API.Models.Results;

namespace SagraFacile.NET.API.Services.Interfaces;

// Define a result type to encapsulate success/failure and potential data/errors
public class AccountResult
{
    public bool Succeeded { get; set; }
    public IEnumerable<IdentityError>? Errors { get; set; }
    public object? Data { get; set; } // To return user info or token later
}

public class LoginResult : AccountResult
{
    public bool RequiresTwoFactor { get; set; }
    public bool IsLockedOut { get; set; }
    public bool IsNotAllowed { get; set; }
}

public interface IAccountService
{
    Task<AccountResult> RegisterUserAsync(RegisterDto registerDto);
    Task<LoginResult> LoginUserAsync(LoginDto loginDto);
    Task<AccountResult> AssignRolesAsync(AssignRolesDto assignRolesDto);
    Task<IEnumerable<UserDto>> GetUsersAsync(); // Added for User Management
    Task<UserDto?> GetUserByIdAsync(string userId); // Added for getting a single user
    Task<AccountResult> UpdateUserAsync(string userId, UpdateUserDto updateUserDto);
    Task<AccountResult> DeleteUserAsync(string userId);
    Task<IEnumerable<string>> GetRolesAsync(); // List all available roles
    Task<AccountResult> CreateRoleAsync(CreateRoleDto createRoleDto); // Create a new role
    Task<TokenResponseDto?> RefreshTokenAsync(string refreshToken);
    Task<AccountResult> ConfirmEmailAsync(string userId, string token);
    Task<AccountResult> ForgotPasswordAsync(ForgotPasswordDto forgotPasswordDto);
    Task<AccountResult> ResetPasswordAsync(ResetPasswordDto resetPasswordDto);
    Task<AccountResult> InviteUserAsync(UserInvitationRequestDto invitationRequestDto);
    Task<AccountResult> AcceptInvitationAsync(AcceptInvitationDto acceptInvitationDto);
    Task<ServiceResult<InvitationDetailsDto>> GetInvitationDetailsAsync(string token);
    Task<IEnumerable<PendingInvitationDto>> GetPendingInvitationsAsync();
    Task<AccountResult> RevokeInvitationAsync(Guid invitationId);
}
