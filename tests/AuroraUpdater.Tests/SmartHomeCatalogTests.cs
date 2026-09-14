using System.Collections.Generic;
using Aurora.App.Models;
using Aurora.App.Services;
using Xunit;

namespace AuroraUpdater.Tests;

public class SmartHomeCatalogTests
{
    [Fact]
    public void HomeAssistant_ParseStatesPayload_ExtractsCatalogMetadata()
    {
        const string payload = """
        [
          {
            "entity_id": "light.kitchen",
            "state": "on",
            "attributes": {
              "friendly_name": "Kitchen Light",
              "brightness": 128,
              "area_id": "Kitchen"
            }
          },
          {
            "entity_id": "cover.garage",
            "state": "closed",
            "attributes": {
              "friendly_name": "Garage Door",
              "current_position": 0
            }
          }
        ]
        """;

        var devices = HomeAssistantClient.ParseStatesPayload(payload);

        Assert.Equal(2, devices.Count);
        Assert.Contains(devices[0].Capabilities, c => c == "brightness");
        Assert.Contains(devices[0].SupportedActions, a => a == "set_level");
        Assert.Contains(devices[1].SupportedActions, a => a == "open");
        Assert.Equal("Kitchen", devices[0].Room);
        Assert.Equal("cover", devices[1].Domain);
    }

    [Fact]
    public void Hubitat_ParseDevicesPayload_ExtractsCommandsAndPrimaryState()
    {
        const string payload = """
        [
          {
            "id": "17",
            "label": "Hall Lamp",
            "type": "Generic Component Dimmer",
            "capabilities": ["Switch", "SwitchLevel"],
            "commands": ["on", "off", "setLevel"],
            "attributes": [
              { "name": "switch", "currentValue": "off" },
              { "name": "level", "currentValue": 25 }
            ],
            "roomName": "Hallway"
          }
        ]
        """;

        var devices = HubitatClient.ParseDevicesPayload(payload);

        var device = Assert.Single(devices);
        Assert.Equal("17", device.DeviceId);
        Assert.Equal("off", device.State);
        Assert.Contains(device.SupportedActions, a => a == "setLevel");
        Assert.Equal("Hallway", device.Room);
    }

    [Fact]
    public void BuildCatalog_MergesAllSourcesIntoUnifiedItems()
    {
        var catalog = SmartHomeCatalogService.BuildCatalog(
            smartThingsDevices: new[]
            {
                new SmartThingsDevice("st-1", "Porch Plug", "switch", "", "", new[] { "switch" }, new[] { "on", "off" }, "{}")
            },
            homeAssistantDevices: new[]
            {
                new HomeAssistantDevice("light.kitchen", "Kitchen Light", "on", "light", "Kitchen", new[] { "light", "brightness" }, new[] { "on", "off", "set_level" }, "{}")
            },
            hubitatDevices: new[]
            {
                new HubitatDevice("17", "Hall Lamp", "dimmer", "off", "Hallway", new[] { "SwitchLevel" }, new[] { "setLevel" }, "{}")
            });

        Assert.Equal(3, catalog.Count);
        Assert.Contains(catalog, d => d.Source == "SmartThings" && d.Id == "st-1");
        Assert.Contains(catalog, d => d.Source == "Home Assistant" && d.Capabilities.Contains("brightness"));
        Assert.Contains(catalog, d => d.Source == "Hubitat" && d.SupportedActions.Contains("setLevel"));
    }

    [Fact]
    public void Format_IncludesRawMetadata_WhenRequested()
    {
        var device = new DiscoveredDevice
        {
            Source = "Hubitat",
            Id = "17",
            Name = "Hall Lamp",
            Type = "dimmer",
            State = "off",
            Room = "Hallway",
            Capabilities = new List<string> { "Switch" },
            SupportedActions = new List<string> { "setLevel" },
            RawMetadata = "{\"note\":\"bedside lamp\"}"
        };

        var formatted = SmartHomeCatalogService.Format(new[] { device }, includeRawMetadata: true);

        Assert.Contains("raw: {\"note\":\"bedside lamp\"}", formatted);
    }

    [Fact]
    public void SmartThings_TryResolveCommand_MapsSetLevel()
    {
        var ok = SmartThingsClient.TryResolveCommand(
            new[] { "switch", "switchLevel" },
            "set_level",
            "42",
            out var capability,
            out var command,
            out var args,
            out var error);

        Assert.True(ok, error);
        Assert.Equal("switchLevel", capability);
        Assert.Equal("setLevel", command);
        Assert.Equal(new object[] { 42 }, args);
    }

    [Fact]
    public void HomeAssistant_TryResolveService_MapsCoverAndBrightnessActions()
    {
        var coverOk = HomeAssistantClient.TryResolveService(
            "cover.garage",
            new[] { "cover", "position" },
            "set_position",
            "75",
            out var domain,
            out var service,
            out var body,
            out var error);

        Assert.True(coverOk, error);
        Assert.Equal("cover", domain);
        Assert.Equal("set_cover_position", service);
        Assert.Equal(75, body["position"]);

        var lightOk = HomeAssistantClient.TryResolveService(
            "light.kitchen",
            new[] { "light", "brightness" },
            "set_level",
            "60",
            out _,
            out var lightService,
            out var lightBody,
            out var lightError);

        Assert.True(lightOk, lightError);
        Assert.Equal("turn_on", lightService);
        Assert.Equal(60, lightBody["brightness_pct"]);
    }
}
