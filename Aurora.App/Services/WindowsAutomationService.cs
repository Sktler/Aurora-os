using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;

namespace Aurora.App.Services
{
    /// <summary>
    /// Permissioned Windows capabilities exposed to Aurora. Sensitive operations are opt-in.
    /// </summary>
    public sealed class WindowsAutomationService
    {
        public bool FilesEnabled { get; set; }
        public bool ScreenEnabled { get; set; }
        public bool ClipboardEnabled { get; set; }
        public bool ApplicationsEnabled { get; set; }
        public bool TerminalEnabled { get; set; }
        public bool UiAutomationEnabled { get; set; }
        public bool NetworkEnabled { get; set; }
        public bool PowerEnabled { get; set; }

        public static WindowsAutomationService FromSettings(AppSettings settings)
        {
            ArgumentNullException.ThrowIfNull(settings);
            return new WindowsAutomationService
            {
                FilesEnabled = settings.WindowsFilesEnabled,
                ScreenEnabled = settings.WindowsScreenEnabled,
                ClipboardEnabled = settings.WindowsClipboardEnabled,
                ApplicationsEnabled = settings.WindowsApplicationsEnabled,
                TerminalEnabled = settings.WindowsTerminalEnabled,
                UiAutomationEnabled = settings.WindowsUiAutomationEnabled,
                NetworkEnabled = settings.WindowsNetworkEnabled,
                PowerEnabled = settings.WindowsPowerEnabled
            };
        }

        public IReadOnlyList<ProcessInfo> GetProcesses()
        {
            if (!ApplicationsEnabled) throw new UnauthorizedAccessException("Application access is disabled.");
            return Process.GetProcesses()
                .Select(p => new ProcessInfo(p.Id, p.ProcessName, SafeMainWindowTitle(p)))
                .OrderBy(p => p.Name)
                .ToList();
        }

        public void Launch(string executableOrPath)
        {
            if (!ApplicationsEnabled) throw new UnauthorizedAccessException("Application access is disabled.");
            if (string.IsNullOrWhiteSpace(executableOrPath)) throw new ArgumentException("An application path or command is required.");
            if (!ActionApprovalCenter.ConfirmAction("windows_launch_application", $"Launch application: {executableOrPath}"))
                throw new UnauthorizedAccessException("Application launch was denied by Aurora approval policy.");
            Process.Start(new ProcessStartInfo { FileName = executableOrPath, UseShellExecute = true });
        }

        public void OpenPath(string path)
        {
            if (!FilesEnabled) throw new UnauthorizedAccessException("File access is disabled.");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A path is required.");
            if (!ActionApprovalCenter.ConfirmAction("windows_open_path", $"Open path: {path}"))
                throw new UnauthorizedAccessException("Opening the requested path was denied by Aurora approval policy.");
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }

        public async Task<string> ReadTextAsync(string path, CancellationToken cancellationToken = default)
        {
            if (!FilesEnabled) throw new UnauthorizedAccessException("File access is disabled.");
            return await File.ReadAllTextAsync(path, cancellationToken);
        }

        public async Task WriteTextAsync(string path, string content, CancellationToken cancellationToken = default)
        {
            if (!FilesEnabled) throw new UnauthorizedAccessException("File access is disabled.");
            if (!ActionApprovalCenter.ConfirmAction("windows_write_file", $"Write file: {path}"))
                throw new UnauthorizedAccessException("Writing the requested file was denied by Aurora approval policy.");
            await File.WriteAllTextAsync(path, content ?? string.Empty, cancellationToken);
        }

        public async Task<string> GetClipboardTextAsync(CancellationToken cancellationToken = default)
        {
            if (!ClipboardEnabled) throw new UnauthorizedAccessException("Clipboard access is disabled.");
            return await Application.Current.Dispatcher.InvokeAsync(
                () => Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty,
                System.Windows.Threading.DispatcherPriority.Normal,
                cancellationToken);
        }

        public async Task SetClipboardTextAsync(string text, CancellationToken cancellationToken = default)
        {
            if (!ClipboardEnabled) throw new UnauthorizedAccessException("Clipboard access is disabled.");
            if (!ActionApprovalCenter.ConfirmAction("windows_set_clipboard", "Set clipboard text"))
                throw new UnauthorizedAccessException("Changing the clipboard was denied by Aurora approval policy.");
            await Application.Current.Dispatcher.InvokeAsync(
                () => Clipboard.SetText(text ?? string.Empty),
                System.Windows.Threading.DispatcherPriority.Normal,
                cancellationToken);
        }

        public BitmapSource CaptureScreen()
        {
            if (!ScreenEnabled) throw new UnauthorizedAccessException("Screen access is disabled.");
            var bounds = new System.Drawing.Rectangle(0, 0, (int)SystemParameters.PrimaryScreenWidth, (int)SystemParameters.PrimaryScreenHeight);
            using var bitmap = new Bitmap(bounds.Width, bounds.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var graphics = Graphics.FromImage(bitmap)) graphics.CopyFromScreen(bounds.Left, bounds.Top, 0, 0, bitmap.Size);
            using var stream = new MemoryStream();
            bitmap.Save(stream, ImageFormat.Png);
            stream.Position = 0;
            var image = new BitmapImage();
            image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.StreamSource = stream; image.EndInit(); image.Freeze();
            return image;
        }

        public async Task<int> RunApprovedCommandAsync(
            string fileName,
            string arguments,
            CancellationToken cancellationToken = default)
        {
            if (!TerminalEnabled) throw new UnauthorizedAccessException("Terminal access is disabled.");
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("A command is required.");
            if (!ActionApprovalCenter.ConfirmAction("windows_run_command", $"Run command: {fileName} {arguments}".Trim()))
                throw new UnauthorizedAccessException("The requested command was denied by Aurora approval policy.");
            var tcs = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = fileName,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = false,
                    CreateNoWindow = true
                }
            };

            void CompleteProcess()
            {
                try { tcs.TrySetResult(process.ExitCode); }
                catch (InvalidOperationException) { tcs.TrySetResult(-1); }
            }

            process.EnableRaisingEvents = true;
            EventHandler processExited = (_, _) => CompleteProcess();
            process.Exited += processExited;
            try
            {
                if (!process.Start())
                    throw new InvalidOperationException("The command could not be started.");

                using var cancellationRegistration = cancellationToken.Register(() =>
                {
                    try
                    {
                        if (!process.HasExited)
                            process.Kill(entireProcessTree: true);
                    }
                    catch (InvalidOperationException) { }
                    catch (NotSupportedException) { }

                    tcs.TrySetCanceled(cancellationToken);
                });

                return await tcs.Task;
            }
            finally
            {
                process.Exited -= processExited;
            }
        }

        private static string SafeMainWindowTitle(Process process) { try { return process.MainWindowTitle ?? string.Empty; } catch { return string.Empty; } }
    }

    public sealed record ProcessInfo(int Id, string Name, string WindowTitle);
}
