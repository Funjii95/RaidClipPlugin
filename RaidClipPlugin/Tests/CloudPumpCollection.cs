using Xunit;
namespace RaidClipPlugin.Tests;

// These tests exercise real heartbeat/lease deadlines in in-process HTTP servers.
// Running alongside the WinForms and persistence suites starves the CI thread pool.
[CollectionDefinition("Cloud pump integration",DisableParallelization=true)]
public sealed class CloudPumpCollection { }
