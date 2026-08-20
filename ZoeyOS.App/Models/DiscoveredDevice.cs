namespace ZoeyOS.App.Models
{
    /// <summary>One device/entity found through an integration's auto-discovery.</summary>
    public class DiscoveredDevice
    {
        public string Source { get; set; } = "";   // "SmartThings" | "Home Assistant"
        public string Name { get; set; } = "";
        public string Detail { get; set; } = "";    // type or current state
    }
}
