using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LaserCraft.Desktop;

public sealed partial class MainWindow
{
    private async Task PickLayerColor()
    {
        var session = Current; var index = session.Canvas.ActiveLayer; var initial = Color.Parse(layerColorValue);
        var dialog = new Window { Title = "Layer color", Width = 460, Height = 510, CanResize = false, WindowStartupLocation = WindowStartupLocation.CenterOwner, Background = Background, Foreground = Foreground };
        Slider red = new() { Minimum = 0, Maximum = 255, Value = initial.R }, green = new() { Minimum = 0, Maximum = 255, Value = initial.G }, blue = new() { Minimum = 0, Maximum = 255, Value = initial.B };
        var swatch = new Border { Height = 48, CornerRadius = new CornerRadius(5) };
        Color Read() => Color.FromRgb((byte)Math.Round(red.Value), (byte)Math.Round(green.Value), (byte)Math.Round(blue.Value));
        void Update() => swatch.Background = new SolidColorBrush(Read());
        foreach (var slider in new[] { red, green, blue }) { slider.Height = 26; slider.MinHeight = 26; slider.ValueChanged += (_, _) => Update(); }
        var panel = new StackPanel { Margin = new Thickness(20), Spacing = 8 }; panel.Children.Add(swatch); var palette = new WrapPanel();
        foreach (var hex in new[] { "#000000", "#FFFFFF", "#64748B", "#EF4444", "#F97316", "#FBBF24", "#84CC16", "#22C55E", "#14B8A6", "#06B6D4", "#3B82F6", "#6366F1", "#8B5CF6", "#D946EF", "#EC4899", "#A16207" })
        {
            var color = Color.Parse(hex); var button = Button("", () => { red.Value = color.R; green.Value = color.G; blue.Value = color.B; }); button.Width = 42; button.Height = 30; button.Background = new SolidColorBrush(color); Avalonia.Automation.AutomationProperties.SetName(button, "Color " + hex); palette.Children.Add(button);
        }
        panel.Children.Add(palette); panel.Children.Add(Label("Red")); panel.Children.Add(red); panel.Children.Add(Label("Green")); panel.Children.Add(green); panel.Children.Add(Label("Blue")); panel.Children.Add(blue);
        var actions = new WrapPanel(); actions.Children.Add(Button("Apply color", () => { var d = session.Canvas.History.Document; var layers = d.Layers.ToList(); var c = Read(); layers[index] = layers[index] with { Color = $"#{c.R:X2}{c.G:X2}{c.B:X2}" }; session.Canvas.Apply(d with { Layers = layers }); dialog.Close(); })); actions.Children.Add(Button("Cancel", () => dialog.Close())); panel.Children.Add(actions); Update(); dialog.Content = new ScrollViewer { Content = panel }; await dialog.ShowDialog(this);
    }
}
