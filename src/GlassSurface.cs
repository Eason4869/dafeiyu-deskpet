using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
namespace Dafeiyu
{
    internal static class GlassSurface
    {
        public static Brush Background()
        {
            var brush=new LinearGradientBrush();brush.StartPoint=new Point(0,0);brush.EndPoint=new Point(1,1);
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(226,236,249),0));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(245,247,251),.48));
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(225,240,237),1));brush.Freeze();return brush;
        }
        public static Brush Panel()
        {
            var brush=new LinearGradientBrush();brush.StartPoint=new Point(.2,0);brush.EndPoint=new Point(.8,1);
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(242,255,255,255),0));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(195,250,252,255),.52));
            brush.GradientStops.Add(new GradientStop(Color.FromArgb(222,255,255,255),1));brush.Freeze();return brush;
        }
        public static Border Card(UIElement child)
        {
            var card=Ui.Card(child);card.Background=Panel();card.CornerRadius=new CornerRadius(18);
            card.BorderBrush=Ui.Brush("#ECFFFFFF");
            card.Effect=new DropShadowEffect{Color=Color.FromRgb(83,112,145),BlurRadius=15,ShadowDepth=3,Opacity=.085};return card;
        }
    }
}
