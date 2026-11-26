using System.Globalization;

namespace Inventory.Service;

/// <summary>
/// Stage 3 (Hiten, 26 Nov 2026): where something is kept – place number, rack number, level letter – shown as
/// <c>02-03-B</c> (Godown 02, Rack 03, Level B; level A is the bottom). The same address can be typed as <c>0203B</c>,
/// scanned from a rack label (its QR holds exactly that text) or picked from three drop-downs; all give the same result.
/// </summary>
public readonly record struct RackAddress(int Place, int Rack, char Level)
{
    /// <summary>"02-03-B" – how an address is shown on screen and printed.</summary>
    public string Text => $"{Place:00}-{Rack:00}-{Level}";

    /// <summary>"0203B" – what a rack label's QR holds (what a person would type).</summary>
    public string Code => $"{Place:00}{Rack:00}{Level}";

    public override string ToString() => Text;

    /// <summary>"Shop, Rack 1, Level B" – an address in full words, with the place's name.</summary>
    public static string Describe(string placeName, RackAddress address) => $"{placeName}, Rack {address.Rack}, Level {address.Level}";

    /// <summary>The letter of a level: 1 → A (the bottom), 2 → B …</summary>
    public static char LevelLetter(int level) => (char)('A' + level - 1);

    /// <summary>The number of a level letter: A → 1, B → 2 …</summary>
    public static int LevelNumber(char letter) => char.ToUpperInvariant(letter) - 'A' + 1;

    /// <summary>
    /// Reads "0203B", "02-03-B", "2-3-b", "02 03 B" or "2/3/B" (spaces, dashes, slashes and dots between the parts).
    /// Without separators it must be exactly two digits, two digits and a letter. Numbers 1–99, a letter A–Z.
    /// </summary>
    public static bool TryParse(string? text, out RackAddress address)
    {
        address = default;
        var clean = (text ?? "").Trim().ToUpperInvariant();
        if (clean.Length == 0)
        {
            return false;
        }

        string placePart, rackPart, levelPart;
        var parts = clean.Split(['-', ' ', '/', '.', '\t'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 3)
        {
            (placePart, rackPart, levelPart) = (parts[0], parts[1], parts[2]);
        }
        else if (parts.Length == 1 && clean.Length == 5)
        {
            (placePart, rackPart, levelPart) = (clean[..2], clean[2..4], clean[4..]);
        }
        else
        {
            return false;
        }

        if (!int.TryParse(placePart, NumberStyles.None, CultureInfo.InvariantCulture, out var place) || place is < 1 or > 99
            || !int.TryParse(rackPart, NumberStyles.None, CultureInfo.InvariantCulture, out var rack) || rack is < 1 or > 99
            || levelPart.Length != 1 || levelPart[0] is < 'A' or > 'Z')
        {
            return false;
        }

        address = new RackAddress(place, rack, levelPart[0]);
        return true;
    }
}

/// <summary>Stage 3: a rack level chosen in the address box – the rack's id and its address.</summary>
public sealed record RackChoice(int RackId, RackAddress Address);
