using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Styling;
using LaserCraft.Domain;
namespace LaserCraft.Desktop;

public sealed record DeviceControlDefaults(decimal Distance=1,decimal Feed=600,decimal Power=1,decimal Duration=100,decimal X=0,decimal Y=0,int Start=1,int Anchor=6,string? Port=null);
public sealed record WorkspacePreferences(bool Light=false,Guid? Device=null,bool Inches=false,Dictionary<Guid,DeviceControlDefaults>? Controls=null,bool FrameBeforeRun=false);
public sealed partial class MainWindow
{
    private WorkspacePreferences preferences=new();
    private bool preferencesLoaded;
    private Guid activeDefaultsId;
    private void SavePreferences()
    {
        if(!preferencesLoaded)return;
        try
        {
            var controls=preferences.Controls??new();
            controls[activeDefaultsId]=new((decimal)Millimetres(jogDistance),(decimal)Millimetres(jogFeed),pulsePower.Value??1,pulseDuration.Value??100,(decimal)Millimetres(positionX),(decimal)Millimetres(positionY),startFrom.SelectedIndex,jobAnchor.SelectedIndex,connectionPort.SelectedItem as string);
            preferences=new(Avalonia.Application.Current?.RequestedThemeVariant==ThemeVariant.Light,profiles.ElementAtOrDefault(deviceChoice.SelectedIndex)?.Id,inches,controls,frameBeforeRun.IsChecked==true);
            Directory.CreateDirectory(StateFolder);var temp=settingsPath+".tmp";File.WriteAllText(temp,JsonSerializer.Serialize(preferences));File.Move(temp,settingsPath,true);
        }
        catch(Exception ex)when(ex is IOException or UnauthorizedAccessException){status.Text="Settings could not be saved: "+ex.Message;}
    }
    private void RestorePreferences()
    {
        try{if(File.Exists(settingsPath))preferences=JsonSerializer.Deserialize<WorkspacePreferences>(File.ReadAllText(settingsPath))??new();}
        catch(Exception ex)when(ex is IOException or JsonException){status.Text="Saved settings could not be read; defaults used.";}
        if(preferences.Light)Avalonia.Application.Current!.RequestedThemeVariant=ThemeVariant.Light;
        var index=profiles.FindIndex(p=>p.Id==preferences.Device);if(index<0)index=profiles.FindIndex(p=>p.Connection!=ConnectionKind.Simulator);deviceChoice.SelectedIndex=index<0?0:index;
        frameBeforeRun.IsChecked=preferences.FrameBeforeRun;SetUnits(preferences.Inches);RestoreDeviceDefaults();RefreshConnectionPorts();preferencesLoaded=true;ApplyProfileBed();
        foreach(var n in new[]{jogDistance,jogFeed,pulsePower,pulseDuration,positionX,positionY})n.ValueChanged+=(_,_)=>{if(preferencesLoaded&&!changingUnits)SavePreferences();};
        frameBeforeRun.IsCheckedChanged+=(_,_)=>SavePreferences();
        connectionPort.SelectionChanged+=(_,_)=>{if(profiles.ElementAtOrDefault(deviceChoice.SelectedIndex)?.Id==activeDefaultsId)SavePreferences();};
        startFrom.SelectionChanged+=(_,_)=>SavePreferences();jobAnchor.SelectionChanged+=(_,_)=>SavePreferences();
    }
    private void RestoreDeviceDefaults()
    {
        var loaded=preferencesLoaded;preferencesLoaded=false;
        activeDefaultsId=profiles.ElementAtOrDefault(deviceChoice.SelectedIndex)?.Id??Guid.Empty;
        var d=preferences.Controls?.GetValueOrDefault(activeDefaultsId)??new();
        SetMillimetres(jogDistance,Math.Clamp((double)d.Distance,.001,1000));SetMillimetres(jogFeed,Math.Clamp((double)d.Feed,60,3000));pulsePower.Value=Math.Clamp(d.Power,.001m,5);pulseDuration.Value=Math.Clamp(d.Duration,1,500);SetMillimetres(positionX,Math.Clamp((double)d.X,0,10000));SetMillimetres(positionY,Math.Clamp((double)d.Y,0,10000));startFrom.SelectedIndex=Math.Clamp(d.Start,0,2);jobAnchor.SelectedIndex=Math.Clamp(d.Anchor,0,8);preferencesLoaded=loaded;
    }
    private void ApplyProfileBed()
    {
        var p=profiles.ElementAtOrDefault(deviceChoice.SelectedIndex);if(p is null||p.Connection==ConnectionKind.Simulator||sessions.Count==0)return;
        var c=Current.Canvas;var d=c.History.Document;if(d.Width!=p.BedWidth||d.Height!=p.BedHeight)c.Apply(d with {Width=p.BedWidth,Height=p.BedHeight});c.Fit();
    }
}
