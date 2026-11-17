namespace FinalProjectFleetLink.Helpers;

public static class PasswordHasher
{
    public static string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);
        return BCrypt.Net.BCrypt.EnhancedHashPassword(password, BCrypt.Net.HashType.SHA512);
    }

    public static bool Verify(string password, string hash)
    {
        return !string.IsNullOrWhiteSpace(password)
            && !string.IsNullOrWhiteSpace(hash)
            && BCrypt.Net.BCrypt.EnhancedVerify(password, hash);
    }
}
