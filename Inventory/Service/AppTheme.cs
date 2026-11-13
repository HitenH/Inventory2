using MudBlazor;

namespace Inventory.Service
{
    /// <summary>The colour choices and the MudBlazor theme built from them (Batch 17 UI refresh).</summary>
    public static class AppTheme
    {
        public static readonly string[] Colours = { "Teal", "Blue", "Green", "Plum", "Slate" };

        private static readonly Dictionary<string, string> LightPrimary = new()
        {
            ["Teal"] = "#0F6E8C",
            ["Blue"] = "#2456A6",
            ["Green"] = "#2F7D4F",
            ["Plum"] = "#6B3FA0",
            ["Slate"] = "#4B5563",
        };

        // Lighter versions of the same colours, used as the accent in dark mode.
        private static readonly Dictionary<string, string> DarkPrimary = new()
        {
            ["Teal"] = "#4FB3D1",
            ["Blue"] = "#7FA6E8",
            ["Green"] = "#6CC498",
            ["Plum"] = "#B48AE0",
            ["Slate"] = "#A3AEBB",
        };

        /// <summary>The light-mode hex of a colour choice (used for the swatches on Settings).</summary>
        public static string LightHex(string? colour) => LightPrimary[Normalise(colour)];

        /// <summary>Returns the colour if it is one of <see cref="Colours"/>, otherwise "Teal".</summary>
        public static string Normalise(string? colour)
        {
            return Colours.Contains(colour, StringComparer.Ordinal) ? colour! : "Teal";
        }

        public static MudTheme Build(string colour)
        {
            colour = Normalise(colour);
            var light = LightPrimary[colour];
            var dark = DarkPrimary[colour];
            var fonts = new[] { "Inter", "Segoe UI", "system-ui", "Arial", "sans-serif" };

            var theme = new MudTheme
            {
                PaletteLight = new PaletteLight
                {
                    Primary = light,
                    PrimaryContrastText = "#FFFFFF",
                    Secondary = light,
                    SecondaryContrastText = "#FFFFFF",
                    Tertiary = "#D8E0E7",
                    TertiaryContrastText = "#1B2630",
                    Error = "#C62828",
                    ErrorContrastText = "#FFFFFF",
                    Success = "#1F7A4D",
                    Warning = light,
                    WarningContrastText = "#FFFFFF",
                    Info = "#D8E0E7",
                    InfoContrastText = "#1B2630",
                    Background = "#F4F6F8",
                    Surface = "#FFFFFF",
                    AppbarBackground = light,
                    AppbarText = "#FFFFFF",
                    DrawerBackground = "#FFFFFF",
                    DrawerText = "#1B2630",
                    TextPrimary = "#1B2630",
                    TextSecondary = "#5B6B78",
                    LinesDefault = "#DCE3E9",
                    LinesInputs = "#C5CFD8",
                    TableLines = "#DCE3E9",
                    TableStriped = "#F8FAFB",
                    TableHover = "#EEF2F5",
                },
                PaletteDark = new PaletteDark
                {
                    Primary = dark,
                    PrimaryContrastText = "#0B1116",
                    Secondary = dark,
                    SecondaryContrastText = "#0B1116",
                    Tertiary = "#334150",
                    TertiaryContrastText = "#E6EBEF",
                    Error = "#E57373",
                    ErrorContrastText = "#1A0B0B",
                    Success = "#62C58F",
                    Warning = dark,
                    WarningContrastText = "#0B1116",
                    Info = "#334150",
                    InfoContrastText = "#E6EBEF",
                    Background = "#151B21",
                    Surface = "#1C242C",
                    AppbarBackground = "#1C242C",
                    AppbarText = "#E6EBEF",
                    DrawerBackground = "#1C242C",
                    DrawerText = "#E6EBEF",
                    TextPrimary = "#E6EBEF",
                    TextSecondary = "#94A3B0",
                    LinesDefault = "#2C3741",
                    LinesInputs = "#3A4753",
                    TableLines = "#2C3741",
                    TableStriped = "#1A222A",
                    TableHover = "#222C35",
                },
                LayoutProperties = new LayoutProperties
                {
                    DefaultBorderRadius = "6px",
                },
                Typography = new Typography
                {
                    Default = new DefaultTypography { FontFamily = fonts },
                    H1 = new H1Typography { FontFamily = fonts },
                    H2 = new H2Typography { FontFamily = fonts },
                    H3 = new H3Typography { FontFamily = fonts },
                    H4 = new H4Typography { FontFamily = fonts },
                    H5 = new H5Typography { FontFamily = fonts },
                    H6 = new H6Typography { FontFamily = fonts },
                    Button = new ButtonTypography { FontFamily = fonts, TextTransform = "none" },
                    Body1 = new Body1Typography { FontFamily = fonts },
                    Body2 = new Body2Typography { FontFamily = fonts },
                    Caption = new CaptionTypography { FontFamily = fonts },
                    Subtitle1 = new Subtitle1Typography { FontFamily = fonts },
                    Subtitle2 = new Subtitle2Typography { FontFamily = fonts },
                },
            };
            return theme;
        }
    }
}
