using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Aurora.App.Services
{
    /// <summary>
    /// Executes commands through the Windows command shells that users already have installed.
    /// Terminal access is the master permission for this service.
    /// </summary>
    public static class TerminalTools
    {
        public static async Task<string> RunPowerShellAsync(string script)
        {
            if (!App.Settings.WindowsTerminalEnabled)
                throw new UnauthorizedAccessException("Terminal access is disabled.");

            if (string.IsNullOrWhiteSpace(script))
                throw new ArgumentException("A PowerShell command is required.", nameof(script));

            var shell = FindPowerShell();
            return await RunProcessAsync(shell, "-NoLogo -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command " + Quote(script));
        }

        public static async Task<string> RunCmdAsync(string command)
        {
            if (!App.Settings.WindowsTerminalEnabled)
                throw new UnauthorizedAccessException("Terminal access is disabled.");

            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("A Command Prompt command is required.", nameof(command));

            return await RunProcessAsync(Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe", "/d /c " + Quote(command));
        }

        public static Task<string> PowerAsync(string action)
        {
            return action.Trim().ToLowerInvariant() switch
            {
                "lock" => RunCmdAsync("rundll32.exe user32.dll,LockWorkStation"),
                "sleep" => RunPowerShellAsync("Start-Process -FilePath 'rundll32.exe' -ArgumentList 'powrprof.dll,SetSuspendState 0,1,0'"),
                "hibernate" => RunCmdAsync("shutdown.exe /h"),
                "restart" => RunCmdAsync("shutdown.exe /r /t 0"),
                "shutdown" => RunCmdAsync("shutdown.exe /s /t 0"),
                "logoff" => RunCmdAsync("shutdown.exe /l"),
                _ => throw new ArgumentException("Power action must be lock, sleep, hibernate, restart, shutdown, or logoff.")
            };
        }

        public static Task<string> NetworkStatusAsync()
            => RunPowerShellAsync("Get-NetAdapter | Select-Object Name,Status,LinkSpeed,MacAddress | Format-Table -AutoSize | Out-String");

        public static Task<string> WifiStatusAsync()
            => RunCmdAsync("netsh wlan show interfaces");

        public static Task<string> WifiToggleAsync(string name, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "Wi-Fi";
            var state = enabled ? "enabled" : "disabled";
            return RunCmdAsync($"netsh interface set interface name=\"{EscapeCmd(name)}\" admin={state}");
        }

        public static Task<string> BluetoothStatusAsync()
            => RunPowerShellAsync("Get-PnpDevice -Class Bluetooth | Select-Object Status,FriendlyName,InstanceId | Format-Table -AutoSize | Out-String");

        private static string FindPowerShell()
        {
            var pwsh = Environment.GetEnvironmentVariable("ProgramFiles") is { Length: > 0 } pf
                ? System.IO.Path.Combine(pf, "PowerShell", "7", "pwsh.exe")
                : "";
            return System.IO.File.Exists(pwsh) ? pwsh : "powershell.exe";
        }

        private static async Task<string> RunProcessAsync(string fileName, string arguments)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };

            using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            var output = string.IsNullOrWhiteSpace(stderr) ? stdout : stdout + Environment.NewLine + stderr;
            return $"Exit code: {process.ExitCode}{Environment.NewLine}{output.Trim()}".Trim();
        }

        private static string Quote(string value) => "\"" + value.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        private static string EscapeCmd(string value) => value.Replace("\"", "\\\"");
    }
}
