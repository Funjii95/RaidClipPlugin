namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private const string DisableAutoScrollTag = "DisableAutoScroll";

    private static void ApplyVisibilitySafeguards(Control root)
    {
        foreach (Control child in root.Controls)
        {
            var autoScrollDisabled = HasDisableAutoScrollTag(child);

            if (!autoScrollDisabled && child is FlowLayoutPanel flow && ContainsInteractiveSettings(flow))
                flow.AutoScroll = true;

            if (!autoScrollDisabled && child is TabPage page)
                page.AutoScroll = true;

            if (autoScrollDisabled && child is ScrollableControl scrollable)
                scrollable.AutoScroll = false;

            ApplyVisibilitySafeguards(child);
        }
    }

    private static bool HasDisableAutoScrollTag(Control control)
    {
        for (var current = control; current is not null; current = current.Parent)
        {
            if (current.Tag is string tag && string.Equals(tag, DisableAutoScrollTag, StringComparison.Ordinal))
                return true;
        }

        return false;
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
