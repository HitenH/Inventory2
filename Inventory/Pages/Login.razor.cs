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

        private LoginModel loginModel = new();
        private EditContext? editContext;
        private ValidationMessageStore? messageStore;

        protected override void OnInitialized()
        {
            editContext = new EditContext(loginModel);

            messageStore = new(editContext);
        }
        public async Task SignIn()
        {
            try
            {
                var user = await UserRepository.GetByName(loginModel.Name);
                if (user != null)
                {
                    if (user.Password != loginModel.Password)
                    {
                        messageStore.Add(() => loginModel.Password, "Invalid Password!");
                    }
                    else
                    {
                        var customAuthStateProvider = (CustomAuthenticationProvider)authStateProvider;
                        await customAuthStateProvider.UpdateAuthenticationState(new UserSession()
                        {
                            UserName = user.Name,
                            Role = user.Role
                        });
                        navManager.NavigateTo("/", true);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.LogError("Login error: " + ex.Message);
            }
           
        }
    }
}
