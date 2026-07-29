using System.ComponentModel;
using System.Runtime.CompilerServices;
using ConAuto.SharedEnums;

namespace ServerApplicationMobile.Services;

public sealed class AuthenticationService : INotifyPropertyChanged
{
    private const string SavedUserIdKey = "atek_login_user_id";
    private const string RememberSignInKey = "atek_remember_sign_in";
    private const string SavedPasswordKey = "atek_login_password";
    private readonly DatabaseService _databaseService;
    private AuthenticatedUser _currentUser;

    public AuthenticationService(DatabaseService databaseService)
    {
        _databaseService = databaseService;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public AuthenticatedUser CurrentUser
    {
        get => _currentUser;
        private set
        {
            if (ReferenceEquals(_currentUser, value))
                return;
            _currentUser = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentUser)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsAuthenticated)));
        }
    }

    public bool IsAuthenticated => CurrentUser != null;
    public bool CanEditCustomers => CurrentUser != null &&
        !string.Equals(CurrentUser.OEMName, "Controlled Automation", StringComparison.OrdinalIgnoreCase) &&
        (CurrentUser.Role == enumOEMUserRole.Admin || CurrentUser.Role == enumOEMUserRole.ServiceTech);
    public string SavedUserId => Preferences.Default.Get(SavedUserIdKey, string.Empty);
    public bool RememberSignIn => Preferences.Default.Get(RememberSignInKey, false);

    public async Task<SignInResult> SignInAsync(
        string userId,
        string password,
        bool rememberSignIn = false)
    {
        var normalizedUserId = userId?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedUserId))
            return SignInResult.Failed("Enter your service tech name or user ID.");

        try
        {
            var user = await _databaseService.AuthenticateAsync(normalizedUserId, password);
            if (user == null)
                return SignInResult.Failed("The user ID or password is incorrect.");

            CurrentUser = user;
            // Keep the entered name for OEM administrator logins as well. It is
            // their chat display identity and must not fall back to the device name.
            Preferences.Default.Set(SavedUserIdKey, normalizedUserId);
            await SaveSignInAsync(password, rememberSignIn);
            return SignInResult.Succeeded(user);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Authentication failed: {ex}");
            return SignInResult.Failed($"Unable to contact the license database: {GetRootMessage(ex)}");
        }
    }

    public async Task<SavedSignInCredentials> GetSavedSignInAsync()
    {
        if (!RememberSignIn)
            return null;

        try
        {
            var password = await SecureStorage.Default.GetAsync(SavedPasswordKey);
            return string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(SavedUserId)
                ? null
                : new SavedSignInCredentials(SavedUserId, password);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Reading saved sign-in failed: {ex.Message}");
            ForgetSavedSignIn();
            return null;
        }
    }

    public void SignOut(bool forgetSavedSignIn = false)
    {
        CurrentUser = null;
        if (forgetSavedSignIn)
            ForgetSavedSignIn();
    }

    public void ForgetSavedSignIn()
    {
        Preferences.Default.Set(RememberSignInKey, false);
        SecureStorage.Default.Remove(SavedPasswordKey);
    }

    private static async Task SaveSignInAsync(string password, bool rememberSignIn)
    {
        if (!rememberSignIn)
        {
            Preferences.Default.Set(RememberSignInKey, false);
            SecureStorage.Default.Remove(SavedPasswordKey);
            return;
        }

        try
        {
            await SecureStorage.Default.SetAsync(SavedPasswordKey, password);
            Preferences.Default.Set(RememberSignInKey, true);
        }
        catch (Exception ex)
        {
            Preferences.Default.Set(RememberSignInKey, false);
            System.Diagnostics.Debug.WriteLine($"Saving sign-in failed: {ex.Message}");
        }
    }

    private static string GetRootMessage(Exception exception)
    {
        while (exception.InnerException != null)
            exception = exception.InnerException;
        return exception.Message;
    }
}

public sealed record SavedSignInCredentials(string UserId, string Password);

public sealed class AuthenticatedUser
{
    public string UserID { get; init; } = string.Empty;
    public string OEMName { get; init; } = string.Empty;
    public string DisplayName { get; init; } = string.Empty;
    public enumOEMUserRole Role { get; init; } = enumOEMUserRole.User;
    public bool IsOemAdministrator { get; init; }
}

public sealed record SignInResult(bool Success, AuthenticatedUser User, string Error)
{
    public static SignInResult Succeeded(AuthenticatedUser user) => new(true, user, string.Empty);
    public static SignInResult Failed(string error) => new(false, null, error);
}
