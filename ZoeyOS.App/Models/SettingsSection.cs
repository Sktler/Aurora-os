namespace ZoeyOS.App.Models
{
    /// <summary>Which page the Settings window is currently showing. Hub is the landing
    /// page (a grid of large tile buttons); every other value is a dedicated page reached
    /// by tapping a tile, with a "back to Settings" button to return to Hub.</summary>
    public enum SettingsSection
    {
        Hub,
        Engine,
        Models,
        Companions,
        Voice,
        Documents,
        System,
        Music,
        SmartHome,
        Google,
        Developer,
        Memory
    }
}
