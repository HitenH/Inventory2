using Inventory.Authentication;
using Inventory.Domain.Repository.Abstract;
using Inventory.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Forms;


namespace Inventory.Pages
{
    public partial class Login
    {
        [Inject] private IUserRepository UserRepository { get; set; }
        [Inject] private ILogger<Login> Logger { get; set; }
        [Inject] private AuthenticationStateProvider authStateProvider { get; set; }
        [Inject] private NavigationManager navManager { get; set; }
        [Inject] private PasswordService Passwords { get; set; } = default!;
        [Inject] private Inventory.Service.ActivityLogService Activity { get; set; } = default!;

        /// <summary>The page the user tried to open before being sent to login (set by RedirectToLogin).</summary>
        [SupplyParameterFromQuery] public string? ReturnUrl { get; set; }

        private LoginModel loginModel = new();

        /// <summary>Only allow going back to a page inside this app (never to another website).</summary>
        private string SafeReturnUrl()
        {
            var url = ReturnUrl?.Trim();
            if (string.IsNullOrEmpty(url)
                || url.Contains("://")
                || url.StartsWith("/") || url.StartsWith("\\")
                || url.StartsWith("login", StringComparison.OrdinalIgnoreCase))
            {
                return "/";
            }

            return "/" + url;
        }
        private EditContext? editContext;
        private ValidationMessageStore? messageStore;
        private string? errorMessage;

        protected override void OnInitialized()
        {
            loginModel = new();
            editContext = new EditContext(loginModel);
            messageStore = new(editContext);
        }
        public async Task SignIn()
        {
            errorMessage = null;
            try
            {
                var user = await UserRepository.GetByName(loginModel.Name?.Trim() ?? string.Empty);
                var rehashNeeded = false;
                var ok = user != null && Passwords.Verify(user, loginModel.Password, out rehashNeeded);

                if (!ok)
                {
                    // Same message for unknown user and wrong password, so names cannot be guessed.
                    // Batch 1c-3: the activity log keeps the name that was typed (no password is ever written).
                    await Activity.TryWriteAsync(Inventory.Service.ActivityLogService.LoginRefused,
                        $"Login refused for '{loginModel.Name?.Trim()}': wrong name or password", null, Logger);
                    errorMessage = "Invalid user name or password.";
                    loginModel.Password = "";
                    return;
                }

                // Checked only after a correct password, so the message does not reveal which names exist.
                if (user!.IsDisabled)
                {
                    errorMessage = "This user is disabled. Ask the Admin.";
                    await Activity.TryWriteAsync(Inventory.Service.ActivityLogService.LoginRefused, "Login refused: this user is disabled", user.Name, Logger);
                    loginModel.Password = "";
                    return;
                }

                if (rehashNeeded)
                {
                    user!.Password = Passwords.Hash(user, loginModel.Password);
                    await UserRepository.Update(user);
                }

                await Activity.TryWriteAsync(Inventory.Service.ActivityLogService.Login, "Logged in", user!.Name, Logger);
                var customAuthStateProvider = (CustomAuthenticationProvider)authStateProvider;
                await customAuthStateProvider.UpdateAuthenticationState(new UserSession()
                {
                    UserName = user!.Name
                });
                navManager.NavigateTo(SafeReturnUrl(), true);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Login error");
                errorMessage = "Login failed because of an unexpected error. Please try again.";
            }
        }
    }
}
