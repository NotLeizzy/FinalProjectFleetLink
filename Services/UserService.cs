using System.Net.Mail;
using System.Text.RegularExpressions;
using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Helpers;
using FinalProjectFleetLink.Models;
using Microsoft.EntityFrameworkCore;

namespace FinalProjectFleetLink.Services;

public sealed record UserListItem(
    int UserId,
    string Email,
    string FullName,
    Role Role,
    int? BranchId,
    string BranchName,
    bool IsActive,
    DateTime CreatedAt);

public sealed record UserRequest(
    string Email,
    string FullName,
    Role Role,
    int? BranchId,
    string? Password,
    string? ConfirmPassword);

public sealed class UserService
{
    private static readonly Regex PasswordPattern = new("[A-Za-z]", RegexOptions.Compiled);
    private static readonly Regex PasswordNumberPattern = new("[0-9]", RegexOptions.Compiled);

    public async Task<IReadOnlyList<UserListItem>> GetUsersAsync(
        string? search,
        Role? role,
        int? branchId,
        bool includeInactive)
    {
        EnsureAdministrator();
        var currentUser = Session.CurrentUser!;
        if (!currentUser.IsHeadOffice && branchId != currentUser.BranchId)
        {
            throw new UnauthorizedAccessException("The selected branch is outside your access scope.");
        }

        await using var db = DbContextFactory.Create();
        var query = db.UserAccounts.AsNoTracking();
        if (!currentUser.IsHeadOffice)
        {
            query = query.Where(user => user.BranchId == currentUser.BranchId);
        }
        else if (branchId.HasValue)
        {
            query = query.Where(user => user.BranchId == branchId.Value);
        }
        if (!includeInactive)
        {
            query = query.Where(user => user.IsActive);
        }
        if (role.HasValue)
        {
            query = query.Where(user => user.Role == (int)role.Value);
        }
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim().ToLower();
            query = query.Where(user => user.Email.ToLower().Contains(term)
                || user.FullName.ToLower().Contains(term));
        }

        return await query
            .OrderBy(user => user.Email)
            .Select(user => new UserListItem(
                user.UserId,
                user.Email,
                user.FullName,
                (Role)user.Role,
                user.BranchId,
                user.Branch == null ? "Head Office" : user.Branch.BranchName,
                user.IsActive,
                user.CreatedAt))
            .ToListAsync();
    }

    public async Task<int> CreateAsync(UserRequest request)
    {
        EnsureAdministrator();
        var normalized = ValidateRequest(request, requirePassword: true);
        EnsureBranchScope(normalized.BranchId);

        await using var db = DbContextFactory.Create();
        if (await EmailExistsAsync(db, normalized.Email, null))
        {
            throw new InvalidOperationException("This email is already used by another account.");
        }

        var user = new UserAccount
        {
            Email = normalized.Email,
            FullName = normalized.FullName,
            Role = (int)normalized.Role,
            BranchId = normalized.BranchId,
            PasswordHash = PasswordHasher.Hash(normalized.Password!),
            IsActive = true,
            CreatedAt = DateTime.Now
        };
        db.UserAccounts.Add(user);
        await db.SaveChangesAsync();
        return user.UserId;
    }

    public async Task UpdateAsync(int userId, UserRequest request)
    {
        EnsureAdministrator();
        var normalized = ValidateRequest(request, requirePassword: false);
        EnsureBranchScope(normalized.BranchId);

        await using var db = DbContextFactory.Create();
        var user = await GetScopedUserAsync(db, userId);
        if (user is null)
        {
            throw new InvalidOperationException("The selected user was not found.");
        }
        if (!string.Equals(user.Email, normalized.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Email cannot be changed after the account is created.");
        }
        if (user.UserId == Session.CurrentUser!.UserId && (Role)user.Role != normalized.Role)
        {
            throw new InvalidOperationException("You cannot change your own role.");
        }
        await EnsureAdministratorIsNotDemotedAsync(db, user, normalized.Role);

        user.FullName = normalized.FullName;
        user.Role = (int)normalized.Role;
        user.BranchId = normalized.BranchId;
        await db.SaveChangesAsync();
    }

    public async Task SetActiveAsync(int userId, bool isActive)
    {
        EnsureAdministrator();
        await using var db = DbContextFactory.Create();
        var user = await GetScopedUserAsync(db, userId);
        if (user is null)
        {
            throw new InvalidOperationException("The selected user was not found.");
        }
        if (user.UserId == Session.CurrentUser!.UserId && !isActive)
        {
            throw new InvalidOperationException("You cannot deactivate your own account.");
        }
        if (!isActive)
        {
            await EnsureAdministratorIsNotRemovedAsync(db, user);
        }

        user.IsActive = isActive;
        await db.SaveChangesAsync();
    }

    public async Task ResetPasswordAsync(int userId, string password, string confirmPassword)
    {
        EnsureAdministrator();
        ValidatePassword(password, confirmPassword);
        await using var db = DbContextFactory.Create();
        var user = await GetScopedUserAsync(db, userId);
        if (user is null)
        {
            throw new InvalidOperationException("The selected user was not found.");
        }

        user.PasswordHash = PasswordHasher.Hash(password);
        await db.SaveChangesAsync();
    }

    private async Task<UserAccount?> GetScopedUserAsync(FleetLinkDbContext db, int userId)
    {
        var currentUser = Session.CurrentUser!;
        var query = db.UserAccounts.Where(user => user.UserId == userId);
        if (!currentUser.IsHeadOffice)
        {
            query = query.Where(user => user.BranchId == currentUser.BranchId);
        }
        return await query.SingleOrDefaultAsync();
    }

    private static async Task<bool> EmailExistsAsync(
        FleetLinkDbContext db,
        string email,
        int? excludedUserId)
    {
        return await db.UserAccounts.AnyAsync(user =>
            user.Email.ToLower() == email
            && (!excludedUserId.HasValue || user.UserId != excludedUserId.Value));
    }

    private static async Task EnsureAdministratorIsNotDemotedAsync(
        FleetLinkDbContext db,
        UserAccount user,
        Role newRole)
    {
        if ((Role)user.Role == Role.Administrator && user.IsActive && newRole != Role.Administrator)
        {
            await EnsureAnotherActiveAdministratorAsync(db, user.UserId);
        }
    }

    private static async Task EnsureAdministratorIsNotRemovedAsync(FleetLinkDbContext db, UserAccount user)
    {
        if ((Role)user.Role == Role.Administrator && user.IsActive)
        {
            await EnsureAnotherActiveAdministratorAsync(db, user.UserId);
        }
    }

    private static async Task EnsureAnotherActiveAdministratorAsync(FleetLinkDbContext db, int excludedUserId)
    {
        var exists = await db.UserAccounts.AnyAsync(user =>
            user.UserId != excludedUserId
            && user.IsActive
            && user.Role == (int)Role.Administrator);
        if (!exists)
        {
            throw new InvalidOperationException("The system must always have at least one active administrator.");
        }
    }

    private static UserRequest ValidateRequest(UserRequest request, bool requirePassword)
    {
        var email = NormalizeEmail(request.Email);
        var fullName = request.FullName.Trim();
        if (fullName.Length == 0)
        {
            throw new ArgumentException("Full name is required.");
        }
        if (!Enum.IsDefined(request.Role))
        {
            throw new ArgumentException("Select a valid role.");
        }
        if (requirePassword || request.Password is not null || request.ConfirmPassword is not null)
        {
            ValidatePassword(request.Password ?? string.Empty, request.ConfirmPassword ?? string.Empty);
        }
        return request with
        {
            Email = email,
            FullName = fullName,
            Password = request.Password
        };
    }

    private static string NormalizeEmail(string value)
    {
        var email = value.Trim().ToLowerInvariant();
        if (email.Length == 0 || email.Length > 100)
        {
            throw new ArgumentException("Email is required and must be at most 100 characters.");
        }
        try
        {
            var address = new MailAddress(email);
            if (!string.Equals(address.Address, email, StringComparison.Ordinal)
                || !address.Host.Contains('.', StringComparison.Ordinal))
            {
                throw new FormatException();
            }
        }
        catch (FormatException)
        {
            throw new ArgumentException("Enter a valid email address with a dotted domain.");
        }
        return email;
    }

    private static void ValidatePassword(string password, string confirmPassword)
    {
        if (password.Length < 8 || !PasswordPattern.IsMatch(password) || !PasswordNumberPattern.IsMatch(password))
        {
            throw new ArgumentException("Password must be at least 8 characters and contain at least one letter and one number.");
        }
        if (!string.Equals(password, confirmPassword, StringComparison.Ordinal))
        {
            throw new ArgumentException("Passwords do not match.");
        }
    }

    private static void EnsureAdministrator()
    {
        var user = Session.CurrentUser
            ?? throw new UnauthorizedAccessException("A logged-in user is required.");
        if (!Permissions.CanAccess(user.Role, Module.Users))
        {
            throw new UnauthorizedAccessException("Only administrators can manage user accounts.");
        }
    }

    private static void EnsureBranchScope(int? branchId)
    {
        var currentUser = Session.CurrentUser!;
        if (!currentUser.IsHeadOffice && branchId != currentUser.BranchId)
        {
            throw new UnauthorizedAccessException("A branch administrator can only manage users in their assigned branch.");
        }
    }
}
