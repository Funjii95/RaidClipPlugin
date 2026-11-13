namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private Control BuildTidalTestPanel() => new TidalTestPanel(BackgroundColor, InputColor, TextColor);
}
