using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ZoeyOS.App.Services
{
    /// <summary>
    /// Tool definitions + execution for the Home companion. Claude decides which
    /// device and action to use; this class is the only thing that actually
    /// talks to SmartThings / Home Assistant.
    /// </summary>
    public static class HomeTools
    {
        public static List<object> Definitions => new()
        {
            new
            {
                name = "list_smart_home_devices",
                description = "Lists every device/entity the user has connected via SmartThings and/or Home Assistant, " +
                               "including current state where available. Call this before controlling a device if you " +
                               "don't already know its id from earlier in the conversation.",
                input_schema = new { type = "object", properties = new { } }
            },
            new
            {
                name = "control_smart_home_device",
                description = "Turns a smart home device on or off through SmartThings or Home Assistant.",
                input_schema = new
                {
                    type = "object",
                    properties = new
                    {
                        source = new
                        {
                            type = "string",
                            @enum = new[] { "smartthings", "home_assistant" },
                            description = "Which integration the device belongs to."
                        },
                        device_id = new
                        {
                            type = "string",
                            description = "For SmartThings: the deviceId. For Home Assistant: the entity_id (e.g. light.kitchen)."
                        },
                        action = new
                        {
                            type = "string",
                            @enum = new[] { "on", "off" },
                            description = "The action to perform."
                        }
                    },
                    required = new[] { "source", "device_id", "action" }
                }
            }
        };

        public static async Task<string> ExecuteAsync(string toolName, JsonElement input) => toolName switch
        {
            "list_smart_home_devices" => await ListAllAsync(),
            "control_smart_home_device" => await ControlAsync(input),
            _ => $"Unknown tool: {toolName}"
        };

        private static async Task<string> ListAllAsync()
        {
            var sb = new StringBuilder();

            if (App.SmartThings.IsConfigured)
            {
                var devices = await App.SmartThings.ListDevicesAsync();
                sb.AppendLine("SmartThings devices:");
                foreach (var d in devices)
                    sb.AppendLine($"- id={d.DeviceId}, label=\"{d.Label}\", type={d.Type}");
            }
            else
            {
                sb.AppendLine("SmartThings: not connected.");
            }

            if (App.HomeAssistant.IsConfigured)
            {
                var devices = await App.HomeAssistant.ListDevicesAsync();
                sb.AppendLine("Home Assistant entities:");
                foreach (var d in devices)
                    sb.AppendLine($"- entity_id={d.EntityId}, name=\"{d.FriendlyName}\", state={d.State}");
            }
            else
            {
                sb.AppendLine("Home Assistant: not connected.");
            }

            return sb.ToString();
        }

        private static async Task<string> ControlAsync(JsonElement input)
        {
            var source = input.TryGetProperty("source", out var s) ? s.GetString() ?? "" : "";
            var deviceId = input.TryGetProperty("device_id", out var d) ? d.GetString() ?? "" : "";
            var action = input.TryGetProperty("action", out var a) ? a.GetString() ?? "" : "";

            if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(action))
                return "Missing source, device_id, or action.";

            if (source == "smartthings")
            {
                if (!App.SmartThings.IsConfigured) return "SmartThings is not connected.";
                var command = action == "on" ? "on" : "off";
                var ok = await App.SmartThings.SendCommandAsync(deviceId, "switch", command);
                return ok
                    ? $"SmartThings device {deviceId} turned {command}."
                    : $"Failed to turn {command} SmartThings device {deviceId}.";
            }

            if (source == "home_assistant")
            {
                if (!App.HomeAssistant.IsConfigured) return "Home Assistant is not connected.";
                var domain = deviceId.Contains('.') ? deviceId.Split('.')[0] : "homeassistant";
                var service = action == "on" ? "turn_on" : "turn_off";
                var ok = await App.HomeAssistant.CallServiceAsync(domain, service, deviceId);
                return ok
                    ? $"Home Assistant entity {deviceId} set to {service}."
                    : $"Failed to call {service} on {deviceId}.";
            }

            return $"Unknown source: {source}";
        }
    }
}
