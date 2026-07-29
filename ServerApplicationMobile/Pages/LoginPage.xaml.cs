using Microsoft.Extensions.DependencyInjection;
using ServerApplicationMobile.Services;

namespace ServerApplicationMobile;

public partial class LoginPage : ContentPage
{
    private readonly AuthenticationService _authenticationService;
    private readonly CustomerDataService _customerDataService;
    private readonly ChatService _chatService;
    private readonly IServiceProvider _services;
    private bool _isSigningIn;
    private bool _autoLoginAttempted;

    public LoginPage(
        AuthenticationService authenticationService,
        CustomerDataService customerDataService,
        ChatService chatService,
        IServiceProvider services)
    {
        InitializeComponent();
        _authenticationService = authenticationService;
        _customerDataService = customerDataService;
        _chatService = chatService;
        _services = services;
        entrUsername.Text = authenticationService.SavedUserId;
        RememberSignInCheckBox.IsChecked = authenticationService.RememberSignIn;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_autoLoginAttempted)
            return;

        _autoLoginAttempted = true;
        var savedSignIn = await _authenticationService.GetSavedSignInAsync();
        if (savedSignIn == null)
            return;

        entrUsername.Text = savedSignIn.UserId;
        entrPassword.Text = savedSignIn.Password;
        RememberSignInCheckBox.IsChecked = true;
        await SignInAsync();
    }

    private async void OnLoginClicked(object sender, EventArgs e)
    {
        await SignInAsync();
    }

    private async void OnPasswordCompleted(object sender, EventArgs e)
    {
        await SignInAsync();
    }

    private async Task SignInAsync()
    {
        if (_isSigningIn || string.IsNullOrWhiteSpace(entrPassword.Text))
            return;

        SetBusy(true);
        ErrorLabel.IsVisible = false;

        try
        {
            var result = await _authenticationService.SignInAsync(
                entrUsername.Text,
                entrPassword.Text,
                RememberSignInCheckBox.IsChecked);

            if (!result.Success)
            {
                ErrorLabel.Text = result.Error;
                ErrorLabel.IsVisible = true;
                return;
            }

            entrPassword.Text = string.Empty;
            _customerDataService.StartLoading();
            _chatService.StartConnecting();

            if (Window != null)
                Window.Page = _services.GetRequiredService<AppTabbedPage>();
        }
        finally
        {
            SetBusy(false);
        }
    }

    private void OnCredentialsChanged(object sender, TextChangedEventArgs e)
    {
        UpdateLoginButton();
    }

    private void SetBusy(bool isBusy)
    {
        _isSigningIn = isBusy;
        ActivityIndicator.IsRunning = isBusy;
        ActivityIndicator.IsVisible = isBusy;
        entrUsername.IsEnabled = !isBusy;
        entrPassword.IsEnabled = !isBusy;
        UpdateLoginButton();
    }

    private void UpdateLoginButton()
    {
        btnLogin.IsEnabled = !_isSigningIn &&
            !string.IsNullOrWhiteSpace(entrUsername.Text) &&
            !string.IsNullOrWhiteSpace(entrPassword.Text);
    }
}
