using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using System.Drawing;
using System.Drawing.Imaging;

namespace ZoeyOS.App.Services
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
            Process.Start(new ProcessStartInfo { FileName = executableOrPath, UseShellExecute = true });
        }

        public void OpenPath(string path)
        {
            if (!FilesEnabled) throw new UnauthorizedAccessException("File access is disabled.");
            if (string.IsNullOrWhiteSpace(path)) throw new ArgumentException("A path is required.");
            Process.Start(new ProcessStartInfo { FileName = path, UseShellExecute = true });
        }

        public string ReadText(string path)
        {
            if (!FilesEnabled) throw new UnauthorizedAccessException("File access is disabled.");
            return File.ReadAllText(path);
        }

        public void WriteText(string path, string content)
        {
            if (!FilesEnabled) throw new UnauthorizedAccessException("File access is disabled.");
            File.WriteAllText(path, content ?? string.Empty);
        }

        public string GetClipboardText()
        {
            if (!ClipboardEnabled) throw new UnauthorizedAccessException("Clipboard access is disabled.");
            return Application.Current.Dispatcher.Invoke(() => Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty);
        }

        public void SetClipboardText(string text)
        {
            if (!ClipboardEnabled) throw new UnauthorizedAccessException("Clipboard access is disabled.");
            Application.Current.Dispatcher.Invoke(() => Clipboard.SetText(text ?? string.Empty));
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

        public Task<int> RunApprovedCommandAsync(string fileName, string arguments)
        {
            if (!TerminalEnabled) throw new UnauthorizedAccessException("Terminal access is disabled.");
            if (string.IsNullOrWhiteSpace(fileName)) throw new ArgumentException("A command is required.");
            var tcs = new TaskCompletionSource<int>();
            var process = new Process { StartInfo = new ProcessStartInfo { FileName = fileName, Arguments = arguments ?? string.Empty, UseShellExecute = false, CreateNoWindow = true } };
            process.EnableRaisingEvents = true;
            process.Exited += (_, _) => tcs.TrySetResult(process.ExitCode);
            process.Start();
            return tcs.Task;
        }

        private static string SafeMainWindowTitle(Process process) { try { return process.MainWindowTitle ?? string.Empty; } catch { return string.Empty; } }
    }

    public sealed record ProcessInfo(int Id, string Name, string WindowTitle);
}
