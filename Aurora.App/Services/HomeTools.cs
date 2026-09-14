using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace Aurora.App.Services
{
    /// <summary>
    /// Tool definitions + execution for the Home companion. Claude decides which
    /// device and action to use; this class is the only thing that actually
    /// talks to SmartThings / Home Assistant / Hubitat.
    /// </summary>
    public static class HomeTools
    {
        public static List<object> Definitions => new()
        {
            new
            {
                name = "list_smart_home_devices",
                description = "Lists the unified smart-home catalog across SmartThings, Home Assistant, and Hubitat. " +
                               "Supports filtering by source, room, type, or capability, and can include raw metadata.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        source = new { type = "string", description = "Optional source filter: smartthings, home_assistant, or hubitat." },
                        room = new { type = "string", description = "Optional room/location filter." },
                        type = new { type = "string", description = "Optional device type/domain filter." },
                        capability = new { type = "string", description = "Optional capability filter." },
                        include_raw_metadata = new { type = "boolean", description = "Set true to include raw hub metadata for each item." }
                    }
                }
            },
            new
            {
                name = "control_smart_home_device",
                description = "Routes an action to the correct connected smart-home hub.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        source = new
                        {
                            type = "string",
                            @enum = new[] { "smartthings", "home_assistant", "hubitat" },
                            description = "Which integration the device belongs to."
                        },
                        device_id = new
                        {
                            type = "string",
                            description = "For SmartThings: the deviceId. For Home Assistant: the entity_id. For Hubitat: the Maker API device id."
                        },
                        action = new
                        {
                            type = "string",
                            description = "The action to perform, e.g. on, off, lock, unlock, open, close, or set_level."
                        },
                        value = new
                        {
                            type = "string",
                            description = "Optional value for actions like set_level, set_position, set_temperature, or hub-specific commands. For Hubitat, separate multiple arguments with |."
                        }
                    },
                    required = new[] { "source", "device_id", "action" }
                }
            }
        };

        public static async Task<string> ExecuteAsync(string toolName, JsonElement input) => toolName switch
        {
            "list_smart_home_devices" => await ListAllAsync(input),
            "control_smart_home_device" => await ControlAsync(input),
            _ => $"Unknown tool: {toolName}"
        };

        private static async Task<string> ListAllAsync(JsonElement input)
        {
            var snapshot = await SmartHomeCatalogService.DiscoverAsync();
            var source = input.TryGetProperty("source", out var sourceValue) ? NormalizeSource(sourceValue.GetString()) : "";
            var room = input.TryGetProperty("room", out var roomValue) ? roomValue.GetString() ?? "" : "";
            var type = input.TryGetProperty("type", out var typeValue) ? typeValue.GetString() ?? "" : "";
            var capability = input.TryGetProperty("capability", out var capabilityValue) ? capabilityValue.GetString() ?? "" : "";
            var includeRawMetadata = App.Settings.SmartHomeCatalogIncludeRawMetadata ||
                                     (input.TryGetProperty("include_raw_metadata", out var rawValue) &&
                                     rawValue.ValueKind == JsonValueKind.True);

            var filtered = SmartHomeCatalogService.Filter(snapshot.Devices, source, room, type, capability);
            var sb = new StringBuilder();
            sb.AppendLine("Catalog summary:");
            AppendSourceStatus(sb, "SmartThings", snapshot);
            AppendSourceStatus(sb, "Home Assistant", snapshot);
            AppendSourceStatus(sb, "Hubitat", snapshot);

            if (filtered.Count == 0)
            {
                sb.Append("No devices matched the requested filters.");
                return sb.ToString();
            }

            sb.AppendLine();
            sb.AppendLine($"Matching devices ({filtered.Count}):");
            sb.Append(SmartHomeCatalogService.Format(filtered, includeRawMetadata));
            return sb.ToString();
        }

        private static async Task<string> ControlAsync(JsonElement input)
        {
            var source = input.TryGetProperty("source", out var s) ? s.GetString() ?? "" : "";
            var deviceId = input.TryGetProperty("device_id", out var d) ? d.GetString() ?? "" : "";
            var action = input.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";
            var value = input.TryGetProperty("value", out var v) ? v.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(action))
                return "Missing source, device_id, or action.";

            var normalizedSource = NormalizeSource(source);
            var snapshot = await SmartHomeCatalogService.DiscoverAsync();
            var device = snapshot.Devices.FirstOrDefault(dv =>
                string.Equals(dv.Source, normalizedSource, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(dv.Id, deviceId, StringComparison.OrdinalIgnoreCase));

            if (device == null)
                return $"No catalog item with id {deviceId} was found for {normalizedSource}. Call list_smart_home_devices first.";

            if (normalizedSource == "SmartThings")
            {
                if (!App.SmartThings.IsConfigured) return "SmartThings is not connected.";
                if (!SmartThingsClient.TryResolveCommand(device.Capabilities, action, value, out var capabilityName, out var command, out var args, out var error))
                    return error;
                var ok = await App.SmartThings.SendCommandAsync(deviceId, capabilityName, command, args);
                return ok
                    ? $"SmartThings device {deviceId} ran {command}."
                    : $"Failed to run {command} on SmartThings device {deviceId}.";
            }

            if (normalizedSource == "Home Assistant")
            {
                if (!App.HomeAssistant.IsConfigured) return "Home Assistant is not connected.";
                if (!HomeAssistantClient.TryResolveService(deviceId, device.Capabilities, action, value, out var domain, out var service, out var body, out var error))
                    return error;
                var ok = await App.HomeAssistant.CallServiceAsync(domain, service, body);
                return ok
                    ? $"Home Assistant entity {deviceId} ran {service}."
                    : $"Failed to call {service} on {deviceId}.";
            }

            if (normalizedSource == "Hubitat")
            {
                if (!App.Hubitat.IsConfigured) return "Hubitat is not connected.";
                var ok = await App.Hubitat.SendCommandAsync(deviceId, action, SplitValue(value));
                return ok
                    ? $"Hubitat device {deviceId} ran {action}."
                    : $"Failed to run {action} on Hubitat device {deviceId}.";
            }

            return $"Unknown source: {source}";
        }

        private static void AppendSourceStatus(StringBuilder sb, string source, SmartHomeCatalogSnapshot snapshot)
        {
            var status = snapshot.SourceStatuses.TryGetValue(source, out var value) ? value : "No status.";
            sb.AppendLine($"- {source}: {snapshot.CountFor(source)} item(s). {status}");
        }

        private static string NormalizeSource(string? source) => (source ?? "").Trim() switch
        {
            "smartthings" => "SmartThings",
            "home_assistant" => "Home Assistant",
            "hubitat" => "Hubitat",
            _ => source?.Trim() ?? ""
        };

        private static string[] SplitValue(string? value) =>
            string.IsNullOrWhiteSpace(value)
                ? Array.Empty<string>()
                : value.Split('|').Select(static part => part.Trim()).Where(static part => part.Length > 0).ToArray();
    }
}
