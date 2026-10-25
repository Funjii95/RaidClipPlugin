namespace RaidClipPlugin;
public partial class MainForm
{
 private readonly TextBox _obsBrowserUrl=new(){Name="ObsBrowserSourceUrl",ReadOnly=true,Dock=DockStyle.Fill,AccessibleName="OBS-Browserquellen-URL"};
 private string ObsBrowserUrl()=>_player?.IdleUrl??$"http://127.0.0.1:{ReadSettingsFromControls().Player.Port}/";
 private Control CreateObsBrowserUrlControl()
 {
  var panel=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=2,Padding=new Padding(0,2,0,2),Margin=Padding.Empty};
  panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent,100));panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute,106));
  panel.RowStyles.Add(new RowStyle(SizeType.Absolute,22));panel.RowStyles.Add(new RowStyle(SizeType.Absolute,32));
  var label=new Label{Text="URL für die OBS-Browserquelle",AutoSize=true,Dock=DockStyle.Fill,ForeColor=TextColor};panel.Controls.Add(label,0,0);panel.SetColumnSpan(label,2);
  panel.Controls.Add(_obsBrowserUrl,0,1);var copy=NewActionButton("URL kopieren");copy.Dock=DockStyle.Fill;panel.Controls.Add(copy,1,1);
  void RefreshUrl(){try{_obsBrowserUrl.Text=ObsBrowserUrl();}catch{_obsBrowserUrl.Text="Port bitte in den Einstellungen prüfen.";}}
  Shown+=(_,_)=>RefreshUrl();_obsBrowserUrl.Enter+=(_,_)=>RefreshUrl();
  copy.Click+=(_,_)=>{RefreshUrl();if(Uri.TryCreate(_obsBrowserUrl.Text,UriKind.Absolute,out var uri)&&uri.IsLoopback){try{Clipboard.SetText(_obsBrowserUrl.Text);copy.Text="Kopiert ✓";}catch(System.Runtime.InteropServices.ExternalException){AppendLog("Zwischenablage ist belegt. Die URL kann im Textfeld markiert werden.");}}};
  return panel;
 }
}
