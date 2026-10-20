using RaidClipPlugin.Services;

namespace RaidClipPlugin;

public sealed partial class MainForm
{
    private sealed record RpgChoice(string Id, string Name)
    { public override string ToString() => Name; }

    private Control BuildRpgAdministrationPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 7, Padding = new Padding(12) };
        foreach (var height in new[] { 52, 52, 52, 52, 52, 52 }) panel.RowStyles.Add(new RowStyle(SizeType.Absolute, height));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var players = new ComboBox { Width = 340, DropDownStyle = ComboBoxStyle.DropDownList };
        var fish = new ComboBox { Width = 340, DropDownStyle = ComboBoxStyle.DropDownList };
        var equipment = new ComboBox { Width = 540, DropDownStyle = ComboBoxStyle.DropDownList };
        var amount = new NumericUpDown { Minimum = 1, Maximum = int.MaxValue, Value = 1, Width = 110 };
        var load = NewActionButton("Spieler laden");
        var refresh = NewActionButton("Inventar aktualisieren");
        var deleteFish = NewActionButton("Fische löschen");
        var deleteEquipment = NewActionButton("Equipment löschen");
        var status = new Label { Dock = DockStyle.Fill, Text = "Nur lokale Verwaltung. Perlen, Perks und historische Fangstatistiken bleiben erhalten. Vor dem Löschen wird eine vollständige Kontosicherung im minigame-Datenordner angelegt.", AutoSize = false };
        FlowLayoutPanel Row(params Control[] controls)
        {
            var row = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoScroll = true };
            row.Controls.AddRange(controls); return row;
        }
        panel.Controls.Add(Row(players, load, refresh), 0, 0);
        panel.Controls.Add(new Label { Text = "Fischvorrat: Art wählen und gewünschte Anzahl entfernen", Dock = DockStyle.Fill }, 0, 1);
        panel.Controls.Add(Row(fish, amount, deleteFish), 0, 2);
        panel.Controls.Add(new Label { Text = "Equipment: einzelne Iteminstanz wählen (auch ausgerüstete Gegenstände)", Dock = DockStyle.Fill }, 0, 3);
        panel.Controls.Add(Row(equipment), 0, 4);
        panel.Controls.Add(Row(deleteEquipment), 0, 5);
        panel.Controls.Add(status, 0, 6);
        var busy = false;
        void SetBusy(bool value)
        { busy = value; foreach (var control in new Control[] { players, load, refresh, fish, amount, deleteFish, equipment, deleteEquipment }) control.Enabled = !value; }
        async Task RefreshInventory()
        {
            fish.Items.Clear(); equipment.Items.Clear();
            if (players.SelectedItem is not RpgChoice player) return;
            var profile = (await _viewerPoints.GetProfileAsync(player.Id, CancellationToken.None)).Entry;
            foreach (var entry in profile.Rpg.Fish.Where(p => p.Value.Quantity > 0))
                fish.Items.Add(new RpgChoice(entry.Key, $"{entry.Key}: {entry.Value.Quantity}x"));
            foreach (var item in profile.Rpg.Inventory.Where(i => i.Slot.HasValue))
                equipment.Items.Add(new RpgChoice(item.InstanceId, $"{item.DisplayName} [{item.Rarity}] · {item.InstanceId}" +
                    (profile.Rpg.Equipment.Values.Contains(item.InstanceId) ? " · ausgerüstet" : "")));
            if (fish.Items.Count > 0) fish.SelectedIndex = 0;
            if (equipment.Items.Count > 0) equipment.SelectedIndex = 0;
        }
        async Task Safe(Func<Task> action)
        {
            if (busy) return;
            SetBusy(true);
            try { await action(); }
            catch (Exception ex) { status.Text = "Fehler: " + ex.Message; AppendLog("RPG-Verwaltung: " + ex.Message); }
            finally { if (!panel.IsDisposed) SetBusy(false); }
        }
        load.Click += async (_, _) => await Safe(async () =>
        {
            players.Items.Clear();
            foreach (var player in await _viewerPoints.GetTopAsync(int.MaxValue, CancellationToken.None))
                players.Items.Add(new RpgChoice(player.UserId, $"{player.DisplayName} (Twitch-ID {player.UserId})"));
            if (players.Items.Count > 0) players.SelectedIndex = 0;
            await RefreshInventory();
        });
        players.SelectedIndexChanged += async (_, _) => { if (!busy) await Safe(RefreshInventory); };
        refresh.Click += async (_, _) => await Safe(RefreshInventory);
        async Task Delete(bool isFish)
        {
            if (players.SelectedItem is not RpgChoice player || (isFish ? fish : equipment).SelectedItem is not RpgChoice selected) return;
            var quantity = (int)amount.Value;
            var target = isFish ? $"{quantity}x {selected.Id}" : selected.Name;
            if (MessageBox.Show(this, $"Wirklich löschen?\nSpieler: {player.Name}\n{target}\n\nAusgerüstetes Equipment wird auch abgelegt. Perlen werden nicht erstattet. Eine Kontosicherung wird angelegt.",
                "RPG-Daten löschen", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            var name = (await _viewerPoints.GetProfileAsync(player.Id, CancellationToken.None)).Entry.DisplayName;
            var service = new RpgAdministrationService(_viewerPoints);
            var id = "admin-delete:" + Guid.NewGuid().ToString("N");
            var result = isFish ? await service.DeleteFishAsync(player.Id, name, id, selected.Id, quantity, CancellationToken.None) :
                await service.DeleteEquipmentAsync(player.Id, name, id, selected.Id, CancellationToken.None);
            status.Text = result.Success ? "Auswahl gelöscht. Sicherung: viewer-points.json.rpg-delete-*.bak im lokalen minigame-Datenordner. Andere Spielerdaten bleiben erhalten." : result.Error;
            if (result.Success) AppendLog($"RPG-Verwaltung: {target} für Twitch-ID {player.Id} gelöscht (mit Sicherung).");
            await RefreshInventory();
        }
        deleteFish.Click += async (_, _) => await Safe(() => Delete(true));
        deleteEquipment.Click += async (_, _) => await Safe(() => Delete(false));
        var tabs=new TabControl{Dock=DockStyle.Fill};
        var individual=new TabPage("Einzelne Items");individual.Controls.Add(panel);tabs.TabPages.Add(individual);
        var maintenance=new TabPage("Zurücksetzen und Datenschutz");maintenance.Controls.Add(BuildDataAdministrationPanel());tabs.TabPages.Add(maintenance);
        return tabs;
    }
}
