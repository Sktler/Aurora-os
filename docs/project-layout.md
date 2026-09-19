# Project layout

```
Aurora.sln
Aurora.App/
  App.xaml / App.xaml.cs        - startup, wires up services, provider
                                   selection, full reset
  Models/                       - Companion, ChatMessage, DiscoveredDevice
  Services/                     - AppSettings, IChatEngine (shared interface),
                                   GeminiClient, GroqClient, VoiceService,
                                   WeatherClient, WebSearchClient,
                                   SpotifyClient, SpotifyAuthClient,
                                   SystemVolumeControl, MemoryStore,
                                   ImageGenClient, SmartThingsClient,
                                   HomeAssistantClient, HubitatClient,
                                   HomeTools, SmartHomeCatalogService,
                                   GoogleAuthClient, SystemTools, FileTools
  ViewModels/                   - DashboardViewModel, CompanionViewModel,
                                   IntegrationsViewModel
  Views/                        - MainWindow.xaml, IntegrationsWindow.xaml
                                   (now the Settings window: engine switch,
                                   reset, voice, volume, real connection
                                   tests, Google/Spotify OAuth), SetupWindow.xaml
                                   (first-run + reopenable provider/key
                                   setup), Converters.cs (all XAML value
                                   converters in one file rather than one
                                   file each)
```
