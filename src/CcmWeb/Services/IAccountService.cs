namespace CcmWeb.Services;

public enum LoginResult
{
    Success,
    InvalidCredentials,
    Locked
}

/// <summary>The signed-in employee, taken from ACCOUNT and EMPLOYEE.</summary>
public sealed record AuthenticatedUser(int AccountID, int EmployeeID, string Username, string FullName, string Role);

public sealed record LoginOutcome(LoginResult Result, AuthenticatedUser? User);

public interface IAccountService
{
    /// <summary>
    /// Checks a username and password. A wrong username and a wrong password produce the same
    /// result; a locked account is only reported once the password is correct.
    /// </summary>
    Task<LoginOutcome> AuthenticateAsync(string username, string password);
}
