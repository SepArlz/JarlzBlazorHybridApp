namespace GabsHybridApp.Shared.States;
public static class StorageConstants
{
    public static string Preference = "serverPreference";
    public static bool IsDarkMode = false;
    public static string AppWebUrl = "https://localhost:7034";
    public static string AppMasterSecret = "BE8A1E514ED4A6CF9E858EB24FC3D";

    // Standard Station PINs (100% offline, zero database dependency)
    public static readonly HashSet<string> AllowedStationPins = new(StringComparer.Ordinal)
    {
        "9110", // Emergency code
        "1234", // Quick shift PIN
        "0000"  // Station backup PIN
    };

    public static bool IsValidStationPin(string? pin) =>
        !string.IsNullOrWhiteSpace(pin) && AllowedStationPins.Contains(pin.Trim());
}