using Avalonia.Controls;
using LaserCraft.Domain;
namespace LaserCraft.Desktop;
public sealed partial class MainWindow
{
    private readonly ComboBox passOrder=new() {ItemsSource=new[]{"Whole layer each pass","All passes per object"},SelectedIndex=0};
    private bool applyingLayerEdits,refreshingLayerEditor;
    private bool CommitLayerEdits()
    {
        if(applyingLayerEdits||refreshingLayerEditor||changingUnits||sessions.Count==0)return true;
        if(speed.Value is null||power.Value is null||passes.Value is null||interval.Value is null){status.Text="Complete the layer values before running.";return false;}
        if(passes.Value!=decimal.Truncate(passes.Value.Value)){status.Text="Passes must be a whole number.";return false;}
        try
        {
            var c=Current.Canvas;var d=c.History.Document;var i=Math.Max(0,layer.SelectedIndex);var source=d.Layers[i];
            var edited=source with {Speed=Millimetres(speed),Power=(double)power.Value.Value,Passes=(int)passes.Value.Value,Interval=Millimetres(interval),Name=layerName.Text??"",Color=layerColorValue,Mode=source.Kind==LayerKind.Image?CutMode.Image:(CutMode)Math.Max(0,mode.SelectedIndex),AirAssist=air.IsChecked==true,Output=output.IsChecked==true,Visible=visible.IsChecked==true,PassOrder=(PassOrderMode)Math.Max(0,passOrder.SelectedIndex)};
            if(edited==source)return true;
            var layers=d.Layers.ToList();layers[i]=edited;var next=d with {Layers=layers};next.Validate();
            applyingLayerEdits=true;c.Apply(next);return true;
        }
        catch(InvalidDataException ex){status.Text="Layer settings: "+ex.Message;return false;}
        finally{applyingLayerEdits=false;}
    }
    private void InstallLayerBindings()
    {
        void Changed(){if(!updatingInspector&&!refreshingLayerEditor&&!changingUnits)CommitLayerEdits();}
        foreach(var n in new[]{speed,power,passes,interval})n.ValueChanged+=(_,_)=>Changed();
        foreach(var c in new[]{air,output,visible})c.IsCheckedChanged+=(_,_)=>Changed();
        mode.SelectionChanged+=(_,_)=>Changed();passOrder.SelectionChanged+=(_,_)=>Changed();layerName.TextChanged+=(_,_)=>Changed();
    }
}
