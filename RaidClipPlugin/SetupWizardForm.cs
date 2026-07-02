using System.Diagnostics;
using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed class SetupWizardForm : Form
{
    private const string DeveloperConsoleUrl =
        "https://dev.twitch.tv/console/apps";

    private readonly TwitchCredentialStore _credentialStore;

    private readonly TextBox _clientIdBox = new()
    {
        Dock = DockStyle.Top,
        PlaceholderText = "Twitch Client ID"
    };

    private readonly TextBox _clientSecretBox = new()
    {
        Dock = DockStyle.Top,
        PlaceholderText = "Twitch Client Secret",
        UseSystemPasswordChar = true
    };

    private readonly CheckBox _showSecretCheck = new()
    {
        Text = "Client Secret anzeigen",
        AutoSize = true
    };

    private readonly Label _errorLabel = new()
    {
        AutoSize = true,
        ForeColor = Color.Firebrick,
        MaximumSize = new Size(540, 0)
    };

    public SetupWizardForm(TwitchCredentialStore credentialStore)
    {
        _credentialStore = credentialStore;

        Text = "Raid Clip Plugin – Einrichtung";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(640, 500);
        BackColor = Color.FromArgb(8, 8, 9);
        ForeColor = Color.Gainsboro;
        Icon = System.Drawing.Icon.ExtractAssociatedIcon(
                   Application.ExecutablePath) ??
               SystemIcons.Application;
        Font = new Font("Segoe UI", 10F);
        AutoScaleMode = AutoScaleMode.Dpi;

        BuildLayout();
        ApplyDarkTheme(this);
    }

    private void BuildLayout()
    {
        var title = new Label
        {
            Text = "Willkommen!",
            AutoSize = true,
            Font = new Font("Segoe UI", 22F, FontStyle.Bold),
            ForeColor = Color.White
        };

        var introduction = new Label
        {
            Text =
                "Um RaidClip zu benutzen, benötigst du eine eigene " +
                "Twitch Application. Öffne die Twitch Developer Console, " +
                "erstelle dort eine Anwendung und trage die beiden Werte ein.",
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            ForeColor = Color.FromArgb(174, 174, 180)
        };

        var privacy = new Label
        {
            Text =
                "🔒 Client ID, Client Secret und spätere Twitch-Tokens werden " +
                "verschlüsselt an dein Windows-Benutzerkonto gebunden. " +
                "Sie werden weder in config.json noch in der EXE gespeichert.",
            AutoSize = true,
            MaximumSize = new Size(560, 0),
            ForeColor = Color.Gainsboro,
            BackColor = Color.FromArgb(32, 18, 20),
            Padding = new Padding(12)
        };

        var openConsoleButton = new Button
        {
            Text = "Twitch-Anwendung öffnen",
            AutoSize = true,
            Padding = new Padding(12, 6, 12, 6)
        };
        openConsoleButton.Click += (_, _) => OpenDeveloperConsole();

        var saveButton = new Button
        {
            Name = "PrimaryButton",
            Text = "Speichern und fortfahren",
            AutoSize = true,
            Padding = new Padding(14, 7, 14, 7),
            BackColor = Color.FromArgb(92, 12, 15),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        saveButton.FlatAppearance.BorderSize = 0;
        saveButton.Click += (_, _) => SaveCredentials();
        AcceptButton = saveButton;

        _showSecretCheck.CheckedChanged += (_, _) =>
            _clientSecretBox.UseSystemPasswordChar =
                !_showSecretCheck.Checked;

        var content = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true,
            Padding = new Padding(34, 26, 34, 22)
        };

        content.Controls.Add(title);
        content.Controls.Add(CreateSpacer(4));
        content.Controls.Add(introduction);
        content.Controls.Add(CreateSpacer(8));
        content.Controls.Add(openConsoleButton);
        content.Controls.Add(CreateSpacer(10));
        content.Controls.Add(CreateField("Twitch Client ID", _clientIdBox));
        content.Controls.Add(CreateField(
            "Twitch Client Secret",
            _clientSecretBox));
        content.Controls.Add(_showSecretCheck);
        content.Controls.Add(CreateSpacer(8));
        content.Controls.Add(privacy);
        content.Controls.Add(_errorLabel);
        content.Controls.Add(CreateSpacer(4));
        content.Controls.Add(saveButton);

        Controls.Add(content);
    }

    private static void ApplyDarkTheme(Control root)
    {
        if (root is Form or Panel)
        {
            root.BackColor = Color.FromArgb(8, 8, 9);
        }

        foreach (Control control in root.Controls)
        {
            switch (control)
            {
                case TextBox textBox:
                    textBox.BackColor = Color.FromArgb(13, 13, 14);
                    textBox.ForeColor = Color.Gainsboro;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    break;
                case Button button:
                    button.BackColor = button.Name == "PrimaryButton"
                        ? Color.FromArgb(92, 12, 15)
                        : Color.FromArgb(22, 22, 24);
                    button.ForeColor = Color.White;
                    button.FlatStyle = FlatStyle.Flat;
                    button.FlatAppearance.BorderColor =
                        Color.FromArgb(222, 24, 30);
                    button.FlatAppearance.BorderSize = 1;
                    break;
                case CheckBox checkBox:
                    checkBox.ForeColor = Color.Gainsboro;
                    checkBox.BackColor = Color.Transparent;
                    checkBox.FlatStyle = FlatStyle.Flat;
                    break;
                case Label label when label.ForeColor == Color.Empty ||
                                      label.ForeColor == SystemColors.ControlText:
                    label.ForeColor = Color.Gainsboro;
                    label.BackColor = Color.Transparent;
                    break;
            }

            ApplyDarkTheme(control);
        }
    }

    private static Control CreateField(string labelText, Control editor)
    {
        var panel = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoSize = true,
            Width = 560,
            Margin = new Padding(0, 4, 0, 4)
        };

        panel.Controls.Add(new Label
        {
            Text = labelText,
            AutoSize = true,
            Font = new Font("Segoe UI", 10F, FontStyle.Bold)
        });
        editor.Width = 540;
        panel.Controls.Add(editor);
        return panel;
    }

    private static Control CreateSpacer(int height) => new Panel
    {
        Width = 1,
        Height = height,
        Margin = Padding.Empty
    };

    private void SaveCredentials()
    {
        try
        {
            _credentialStore.Save(
                _clientIdBox.Text,
                _clientSecretBox.Text);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception exception)
        {
            _errorLabel.Text = exception.Message;
        }
    }

    private static void OpenDeveloperConsole()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DeveloperConsoleUrl,
                UseShellExecute = true
            });
        }
        catch (Exception exception)
        {
            MessageBox.Show(
                "Die Twitch Developer Console konnte nicht geöffnet werden: " +
                exception.Message,
                "Browser konnte nicht geöffnet werden",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }
    }
}
