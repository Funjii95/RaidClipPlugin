using Xunit;

namespace RaidClipPlugin.Tests;

// MainForm changes process-wide Console writers and initializes WinForms state.
// These tests must not construct multiple windows on competing STA threads.
[CollectionDefinition("WinForms", DisableParallelization = true)]
public sealed class WinFormsCollection { }
