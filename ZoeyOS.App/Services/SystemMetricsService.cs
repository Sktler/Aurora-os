using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ZoeyOS.App.Services
{
    /// <summary>Live Windows performance metrics for the dashboard. Sampling is best-effort so Aurora remains usable on systems where a counter is unavailable.</summary>
    public sealed class SystemMetricsService : IDisposable
    {
        private readonly DispatcherTimer _timer;
        private readonly PerformanceCounter? _cpu;
        private readonly PerformanceCounter? _ram;
        private readonly PerformanceCounter? _disk;
        private readonly PerformanceCounter[] _gpu;

        public event Action<SystemMetrics>? Updated;

        public SystemMetricsService()
        {
            _cpu = TryCounter("Processor", "% Processor Time", "_Total");
            _ram = TryCounter("Memory", "% Committed Bytes In Use");
            _disk = TryCounter("PhysicalDisk", "% Disk Time", "_Total");
            _gpu = TryGpuCounters();

            _timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _timer.Tick += (_, _) => Sample();
            _timer.Start();
            Sample();
        }

        private static PerformanceCounter? TryCounter(string category, string counter, string? instance = null)
        {
            try
            {
                var c = instance == null ? new PerformanceCounter(category, counter, true) : new PerformanceCounter(category, counter, instance, true);
                c.NextValue();
                return c;
            }
            catch { return null; }
        }

        private static PerformanceCounter[] TryGpuCounters()
        {
            try
            {
                var category = new PerformanceCounterCategory("GPU Engine");
                return category.GetInstanceNames()
                    .Where(n => n.Contains("engtype_3D", StringComparison.OrdinalIgnoreCase))
                    .Select(n => TryCounter("GPU Engine", "Utilization Percentage", n))
                    .Where(c => c != null)
                    .Cast<PerformanceCounter>()
                    .ToArray();
            }
            catch { return Array.Empty<PerformanceCounter>(); }
        }

        private void Sample()
        {
            try
            {
                var cpu = Read(_cpu);
                var ram = Read(_ram);
                var disk = Math.Min(100, Read(_disk));
                var gpu = _gpu.Length == 0 ? -1 : Math.Min(100, _gpu.Max(Read));
                Updated?.Invoke(new SystemMetrics(cpu, ram, disk, gpu));
            }
            catch { }
        }

        private static double Read(PerformanceCounter? counter)
        {
            if (counter == null) return -1;
            try { return Math.Clamp(counter.NextValue(), 0, 100); } catch { return -1; }
        }

        public void Dispose()
        {
            _timer.Stop();
            _cpu?.Dispose(); _ram?.Dispose(); _disk?.Dispose();
            foreach (var c in _gpu) c.Dispose();
        }
    }

    public readonly record struct SystemMetrics(double CpuPercent, double RamPercent, double DiskPercent, double GpuPercent);
}
