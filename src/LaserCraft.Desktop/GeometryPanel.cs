using Avalonia.Controls;
using Avalonia.Threading;
using LaserCraft.Domain;
namespace LaserCraft.Desktop;
public sealed partial class MainWindow
{
    private readonly CheckBox aspectLock=new() {Content="Lock",VerticalAlignment=Avalonia.Layout.VerticalAlignment.Bottom,Margin=new Avalonia.Thickness(4)};
    private DispatcherTimer? geometryTimer;private bool syncingGeometry,geometryPending;private Session? geometryOwner;private string geometrySnapshot="",geometryIds="";
    private bool KeepPendingGeometry()
    {
        if(!geometryPending)return false;
        if(Current==geometryOwner&&Snapshot(Current)==geometrySnapshot&&string.Join(',',Current.Canvas.Selection.Order())==geometryIds)return true;
        geometryTimer?.Stop();geometryPending=false;return false;
    }
    private void InstallGeometryBindings()
    {
        ToolTip.SetTip(aspectLock,"Lock aspect ratio: changing Width scales Height by the same percentage, and vice versa.");
        geometryTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(350)};geometryTimer.Tick+=(_,_)=>{geometryTimer.Stop();if(KeepPendingGeometry())ApplyGeometry();};
        void Changed(bool horizontal)
        {
            if(syncingGeometry||changingUnits||sessions.Count==0||Current.Canvas.Selection.Count==0)return;
            var c=Current.Canvas;var b=ShapeEditing.Bounds(c.History.Document.Shapes.Where(s=>c.Selection.Contains(s.Id)));
            try
            {
                void Couple(NumericUpDown input,double value){var display=DisplayValue(value);if(display<input.Minimum||display>input.Maximum)throw new InvalidDataException("Locked dimensions exceed the input range.");SetMillimetres(input,value);}
                if(aspectLock.IsChecked==true){syncingGeometry=true;if(horizontal)Couple(height,GeometrySizing.MatchAspect(Millimetres(width),b.Width,b.Height));else Couple(width,GeometrySizing.MatchAspect(Millimetres(height),b.Height,b.Width));}
                geometryOwner=Current;geometrySnapshot=Snapshot(Current);geometryIds=string.Join(',',c.Selection.Order());geometryPending=true;geometryTimer.Stop();geometryTimer.Start();
            }
            catch(InvalidDataException ex){geometryTimer.Stop();geometryPending=false;syncingGeometry=true;SetMillimetres(width,b.Width);SetMillimetres(height,b.Height);status.Text=ex.Message;}
            finally{syncingGeometry=false;}
        }
        width.ValueChanged+=(_,_)=>Changed(true);height.ValueChanged+=(_,_)=>Changed(false);Closed+=(_,_)=>geometryTimer.Stop();
    }
}
