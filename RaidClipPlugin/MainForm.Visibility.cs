namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private static void ApplyVisibilitySafeguards(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is FlowLayoutPanel flow && ContainsInteractiveSettings(flow))
                flow.AutoScroll = true;

            if (child is TabPage page)
                page.AutoScroll = true;

            ApplyVisibilitySafeguards(child);
        }
    }

    private static bool ContainsInteractiveSettings(Control root)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Button or CheckBox or TextBox or ComboBox or
                NumericUpDown or ListBox or ListView or DataGridView)
                return true;
            if (child.HasChildren && ContainsInteractiveSettings(child))
                return true;
        }
        return false;
    }
}
