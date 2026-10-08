using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Threading;
using Aurora.App.Services;

namespace Aurora.App
{
    public partial class App : Application
    {
        public static AppSettings Settings { get; set; } = null!;
        public static ProfileManager Profiles { get; private set; } = null!;
        public static SpeakerRecognitionService SpeakerRecognition { get; } = new();
        public static MemoryStore Memory { get; private set; } = null!;
        public static IChatEngine AI { get; private set; } = null!;
        public static ImageGenClient ImageGen { get; private set; } = null!;
        public static SmartThingsClient SmartThings { get; private set; } = null!;
        public static HomeAssistantClient HomeAssistant { get; private set; } = null!;
        public static HubitatClient Hubitat { get; private set; } = null!;
        public static VoiceService Voice { get; private set; } = null!;
        public static WakeWordService WakeWord { get; private set; } = null!;
        public static WeatherClient Weather { get; private set; } = null!;
        public static WebSearchClient WebSearch { get; private set; } = null!;
        public static SpotifyClient Spotify { get; private set; } = null!;
        public static CameraService Camera { get; private set; } = null!;
        public static McpService Mcp { get; private set; } = null!;
        public static WindowsAutomationService WindowsAutomation { get; private set; } = null!;
        public static ActionRecoveryService ActionRecovery { get; private set; } = null!;
        public static AppAdapterService AppAdapters { get; private set; } = null!;
        public static SystemMetricsService Metrics { get; private set; } = null!;
        public static WindowsUpdaterService Updater { get; private set; } = null!;
        public static InstallationSecurityService Security { get; private set; } = null!;
        public static EmergencyStopButtonService EmergencyStopButtons { get; private set; } = null!;
        public static event EventHandler? AIProviderStatusChanged;
        public static ProfileRecoveryCodeStore RecoveryCodes { get; } = new();
        private static Views.LockdownWindow? _lockdownWindow;

        protected override async void OnStartup(StartupEventArgs e)
        {
            try
            {
                base.OnStartup(e);
                ShutdownMode = ShutdownMode.OnExplicitShutdown;
                var firstRun = !AppSettings.HasSavedConfiguration;
                Settings = AppSettings.LoadOrCreate();
                Profiles = new ProfileManager();
                Profiles.Initialize(Settings);
                Updater = new WindowsUpdaterService();
                Security = new InstallationSecurityService(System.IO.Path.Combine(AppSettings.ConfigDir, "security.lock"));
                EmergencyStopButtons = new EmergencyStopButtonService();

                if (firstRun)
                {
                    var welcome = new Views.FirstRunWindow();
                    if (welcome.ShowDialog() != true)
                    {
                        Shutdown();
                        return;
                    }

                    await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                    while (!AppSettings.HasSavedConfiguration)
                    {
                        var setup = new Views.SetupWindow(restartOnSave: false)
                        {
                            Topmost = true,
                            ShowInTaskbar = true,
                            WindowState = WindowState.Normal
                        };
                        setup.Loaded += (_, _) =>
                        {
                            setup.Activate();
                            setup.Focus();
                        };
                        setup.ShowDialog();
                        setup.Topmost = false;

                        if (!AppSettings.HasSavedConfiguration)
                        {
                            MessageBox.Show(
                                "Aurora needs a provider API key before it can continue. The setup wizard will remain open until setup is complete.",
                                "Aurora setup required",
                                MessageBoxButton.OK,
                                MessageBoxImage.Information);
                        }
                    }
                }

                var bootstrap = new Views.StartupPermissionWindow();
                bootstrap.Show();
                bootstrap.Activate();
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.Loaded);

                var permissions = new WindowsPermissionService();
                await bootstrap.AskPermissionAsync("Location", permissions.RequestLocationAsync);
                await bootstrap.AskPermissionAsync("Microphone", permissions.RequestMicrophoneAsync);
                await bootstrap.AskPermissionAsync("Camera", permissions.RequestCameraAsync);
                Settings.WindowsFilesEnabled = await bootstrap.AskCapabilityPermissionAsync("Files");
                Settings.WindowsScreenEnabled = await bootstrap.AskCapabilityPermissionAsync("Screen capture");
                Settings.WindowsClipboardEnabled = await bootstrap.AskCapabilityPermissionAsync("Clipboard");
                Settings.WindowsApplicationsEnabled = await bootstrap.AskCapabilityPermissionAsync("Applications");
                Settings.WindowsTerminalEnabled = await bootstrap.AskCapabilityPermissionAsync("Terminal");
                Settings.WindowsUiAutomationEnabled = await bootstrap.AskCapabilityPermissionAsync("UI automation");
                Settings.WindowsNetworkEnabled = await bootstrap.AskCapabilityPermissionAsync("Network");
                Settings.WindowsPowerEnabled = await bootstrap.AskCapabilityPermissionAsync("Power controls");
                Settings.Save();

                System.Diagnostics.Debug.WriteLine("[Startup] Permission choices complete. Creating dashboard.");

                Memory = new MemoryStore(Settings.DatabasePath);
                Memory.SetActiveProfile(Settings.ProfileId);
                Memory.Initialize();
                ActionRecovery = new ActionRecoveryService(RestoreClipboardTextAsync);
                Weather = new WeatherClient();

                try { Metrics = new SystemMetricsService(); }
                catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Startup] Metrics initialization failed: {ex}"); }

                var mainWindow = new Views.MainWindow();
                MainWindow = mainWindow;
                ShutdownMode = ShutdownMode.OnMainWindowClose;
                mainWindow.Show();
                mainWindow.Activate();
                bootstrap.Close();
                if (Security.IsLockedDown) ShowLockdownOverlay(mainWindow);

                try
                {
                    AI = BuildChatEngine();
                    AIProviderStatusChanged?.Invoke(null, EventArgs.Empty);
                    ImageGen = BuildImageGenClient();
                    SmartThings = new SmartThingsClient(Settings.SmartThingsToken);
                    HomeAssistant = new HomeAssistantClient(Settings.HomeAssistantUrl, Settings.HomeAssistantToken);
                    Hubitat = new HubitatClient(Settings.HubitatUrl, Settings.HubitatToken);
                    Voice = new VoiceService(Settings.VoiceName);
                    WakeWord = new WakeWordService();
                    WebSearch = new WebSearchClient();
                    Spotify = BuildSpotifyClient();
                    Camera = new CameraService();
                    Mcp = new McpService();
                    WindowsAutomation = CreateWindowsService();
                    AppAdapters = new AppAdapterService(WindowsAutomation);

                    try { WakeWord.Start(); }
                    catch (Exception ex) { System.Diagnostics.Debug.WriteLine($"[Startup] Wake word start failed: {ex}"); }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[Startup] Optional service initialization failed: {ex}");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString(), "Startup failure");
                Shutdown(-1);
            }
        }

        private static WindowsAutomationService CreateWindowsService()
        {
            return new WindowsAutomationService(ActionRecovery)
            {
                FilesEnabled = Settings.WindowsFilesEnabled,
                ScreenEnabled = Settings.WindowsScreenEnabled,
                ClipboardEnabled = Settings.WindowsClipboardEnabled,
                ApplicationsEnabled = Settings.WindowsApplicationsEnabled,
                TerminalEnabled = Settings.WindowsTerminalEnabled,
                UiAutomationEnabled = Settings.WindowsUiAutomationEnabled,
                NetworkEnabled = Settings.WindowsNetworkEnabled,
                PowerEnabled = Settings.WindowsPowerEnabled
            };
        }
        public static void RefreshWindowsPermissions() { WindowsAutomation = CreateWindowsService(); AppAdapters = new AppAdapterService(WindowsAutomation); }
        private static SpotifyClient BuildSpotifyClient() { var client = new SpotifyClient(Settings.SpotifyClientId, Settings.SpotifyRefreshToken); client.RefreshTokenRotated += newToken => { Settings.SpotifyRefreshToken = newToken; Settings.Save(); }; return client; }
        private static bool ActiveProviderIsConfigured() => Settings.ChatProvider switch { "groq" => !string.IsNullOrWhiteSpace(Settings.GroqApiKey), "openai" => !string.IsNullOrWhiteSpace(Settings.OpenAIApiKey), "claude" => !string.IsNullOrWhiteSpace(Settings.ClaudeApiKey), "copilot" => !string.IsNullOrWhiteSpace(Settings.GitHubCopilotApiKey), _ => !string.IsNullOrWhiteSpace(Settings.GeminiApiKey) };
        private static IChatEngine BuildChatEngine()
        {
            var isLockedDown = Security?.IsLockedDown == true;
            return Settings.ChatProvider switch
            {
                "groq" => new GroqClient(isLockedDown ? "" : Settings.GroqApiKey, Settings.GroqModel),
                "openai" => new OpenAIClient(isLockedDown ? "" : Settings.OpenAIApiKey, Settings.OpenAIModel),
                "claude" => new ClaudeClient(isLockedDown ? "" : Settings.ClaudeApiKey, Settings.ClaudeModel),
                "copilot" => new GitHubCopilotClient(isLockedDown ? "" : Settings.GitHubCopilotApiKey, Settings.GitHubCopilotModel),
                _ => new GeminiClient(isLockedDown ? "" : Settings.GeminiApiKey, Settings.GeminiModel)
            };
        }
        private static ImageGenClient BuildImageGenClient() { var key = Settings.ImageProvider == "openai" ? Settings.ImageProviderApiKey : Settings.GeminiApiKey; return new ImageGenClient(key, Settings.ImageProvider); }
        public static void ShowLockdownOverlay(Window? targetWindow = null)
        {
            if (Security == null || !Security.IsLockedDown) return;
            Current.Dispatcher.Invoke(() =>
            {
                if (_lockdownWindow != null)
                {
                    _lockdownWindow.Activate();
                    return;
                }

                var target = targetWindow ?? Current.Windows.OfType<Window>()
                    .FirstOrDefault(window => window.IsActive && window is not Views.LockdownWindow)
                    ?? Current.MainWindow;
                var workArea = GetMonitorWorkArea(target);

                _lockdownWindow = new Views.LockdownWindow(
                    Security,
                    VerifyRecoveryCode,
                    HasRecoveryCode,
                    ClearLockdownOverlay,
                    workArea);
                _lockdownWindow.Show();
                _lockdownWindow.Activate();
            });
        }
        private static Rect GetMonitorWorkArea(Window? window)
        {
            if (window == null)
                return SystemParameters.WorkArea;

            var helper = new WindowInteropHelper(window);
            var handle = helper.Handle;
            if (handle == IntPtr.Zero)
                return SystemParameters.WorkArea;

            var monitor = MonitorFromWindow(handle, MONITOR_DEFAULTTONEAREST);
            if (monitor == IntPtr.Zero)
                return SystemParameters.WorkArea;

            var info = new MONITORINFO { cbSize = Marshal.SizeOf<MONITORINFO>() };
            return GetMonitorInfo(monitor, ref info)
                ? new Rect(info.rcWork.Left, info.rcWork.Top, info.rcWork.Right - info.rcWork.Left, info.rcWork.Bottom - info.rcWork.Top)
                : SystemParameters.WorkArea;
        }
        private const uint MONITOR_DEFAULTTONEAREST = 2;
        [DllImport("user32.dll")]
        private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint dwFlags);
        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetMonitorInfo(IntPtr hMonitor, ref MONITORINFO lpmi);
        [StructLayout(LayoutKind.Sequential)]
        private struct MONITORINFO
        {
            public int cbSize;
            public RECT rcMonitor;
            public RECT rcWork;
            public uint dwFlags;
        }
        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }
        private static bool VerifyRecoveryCode(string code)
        {
            if (Profiles?.ActiveProfile == null)
                return false;

            return RecoveryCodes.VerifyCode(Profiles.ActiveProfile.Id, code);
        }
        private static bool HasRecoveryCode()
        {
            if (Profiles?.ActiveProfile == null)
                return false;

            return RecoveryCodes.HasCode(Profiles.ActiveProfile.Id);
        }
        private static void ClearLockdownOverlay()
        {
            _lockdownWindow = null;
            foreach (Window window in Current.Windows)
                if (window is not Views.LockdownWindow) window.IsEnabled = true;
        }

        private static Task RestoreClipboardTextAsync(string? text)
        {
            return Current.Dispatcher.InvokeAsync(() =>
            {
                if (text is null)
                    System.Windows.Clipboard.Clear();
                else
                    System.Windows.Clipboard.SetText(text);
            }).Task;
        }

        public static void EnterLockdown(string reason)
        {
            var target = Current.Windows.OfType<Window>()
                .FirstOrDefault(window => window.IsActive && window is not Views.LockdownWindow)
                ?? Current.MainWindow;

            Security.EnterLockdown(reason);
            foreach (Window window in Current.Windows)
                if (window is not Views.LockdownWindow) window.IsEnabled = false;
            ShowLockdownOverlay(target);
        }
        public static void EmergencyStopProvider()
        {
            if (Settings == null) throw new InvalidOperationException("Aurora settings are not initialized.");

            AI?.RevokeCredentials();
            if (Settings.ChatProvider == "gemini" && Settings.ImageProvider != "openai")
                ImageGen?.RevokeCredentials();
            Settings.ClearActiveChatProviderCredential();
            AI = BuildChatEngine();
            AIProviderStatusChanged?.Invoke(null, EventArgs.Empty);
            if (Settings.ChatProvider == "gemini")
                ImageGen = BuildImageGenClient();

            try
            {
                Settings.Save();
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("The active AI provider was disconnected for this session, but its cleared credential could not be saved to local settings.", ex);
            }
        }

        public static void RefreshIntegrationClients() { Profiles?.SaveActiveSettings(); SmartThings = new SmartThingsClient(Settings.SmartThingsToken); HomeAssistant = new HomeAssistantClient(Settings.HomeAssistantUrl, Settings.HomeAssistantToken); Hubitat = new HubitatClient(Settings.HubitatUrl, Settings.HubitatToken); ImageGen = BuildImageGenClient(); Spotify = BuildSpotifyClient(); AI = BuildChatEngine(); AIProviderStatusChanged?.Invoke(null, EventArgs.Empty); RefreshWindowsPermissions(); }
        public static void ResetEverythingAndRestart()
        {
            var databasePath = Settings?.DatabasePath;
            Memory?.Dispose();
            Voice?.Dispose();
            WakeWord?.Dispose();
            Camera?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Mcp?.DisposeAsync().AsTask().GetAwaiter().GetResult();
            Metrics?.Dispose();
            AppSettings.ResetAll(databasePath);

            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(processPath))
            {
                var arguments = Environment.GetCommandLineArgs()
                                      .Skip(1)
                    .Select(QuoteProcessArgument);
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = processPath,
                    Arguments = string.Join(" ", arguments),
                    UseShellExecute = true
                });
            }

            Environment.Exit(0);
        }
        private static string QuoteProcessArgument(string argument) =>
            argument.Contains(' ') || argument.Contains('"')
                ? $"\"{argument.Replace("\"", "\\\"")}\""
                : argument;
        protected override void OnExit(ExitEventArgs e) { EmergencyStopButtons?.Dispose(); Memory?.Dispose(); Voice?.Dispose(); WakeWord?.Dispose(); Camera?.DisposeAsync().AsTask().GetAwaiter().GetResult(); Mcp?.DisposeAsync().AsTask().GetAwaiter().GetResult(); Metrics?.Dispose(); base.OnExit(e); }
    }
}
