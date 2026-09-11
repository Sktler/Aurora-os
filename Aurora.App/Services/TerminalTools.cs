using System;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;

namespace Aurora.App.Services
{
    /// <summary>
    /// Executes commands through Windows PowerShell and Command Prompt.
    /// Terminal access is the master permission for this service.
    /// </summary>
    public static class TerminalTools
    {
        public static async Task<string> RunPowerShellAsync(string script)
        {
            EnsureTerminalAccess();
            if (string.IsNullOrWhiteSpace(script))
                throw new ArgumentException("A PowerShell command is required.", nameof(script));

            var psi = new ProcessStartInfo
            {
                FileName = FindPowerShell(),
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.ArgumentList.Add("-NoLogo");
            psi.ArgumentList.Add("-NoProfile");
            psi.ArgumentList.Add("-NonInteractive");
            psi.ArgumentList.Add("-ExecutionPolicy");
            psi.ArgumentList.Add("Bypass");
            psi.ArgumentList.Add("-Command");
            psi.ArgumentList.Add(script);
            return await RunProcessAsync(psi);
        }

        public static async Task<string> RunCmdAsync(string command)
        {
            EnsureTerminalAccess();
            if (string.IsNullOrWhiteSpace(command))
                throw new ArgumentException("A Command Prompt command is required.", nameof(command));

            var psi = new ProcessStartInfo
            {
                FileName = Environment.GetEnvironmentVariable("ComSpec") ?? "cmd.exe",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8
            };
            psi.ArgumentList.Add("/d");
            psi.ArgumentList.Add("/c");
            psi.ArgumentList.Add(command);
            return await RunProcessAsync(psi);
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

        public static Task<string> NetworkToggleAsync(string name, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(name)) name = "Wi-Fi";
            var cmd = enabled ? "Enable-NetAdapter" : "Disable-NetAdapter";
            return RunPowerShellAsync($"{cmd} -Name '{EscapePowerShell(name)}' -Confirm:$false");
        }

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

        public static Task<string> BluetoothToggleAsync(string instanceId, bool enabled)
        {
            if (string.IsNullOrWhiteSpace(instanceId))
                throw new ArgumentException("A Bluetooth device InstanceId is required.", nameof(instanceId));
            var cmd = enabled ? "Enable-PnpDevice" : "Disable-PnpDevice";
            return RunPowerShellAsync($"{cmd} -InstanceId '{EscapePowerShell(instanceId)}' -Confirm:$false");
        }

        private static void EnsureTerminalAccess()
        {
            if (!App.Settings.WindowsTerminalEnabled)
                throw new UnauthorizedAccessException("Terminal access is disabled.");
        }

        private static string FindPowerShell()
        {
            var pf = Environment.GetEnvironmentVariable("ProgramFiles");
            var pwsh = string.IsNullOrWhiteSpace(pf) ? "" : System.IO.Path.Combine(pf, "PowerShell", "7", "pwsh.exe");
            return System.IO.File.Exists(pwsh) ? pwsh : "powershell.exe";
        }

        private static async Task<string> RunProcessAsync(ProcessStartInfo psi)
        {
            using var process = new Process { StartInfo = psi };
            process.Start();
            var stdoutTask = process.StandardOutput.ReadToEndAsync();
            var stderrTask = process.StandardError.ReadToEndAsync();
            await process.WaitForExitAsync();
            var stdout = await stdoutTask;
            var stderr = await stderrTask;
            var output = string.IsNullOrWhiteSpace(stderr) ? stdout : stdout + Environment.NewLine + stderr;
            return $"Exit code: {process.ExitCode}{Environment.NewLine}{output.Trim()}".Trim();
        }

        private static string EscapePowerShell(string value) => value.Replace("'", "''");
        private static string EscapeCmd(string value) => value.Replace("\"", "\\\"");
    }
}
