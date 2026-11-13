namespace LaserCraft.Domain;
public enum MaterialTestAxis {Speed,Power,Passes}
public enum MaterialTestShape {Square,Circle}
public sealed record MaterialTestOptions(int Rows=5,int Columns=5,double CellWidth=10,double CellHeight=10,double Gap=3,double X=10,double Y=10,MaterialTestAxis RowAxis=MaterialTestAxis.Power,MaterialTestAxis ColumnAxis=MaterialTestAxis.Speed,double RowMinimum=10,double RowMaximum=50,double ColumnMinimum=300,double ColumnMaximum=1500,double Speed=600,double Power=20,int Passes=1,CutMode Mode=CutMode.Line,MaterialTestShape Shape=MaterialTestShape.Square,bool AirAssist=false,double Interval=.1,bool GroupCells=true);
public sealed record MaterialTestCell(int Row,int Column,double Speed,double Power,int Passes);
public sealed record MaterialTestResult(DesignDocument Document,IReadOnlyList<MaterialTestCell> Cells);
public static class MaterialTests
{
    public static MaterialTestResult Create(double bedWidth,double bedHeight,MaterialTestOptions o)
    {
        if(o.Rows<1||o.Columns<1||(long)o.Rows*o.Columns>256||o.RowAxis==o.ColumnAxis||!Enum.IsDefined(o.RowAxis)||!Enum.IsDefined(o.ColumnAxis)||!Enum.IsDefined(o.Shape)||o.Mode is not(CutMode.Line or CutMode.Fill))throw new InvalidDataException("Choose distinct axes, Line/Fill and 1–256 total cells.");
        if(!double.IsFinite(o.CellWidth)||!double.IsFinite(o.CellHeight)||!double.IsFinite(o.Gap)||o.CellWidth<=0||o.CellHeight<=0||o.Gap<0||!double.IsFinite(o.X)||!double.IsFinite(o.Y)||o.X<0||o.Y<0||!double.IsFinite(o.RowMinimum)||!double.IsFinite(o.RowMaximum)||!double.IsFinite(o.ColumnMinimum)||!double.IsFinite(o.ColumnMaximum)||o.RowMinimum>o.RowMaximum||o.ColumnMinimum>o.ColumnMaximum)throw new InvalidDataException("Use finite positive cell sizes, nonnegative gaps/position and increasing ranges.");
        if(o.X+o.Columns*o.CellWidth+(o.Columns-1)*o.Gap>bedWidth||o.Y+o.Rows*o.CellHeight+(o.Rows-1)*o.Gap>bedHeight)throw new InvalidDataException("Test grid does not fit the bed; reduce dimensions/count or position.");
        double Value(double lo,double hi,int index,int count)=>count==1?lo:lo+(hi-lo)*index/(count-1);
        var group=o.GroupCells?Guid.NewGuid():(Guid?)null;var shapes=new List<DesignShape>();var layers=new List<CutLayer>();var cells=new List<MaterialTestCell>();
        for(int row=0;row<o.Rows;row++)for(int col=0;col<o.Columns;col++)
        {
            double speed=o.Speed,power=o.Power;int passes=o.Passes;
            void Set(MaterialTestAxis axis,double value){switch(axis){case MaterialTestAxis.Speed:speed=value;break;case MaterialTestAxis.Power:power=value;break;case MaterialTestAxis.Passes:if(value<1||value>100)throw new InvalidDataException("Pass range must be 1–100.");passes=(int)Math.Round(value,MidpointRounding.AwayFromZero);break;}}
            Set(o.RowAxis,Value(o.RowMinimum,o.RowMaximum,row,o.Rows));Set(o.ColumnAxis,Value(o.ColumnMinimum,o.ColumnMaximum,col,o.Columns));
            var layer=layers.Count;layers.Add(new($"R{row+1} C{col+1} · {speed:0.###}/{power:0.###}/{passes}",LayerPalette.Colors[layer%LayerPalette.Colors.Length],Speed:speed,Power:power,Mode:o.Mode,Passes:passes,AirAssist:o.AirAssist,Interval:o.Interval));
            shapes.Add(new(Guid.NewGuid(),o.Shape==MaterialTestShape.Circle?ShapeKind.Ellipse:ShapeKind.Rectangle,o.X+col*(o.CellWidth+o.Gap),o.Y+row*(o.CellHeight+o.Gap),o.CellWidth,o.CellHeight,layer,GroupId:group));cells.Add(new(row+1,col+1,speed,power,passes));
        }
        var d=new DesignDocument(1,"Material test",bedWidth,bedHeight,shapes,layers);d.Validate();return new(d,cells.AsReadOnly());
    }
}
