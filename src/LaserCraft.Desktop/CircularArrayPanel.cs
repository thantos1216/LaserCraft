using Avalonia;
using Avalonia.Controls;
using LaserCraft.Domain;
namespace LaserCraft.Desktop;
public sealed partial class MainWindow
{
    private async Task CircularArray()
    {
        var session=Current;var canvas=session.Canvas;var document=canvas.History.Document;var scope=canvas.Selection.ToHashSet();var fingerprint=Snapshot(session);
        if(scope.Count==0){status.Text="Select artwork to repeat first.";return;}
        var dialog=new Window {Title="Circular array",Width=670,Height=480,MinWidth=560,MinHeight=400,WindowStartupLocation=WindowStartupLocation.CenterOwner};
        var count=Number(2,100000);count.Value=6;var sweep=Number(-360,360);sweep.Value=360;sweep.Increment=1;
        var cx=Number(-10000,10000);cx.Value=(decimal)document.Width/2;var cy=Number(-10000,10000);cy.Value=(decimal)document.Height/2;
        var rotate=new CheckBox {Content="Rotate vector cells around center",IsChecked=false};var info=Label("");CircularArrayLayout? layout=null;Button apply=null!;
        int Count(){var v=count.Value??0;if(v!=decimal.Truncate(v))throw new InvalidDataException("Cell count must be a whole number.");return (int)v;}
        double Required(NumericUpDown input)=>input.Value is decimal value?(double)value:throw new InvalidDataException("Fill in every numeric value.");
        PointMm Center()=>new(Measurements.ToMillimetres(Required(cx),inches),Measurements.ToMillimetres(Required(cy),inches));
        void Update()
        {
            layout=null;apply.IsEnabled=false;
            try
            {
                layout=CircularArrays.Measure(document,scope,Count(),Center(),Required(sweep),rotate.IsChecked==true);
                var b=layout.Bounds;var unit=inches?"in":"mm";
                info.Text=$"{layout.Count} cells including originals · step {layout.StepDegrees:0.###}°\nAdds {layout.AddedObjects} objects / {layout.AddedLayers} image layers. Bounds {DisplayValue(b.Width):0.###} × {DisplayValue(b.Height):0.###} {unit}.";
                if(b.X<0||b.Y<0||b.X+b.Width>document.Width||b.Y+b.Height>document.Height)info.Text+="\nArtwork extends outside the bed. Reposition before output.";
                apply.IsEnabled=true;
            }
            catch(InvalidDataException ex){info.Text=ex.Message;}
        }
        cx.Value=(decimal)DisplayValue(document.Width/2);cy.Value=(decimal)DisplayValue(document.Height/2);
        apply=Button("Create array",()=>
        {
            try
            {
                if(layout is null)return;
                if(Current!=session||Snapshot(session)!=fingerprint||!canvas.Selection.SetEquals(scope))throw new InvalidDataException("Document or selection changed; reopen the array dialog.");
                var result=CircularArrays.Create(document,scope,Count(),Center(),Required(sweep),rotate.IsChecked==true);
                canvas.CancelDrawing();canvas.Selection.Clear();foreach(var id in result.Selection)canvas.Selection.Add(id);canvas.Apply(result.Document);dialog.Close();status.Text="Circular array created · originals and copies selected · one Undo step.";
            }
            catch(InvalidDataException ex){info.Text=ex.Message;apply.IsEnabled=false;}
        });
        var panel=new StackPanel {Spacing=12,Margin=new Thickness(18)};panel.Children.Add(Label("Repeat artwork around a center",18));
        panel.Children.Add(Label("Originals stay in place. A full ±360° circle excludes the duplicate endpoint; partial arcs include both ends. Positive angles turn clockwise on the canvas. Disable rotation for images; every image copy gets an independent layer. Overlaps are allowed: inspect Preview before Run."));
        var values=new WrapPanel();Inline(values,"Cells (total)",count,130);Inline(values,"Sweep · degrees",sweep,150);panel.Children.Add(values);
        var centers=new WrapPanel();Inline(centers,"Center X · "+(inches?"in":"mm"),cx,150);Inline(centers,"Center Y · "+(inches?"in":"mm"),cy,150);panel.Children.Add(centers);panel.Children.Add(rotate);panel.Children.Add(info);
        var actions=new WrapPanel();actions.Children.Add(apply);actions.Children.Add(Button("Cancel",dialog.Close));panel.Children.Add(actions);
        foreach(var number in new[]{count,sweep,cx,cy})number.ValueChanged+=(_,_)=>Update();rotate.IsCheckedChanged+=(_,_)=>Update();
        dialog.Content=new ScrollViewer {Content=panel};Update();await dialog.ShowDialog(this);
    }
}
