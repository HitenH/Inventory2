using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components;
using Inventory.Authentication;

namespace Inventory.Shared
{
    public partial class MainLayout
    {
        [Inject] private AuthenticationStateProvider authStateProvider { get; set; }
        [Inject] private NavigationManager navManager { get; set; }

        private bool showMaster;
        private bool showTransaction;

        protected override void OnInitialized()
        {
            showMaster = false;
            showTransaction = false;
        }
        public async Task Logout()
        {
            var customAuthStateProvider = (CustomAuthenticationProvider)authStateProvider;
            await customAuthStateProvider.UpdateAuthenticationState(null);
            navManager.NavigateTo("/", true);
        }

        public void ShowMaster()
        {
            showMaster = !showMaster;
            showTransaction = false;
        }

        public void ShowTransaction()
        {
            showTransaction = !showTransaction;
            showMaster = false;
        }
    }
}
