using System.Collections.Generic;
using System.Linq;

namespace Aurora.App.Models
{
    /// <summary>One normalized smart-home or camera device surfaced by discovery.</summary>
    public class DiscoveredDevice
    {
        public string Source { get; set; } = "";   // "SmartThings" | "Home Assistant" | "Hubitat"
        public string Name { get; set; } = "";
        public string Detail { get; set; } = "";    // type or current state
        public string Id { get; set; } = "";
        public string Type { get; set; } = "";
        public string State { get; set; } = "";
        public string Room { get; set; } = "";
        public List<string> Capabilities { get; set; } = new();
        public List<string> SupportedActions { get; set; } = new();
        public string RawMetadata { get; set; } = "";

        public string CapabilitiesSummary => Capabilities.Count == 0 ? "None reported" : string.Join(", ", Capabilities);
        public string SupportedActionsSummary => SupportedActions.Count == 0 ? "None reported" : string.Join(", ", SupportedActions);
        public string LocationSummary => string.IsNullOrWhiteSpace(Room) ? "No room reported" : Room;
        public string StateSummary => string.IsNullOrWhiteSpace(State) ? "Unknown" : State;
        public string TypeSummary => string.IsNullOrWhiteSpace(Type) ? "Unknown type" : Type;
        public string CatalogSummary =>
            string.Join(" · ", new[] { TypeSummary, StateSummary, LocationSummary }.Where(x => !string.IsNullOrWhiteSpace(x)));
    }
}
