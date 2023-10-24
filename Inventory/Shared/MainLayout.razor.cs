using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;
using Inventory.Authentication;

namespace Inventory.Shared
{
    public partial class MainLayout
    {
        [Inject] private AuthenticationStateProvider authStateProvider { get; set; }
        [Inject] private NavigationManager navManager { get; set; }
        public async Task Logout()
        {
            var customAuthStateProvider = (CustomAuthenticationProvider)authStateProvider;
            await customAuthStateProvider.UpdateAuthenticationState(null);
            navManager.NavigateTo("/", true);
        }
    }
}
