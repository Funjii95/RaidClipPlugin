using System.Diagnostics;
namespace RaidClipPlugin.Services;
internal static class PluginInstanceGuard
{
    internal static bool IncludesAnotherInstance(IEnumerable<int> processIds, int currentProcessId) =>
        processIds.Any(id => id != currentProcessId);

    internal static bool AnotherInstanceIsRunning()
    {
        var processes = Process.GetProcessesByName("RaidClipPlugin");
        try { return IncludesAnotherInstance(processes.Select(p => p.Id), Environment.ProcessId); }
        finally { foreach (var process in processes) process.Dispose(); }
    }
}
