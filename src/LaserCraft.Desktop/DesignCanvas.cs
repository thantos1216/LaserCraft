using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using LaserCraft.Domain;

namespace LaserCraft.Desktop;
public sealed class DesignCanvas : Control
{
    public EditorHistory History { get; } = new();
    public Guid? Selected { get; set; }
    public string Tool { get; set; } = "Select";
    public bool Snap { get; set; } = true;
    public int ActiveLayer { get; set; }
    public event Action? Changed;
    private Point? start;
    private DesignShape? original, preview;
    private double zoom = 1;
    private Vector pan;
    private Point? panStart;
    private double Scale => Math.Max(0.05, Math.Min((Bounds.Width - 80) / History.Document.Width, (Bounds.Height - 80) / History.Document.Height)) * zoom;
    private Point Origin => new((Bounds.Width - History.Document.Width * Scale) / 2 + pan.X, (Bounds.Height - History.Document.Height * Scale) / 2 + pan.Y);
    public DesignCanvas() { Focusable = true; ClipToBounds = true; }
    private Point World(Point p) { var o = Origin; return new((p.X-o.X)/Scale, (p.Y-o.Y)/Scale); }
    private Point Snapped(Point p) => Snap ? new(Math.Round(p.X), Math.Round(p.Y)) : p;
    private static IBrush Brush(string color) { try { return new SolidColorBrush(Color.Parse(color)); } catch { return Brushes.Cyan; } }
    public void Refresh() { InvalidateVisual(); Changed?.Invoke(); }
    public void Fit() { zoom = 1; pan = default; InvalidateVisual(); }
    public void Apply(DesignDocument d) { History.Apply(d); Refresh(); }
    public void DeleteSelected() { if (Selected is not null) { Apply(History.Document with { Shapes = History.Document.Shapes.Where(s => s.Id != Selected).ToList() }); Selected = null; Refresh(); } }
    public override void Render(DrawingContext ctx)
    {
        base.Render(ctx); ctx.FillRectangle(Brush("#0A111B"), new Rect(Bounds.Size));
        var d = History.Document; var o = Origin; var scale = Scale;
        var bed = new Rect(o, new Size(d.Width*scale,d.Height*scale));
        ctx.DrawRectangle(Brush("#101E2B"), new Pen(Brush("#3B5366"),1),bed);
        using (ctx.PushClip(bed))
        {
            var step = scale < .4 ? 50 : 10;
            var grid = new Pen(Brush("#203242"), .5);
            for (double x=0; x<=d.Width; x+=step) ctx.DrawLine(grid,new(o.X+x*scale,o.Y),new(o.X+x*scale,bed.Bottom));
            for (double y=0; y<=d.Height; y+=step) ctx.DrawLine(grid,new(o.X,o.Y+y*scale),new(bed.Right,o.Y+y*scale));
            foreach (var stored in d.Shapes) Draw(preview?.Id == stored.Id ? preview : stored);
            if (preview is not null && !d.Shapes.Any(s=>s.Id==preview.Id)) Draw(preview);
        }
        void Draw(DesignShape s)
        {
            var r = new Rect(o.X+s.X*scale,o.Y+s.Y*scale,s.Width*scale,s.Height*scale);
            var pen = new Pen(Brush(d.Layers[s.Layer].Color),1.8);
            if (s.Kind==ShapeKind.Rectangle) ctx.DrawRectangle(null,pen,r); else ctx.DrawEllipse(null,pen,r);
            if (s.Id==Selected) ctx.DrawRectangle(null,new Pen(Brushes.White,.8,DashStyle.Dash),r.Inflate(4));
        }
    }
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        Focus(); var props=e.GetCurrentPoint(this).Properties;
        if (props.IsMiddleButtonPressed) { panStart=e.GetPosition(this); e.Pointer.Capture(this); return; }
        if (!props.IsLeftButtonPressed) return;
        start=Snapped(World(e.GetPosition(this))); var p=start.Value; original=null; preview=null;
        if (Tool=="Select")
        {
            original=History.Document.Shapes.LastOrDefault(s=>p.X>=s.X && p.X<=s.X+s.Width && p.Y>=s.Y && p.Y<=s.Y+s.Height);
            Selected=original?.Id;
        }
        else if (p.X>=0 && p.Y>=0 && p.X<=History.Document.Width && p.Y<=History.Document.Height)
        { preview=new(Guid.NewGuid(),Tool=="Rectangle"?ShapeKind.Rectangle:ShapeKind.Ellipse,p.X,p.Y,.1,.1,ActiveLayer); Selected=preview.Id; }
        e.Pointer.Capture(this); Refresh();
    }
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        if (panStart is Point last) { var current=e.GetPosition(this); pan+=current-last; panStart=current; InvalidateVisual(); return; }
        if (start is not Point a) return;
        var p=Snapped(World(e.GetPosition(this)));
        if (original is not null) preview=original with { X=original.X+p.X-a.X,Y=original.Y+p.Y-a.Y };
        else if (preview is not null) preview=preview with { X=Math.Min(a.X,p.X),Y=Math.Min(a.Y,p.Y),Width=Math.Max(.1,Math.Abs(p.X-a.X)),Height=Math.Max(.1,Math.Abs(p.Y-a.Y)) };
        InvalidateVisual();
    }
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        if (preview is not null && preview != original)
        {
            var items=History.Document.Shapes.Select(s=>s.Id==preview.Id?preview:s).ToList();
            if(!items.Any(s=>s.Id==preview.Id)) items.Add(preview);
            History.Apply(History.Document with { Shapes=items });
        }
        preview=null; original=null; start=null; panStart=null; e.Pointer.Capture(null); Refresh();
    }
    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e) { start=null; panStart=null; original=null; preview=null; InvalidateVisual(); }
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e) { zoom=Math.Clamp(zoom*(e.Delta.Y>0?1.12:1/1.12),.25,8); InvalidateVisual(); e.Handled=true; }
    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key==Key.Delete) { DeleteSelected(); e.Handled=true; }
        else if (e.Key==Key.Escape) { Selected=null; preview=null; start=null; Refresh(); }
        base.OnKeyDown(e);
    }
}
