using FinalProjectFleetLink.Data;
using FinalProjectFleetLink.Models;

namespace FinalProjectFleetLink.Helpers;

public sealed record LoggedInUser(
    int UserId,
    string Email,
    string FullName,
    Role Role,
    int? BranchId)
{
    public bool IsHeadOffice => BranchId is null;
}

public static class Session
{
    public static LoggedInUser? CurrentUser { get; private set; }

    public static bool IsLoggedIn => CurrentUser is not null;

    public static bool IsHeadOffice => CurrentUser?.IsHeadOffice ?? false;

    public static bool MustChangePassword { get; private set; }

    public static void Login(UserAccount user)
    {
        ArgumentNullException.ThrowIfNull(user);
        Login(user, null);
    }

    public static void Login(UserAccount user, string? password)
    {
        ArgumentNullException.ThrowIfNull(user);
        CurrentUser = new LoggedInUser(
            user.UserId,
            user.Email,
            user.FullName,
            (Role)user.Role,
            user.BranchId);
        MustChangePassword = string.Equals(password, DatabaseSeeder.DefaultPassword, StringComparison.Ordinal);
    }

    public static void Logout()
    {
        CurrentUser = null;
        MustChangePassword = false;
    }
}
