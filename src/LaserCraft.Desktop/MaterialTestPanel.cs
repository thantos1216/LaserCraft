using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using LaserCraft.Domain;
namespace LaserCraft.Desktop;
public sealed partial class MainWindow
{
    private async Task CreateMaterialTest()
    {
        var bed=Current.Canvas.History.Document;var dialog=new Window {Title="Material test grid",Width=820,Height=760,MinWidth=660,MinHeight=600,WindowStartupLocation=WindowStartupLocation.CenterOwner,Background=Background,Foreground=Foreground};
        var rows=Number(1,256);rows.Value=5;var cols=Number(1,256);cols.Value=5;var cw=Number(.1m,1000);cw.Value=10;var ch=Number(.1m,1000);ch.Value=10;var gap=Number(0,1000);gap.Value=3;var px=Number(0,10000);px.Value=10;var py=Number(0,10000);py.Value=10;
        ComboBox Choice(Array items,int index=0)=>new(){ItemsSource=items,SelectedIndex=index};
        var ra=Choice(Enum.GetValues<MaterialTestAxis>(),1);var ca=Choice(Enum.GetValues<MaterialTestAxis>());var modeChoice=Choice(new[]{"Line (cut)","Fill (engrave)"});var shapeChoice=Choice(Enum.GetValues<MaterialTestShape>());
        var rmin=Number(0,100000);rmin.Value=10;var rmax=Number(0,100000);rmax.Value=50;var cmin=Number(0,100000);cmin.Value=300;var cmax=Number(0,100000);cmax.Value=1500;
        var fixedSpeed=Number(1,100000);fixedSpeed.Value=600;var fixedPower=Number(0,100);fixedPower.Value=20;var fixedPasses=Number(1,100);fixedPasses.Value=1;var scan=Number(.01m,100);scan.Value=.1m;var assist=new CheckBox {Content="Air assist"};var groupCells=new CheckBox {Content="Group cells for moving together",IsChecked=true};
        var info=Label("");var canvas=new MaterialTestPreview {Height=220};MaterialTestResult? ready=null;Button create=null!;
        int Integer(NumericUpDown n){var v=n.Value??throw new InvalidDataException("Fill in every value.");if(v!=decimal.Truncate(v))throw new InvalidDataException("Rows, columns and fixed passes must be integers.");return (int)v;}
        double Val(NumericUpDown n)=>(double)(n.Value??throw new InvalidDataException("Fill in every value."));
        void Update()
        {
            ready=null;create.IsEnabled=false;
            try{ready=MaterialTests.Create(bed.Width,bed.Height,new(Integer(rows),Integer(cols),Val(cw),Val(ch),Val(gap),Val(px),Val(py),(MaterialTestAxis)ra.SelectedItem!,(MaterialTestAxis)ca.SelectedItem!,Val(rmin),Val(rmax),Val(cmin),Val(cmax),Val(fixedSpeed),Val(fixedPower),Integer(fixedPasses),(CutMode)modeChoice.SelectedIndex,(MaterialTestShape)shapeChoice.SelectedItem!,assist.IsChecked==true,Val(scan),groupCells.IsChecked==true));canvas.Document=ready.Document;canvas.InvalidateVisual();info.Text=string.Join("\n",ready.Cells.Select(c=>$"R{c.Row} C{c.Column}: {c.Speed:0.###} mm/min · {c.Power:0.###}% · {c.Passes} passes"));create.IsEnabled=true;}
            catch(InvalidDataException ex){info.Text=ex.Message;canvas.Document=null;canvas.InvalidateVisual();}
        }
        create=Button("Create editable project",()=>{if(ready is null)return;AddDocument(ready.Document);Current.Saved="";Refresh();dialog.Close();status.Text="Material test created in a new unsaved project. Review Preview, placement and settings before Run; nothing was sent.";});
        var panel=new StackPanel {Spacing=5,Margin=new Avalonia.Thickness(16)};panel.Children.Add(Label("Editable cut / engraving test grid",18));panel.Children.Add(Label("Creates a new project; does not fire or move the laser. Settings are user-entered experiments, not calibrated recipes. Pass values interpolate then round to nearest integer. Each cell has an independent layer; output follows row-major order. Dimensions explicitly use mm."));
        void Row(params (string Name,Control Control)[] fields){var row=new WrapPanel();foreach(var f in fields)Inline(row,f.Name,f.Control,125);panel.Children.Add(row);}
        Row(("Rows",rows),("Columns",cols),("Cell width mm",cw),("Cell height mm",ch),("Gap mm",gap));Row(("X mm",px),("Y mm",py),("Mode",modeChoice),("Shape",shapeChoice));Row(("Rows vary",ra),("Row min",rmin),("Row max",rmax));Row(("Columns vary",ca),("Column min",cmin),("Column max",cmax));Row(("Fixed mm/min",fixedSpeed),("Fixed power %",fixedPower),("Fixed passes",fixedPasses),("Fill interval mm",scan));panel.Children.Add(assist);panel.Children.Add(groupCells);panel.Children.Add(canvas);panel.Children.Add(info);var actions=new WrapPanel();actions.Children.Add(create);actions.Children.Add(Button("Cancel",dialog.Close));var root=new Grid {RowDefinitions=new("*,Auto")};root.Children.Add(new ScrollViewer {Content=panel});Grid.SetRow(actions,1);root.Children.Add(actions);dialog.Content=root;
        foreach(var n in new[]{rows,cols,cw,ch,gap,px,py,rmin,rmax,cmin,cmax,fixedSpeed,fixedPower,fixedPasses,scan})n.ValueChanged+=(_,_)=>Update();foreach(var combo in new[]{ra,ca,modeChoice,shapeChoice})combo.SelectionChanged+=(_,_)=>Update();assist.IsCheckedChanged+=(_,_)=>Update();groupCells.IsCheckedChanged+=(_,_)=>Update();Update();await dialog.ShowDialog(this);
    }
}

internal sealed class MaterialTestPreview : Control
{
    public DesignDocument? Document {get;set;}
    public override void Render(DrawingContext context)
    {
        context.FillRectangle(new SolidColorBrush(Color.Parse("#0A111B")),new Rect(Bounds.Size));if(Document is not {} d||d.Shapes.Count==0)return;
        var b=ShapeEditing.Bounds(d.Shapes);var scale=Math.Max(.001,Math.Min(Math.Max(1,Bounds.Width-30)/b.Width,Math.Max(1,Bounds.Height-30)/b.Height));
        foreach(var s in d.Shapes){var rect=new Rect((Bounds.Width-b.Width*scale)/2+(s.X-b.X)*scale,(Bounds.Height-b.Height*scale)/2+(s.Y-b.Y)*scale,s.Width*scale,s.Height*scale);var pen=new Pen(new SolidColorBrush(Color.Parse(d.Layers[s.Layer].Color)),1.5);if(s.Kind==ShapeKind.Ellipse)context.DrawEllipse(null,pen,rect.Center,rect.Width/2,rect.Height/2);else context.DrawRectangle(null,pen,rect);}
    }
}
