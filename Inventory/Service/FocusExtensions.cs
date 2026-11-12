using Microsoft.JSInterop;

namespace Inventory.Service
{
    /// <summary>Moving the cursor from code (keyboard-friendly entry, owner's request Nov 2026).</summary>
    public static class FocusExtensions
    {
        /// <summary>
        /// Puts the cursor in the box matching <paramref name="selector"/> (an id like "#variantid" or a class such as
        /// ".focus-line-product"). Waits briefly if the box is still being drawn. Never fails the caller: a cursor that
        /// does not move is not worth an error message.
        /// </summary>
        public static async Task FocusBoxAsync(this IJSRuntime js, string selector)
        {
            try
            {
                await js.InvokeVoidAsync("inventoryShortcuts.focus", selector);
            }
            catch (JSException)
            {
            }
            catch (JSDisconnectedException)
            {
            }
            catch (TaskCanceledException)
            {
            }
        }
    }
}
