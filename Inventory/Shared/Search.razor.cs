using Microsoft.AspNetCore.Components;

namespace Inventory.Shared
{
    public partial class Search
    {
        [Parameter] public EventCallback<ChangeEventArgs> SearchItemCallBack { get; set; }
    }
}
