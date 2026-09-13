using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Aurora.App.Models;

namespace Aurora.App.Services
{
    public class SmartHomeCatalogSnapshot
    {
        public List<DiscoveredDevice> Devices { get; } = new();
        public Dictionary<string, string> SourceStatuses { get; } = new(StringComparer.OrdinalIgnoreCase);

        public int CountFor(string source) => Devices.Count(d => string.Equals(d.Source, source, StringComparison.OrdinalIgnoreCase));
    }

    public static class SmartHomeCatalogService
    {
        public static async Task<SmartHomeCatalogSnapshot> DiscoverAsync()
        {
            var snapshot = new SmartHomeCatalogSnapshot();

            await LoadSmartThingsAsync(snapshot);
            await LoadHomeAssistantAsync(snapshot);
            await LoadHubitatAsync(snapshot);

            return snapshot;
        }

        public static List<DiscoveredDevice> BuildCatalog(
            IEnumerable<SmartThingsDevice>? smartThingsDevices = null,
            IEnumerable<HomeAssistantDevice>? homeAssistantDevices = null,
            IEnumerable<HubitatDevice>? hubitatDevices = null)
        {
            var result = new List<DiscoveredDevice>();

            if (smartThingsDevices != null)
            {
                result.AddRange(smartThingsDevices.Select(d => CreateCatalogItem(
                    "SmartThings",
                    d.DeviceId,
                    d.Label,
                    d.Type,
                    d.State,
                    d.Room,
                    d.Capabilities,
                    d.SupportedActions,
                    d.RawMetadata)));
            }

            if (homeAssistantDevices != null)
            {
                result.AddRange(homeAssistantDevices.Select(d => CreateCatalogItem(
                    "Home Assistant",
                    d.EntityId,
                    d.FriendlyName,
                    d.Domain,
                    d.State,
                    d.Room,
                    d.Capabilities,
                    d.SupportedActions,
                    d.RawMetadata)));
            }

            if (hubitatDevices != null)
            {
                result.AddRange(hubitatDevices.Select(d => CreateCatalogItem(
                    "Hubitat",
                    d.DeviceId,
                    d.Label,
                    d.Type,
                    d.State,
                    d.Room,
                    d.Capabilities,
                    d.SupportedActions,
                    d.RawMetadata)));
            }

            return result
                .OrderBy(static d => d.Source)
                .ThenBy(static d => d.Name)
                .ToList();
        }

        public static List<DiscoveredDevice> Filter(
            IEnumerable<DiscoveredDevice> devices,
            string? source = null,
            string? room = null,
            string? type = null,
            string? capability = null)
        {
            var query = devices;

            if (!string.IsNullOrWhiteSpace(source))
                query = query.Where(d => string.Equals(d.Source, source.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(room))
                query = query.Where(d => d.Room.Contains(room.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(type))
                query = query.Where(d =>
                    d.Type.Contains(type.Trim(), StringComparison.OrdinalIgnoreCase) ||
                    d.Name.Contains(type.Trim(), StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(capability))
                query = query.Where(d => d.Capabilities.Any(c => c.Contains(capability.Trim(), StringComparison.OrdinalIgnoreCase)));

            return query.OrderBy(d => d.Source).ThenBy(d => d.Name).ToList();
        }

        public static string Format(IEnumerable<DiscoveredDevice> devices, bool includeRawMetadata)
        {
            var lines = new List<string>();
            foreach (var device in devices)
            {
                lines.Add($"- source={device.Source}, id={device.Id}, name=\"{device.Name}\", type={device.TypeSummary}, state={device.StateSummary}, room={device.LocationSummary}");
                lines.Add($"  capabilities: {device.CapabilitiesSummary}");
                lines.Add($"  actions: {device.SupportedActionsSummary}");
                if (includeRawMetadata && !string.IsNullOrWhiteSpace(device.RawMetadata))
                    lines.Add($"  raw: {device.RawMetadata}");
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static async Task LoadSmartThingsAsync(SmartHomeCatalogSnapshot snapshot)
        {
            if (!App.SmartThings.IsConfigured)
            {
                snapshot.SourceStatuses["SmartThings"] = "Not connected.";
                return;
            }

            try
            {
                var devices = await App.SmartThings.ListDevicesAsync();
                snapshot.Devices.AddRange(BuildCatalog(smartThingsDevices: devices));
                snapshot.SourceStatuses["SmartThings"] = $"Loaded {devices.Count} device(s).";
            }
            catch (Exception ex)
            {
                snapshot.SourceStatuses["SmartThings"] = $"Discovery failed: {ex.Message}";
            }
        }

        private static async Task LoadHomeAssistantAsync(SmartHomeCatalogSnapshot snapshot)
        {
            if (!App.HomeAssistant.IsConfigured)
            {
                snapshot.SourceStatuses["Home Assistant"] = "Not connected.";
                return;
            }

            try
            {
                var devices = await App.HomeAssistant.ListDevicesAsync();
                snapshot.Devices.AddRange(BuildCatalog(homeAssistantDevices: devices));
                snapshot.SourceStatuses["Home Assistant"] = $"Loaded {devices.Count} device(s).";
            }
            catch (Exception ex)
            {
                snapshot.SourceStatuses["Home Assistant"] = $"Discovery failed: {ex.Message}";
            }
        }

        private static async Task LoadHubitatAsync(SmartHomeCatalogSnapshot snapshot)
        {
            if (!App.Hubitat.IsConfigured)
            {
                snapshot.SourceStatuses["Hubitat"] = "Not connected.";
                return;
            }

            try
            {
                var devices = await App.Hubitat.ListDevicesAsync();
                snapshot.Devices.AddRange(BuildCatalog(hubitatDevices: devices));
                snapshot.SourceStatuses["Hubitat"] = $"Loaded {devices.Count} device(s).";
            }
            catch (Exception ex)
            {
                snapshot.SourceStatuses["Hubitat"] = $"Discovery failed: {ex.Message}";
            }
        }

        private static DiscoveredDevice CreateCatalogItem(
            string source,
            string id,
            string name,
            string type,
            string state,
            string room,
            IReadOnlyList<string> capabilities,
            IReadOnlyList<string> supportedActions,
            string rawMetadata)
        {
            var safeName = string.IsNullOrWhiteSpace(name) ? id : name;
            var detailParts = new List<string>();
            if (!string.IsNullOrWhiteSpace(type)) detailParts.Add(type);
            if (!string.IsNullOrWhiteSpace(state)) detailParts.Add($"state: {state}");
            if (!string.IsNullOrWhiteSpace(room)) detailParts.Add($"room: {room}");

            return new DiscoveredDevice
            {
                Source = source,
                Id = id,
                Name = safeName,
                Type = type,
                State = state,
                Room = room,
                Detail = detailParts.Count > 0 ? string.Join(" · ", detailParts) : "No details reported.",
                Capabilities = capabilities.Where(static c => !string.IsNullOrWhiteSpace(c)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static c => c).ToList(),
                SupportedActions = supportedActions.Where(static a => !string.IsNullOrWhiteSpace(a)).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(static a => a).ToList(),
                RawMetadata = rawMetadata
            };
        }
    }
}
