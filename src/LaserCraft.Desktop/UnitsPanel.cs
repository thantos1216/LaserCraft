using Avalonia.Controls;
using LaserCraft.Domain;
namespace LaserCraft.Desktop;
public sealed partial class MainWindow
{
    private bool inches,changingUnits;
    private readonly ComboBox unitChoice=new() { ItemsSource=new[]{"Metric · mm","Imperial · in"},SelectedIndex=0,Width=135 };
    private readonly TextBlock geometryUnits=new() { Text="mm" };
    private readonly List<(TextBlock Label,string Metric)> unitLabels=[];
    private NumericUpDown[] UnitInputs=>[x,y,width,height,speed,interval,jogDistance,jogFeed,positionX,positionY];
    private decimal DisplayValue(double mm)=>(decimal)Measurements.ToDisplay(mm,inches);
    private double Millimetres(NumericUpDown input)=>Measurements.ToMillimetres((double)(input.Value??0),inches&&UnitInputs.Contains(input));
    private void SetMillimetres(NumericUpDown input,double mm)=>input.Value=UnitInputs.Contains(input)?DisplayValue(mm):(decimal)mm;
    private void SetUnits(bool imperial)
    {
        if(changingUnits)return;changingUnits=true;
        try
        {
            var factor=(inches?25.4m:1)/(imperial?25.4m:1);inches=imperial;
            foreach(var n in UnitInputs){var value=n.Value*factor;var min=n.Minimum*factor;var max=n.Maximum*factor;var increment=n.Increment*factor;n.Minimum=-100000000;n.Maximum=100000000;n.Minimum=min;n.Maximum=max;n.Increment=increment;n.Value=value;n.FormatString=imperial?"0.#####":"0.###";}
            unitChoice.SelectedIndex=imperial?1:0;geometryUnits.Text=imperial?"in":"mm";
            foreach(var (label,metric) in unitLabels)label.Text=imperial?metric.Replace("mm","in"):metric;
            foreach(var session in sessions){session.Canvas.Inches=imperial;session.Canvas.InvalidateVisual();}
            if(sessions.Count>0){RefreshStatus();RefreshWorkspace();}
        }
        finally{changingUnits=false;}
    }
    private Control BuildUnitsControl()
    {
        unitChoice.SelectionChanged+=(_,_)=>{if(changingUnits)return;SetUnits(unitChoice.SelectedIndex==1);SavePreferences();};return unitChoice;
    }
}
