namespace Inventory.Service
{
    /// <summary>
    /// The colour and mode the user picked (Batch 17). Shared with the pages through a CascadingValue
    /// set up in MainLayout; Settings changes it and MainLayout reacts through <see cref="Changed"/>.
    /// </summary>
    public class AppThemeState
    {
        public string Colour { get; set; } = "Teal";

        /// <summary>Light | Dark | System</summary>
        public string Mode { get; set; } = "System";

        public event Action? Changed;

        public void Set(string colour, string mode)
        {
            Colour = AppTheme.Normalise(colour);
            Mode = mode is "Light" or "Dark" ? mode : "System";
            Changed?.Invoke();
        }
    }
}
