using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace Dafeiyu
{
    public static class UiIcons
    {
        public static FrameworkElement Image(string name,Brush ink)
        {
            string shape=name=="speaker"?"M3,9 L7,9 12,5 12,19 7,15 3,15 Z M16,8 C20,10 20,14 16,16 M18,4 C26,9 26,15 18,20":
                name=="muted"?"M3,9 L7,9 12,5 12,19 7,15 3,15 Z M16,9 L22,15 M22,9 L16,15":
                name=="live"?"M4,9 L4,15 M8,5 L8,19 M12,2 L12,22 M16,6 L16,18 M20,9 L20,15":
                name=="stop"?"M6,6 L18,6 18,18 6,18 Z":
                name=="expand"?"M14,4 L20,4 20,10 M20,4 L11,13 M10,5 L5,5 5,19 19,19 19,14":
                name=="send"?"M12,19 L12,5 M6,11 L12,5 18,11":
                name=="back"?"M19,12 L5,12 M11,6 L5,12 11,18":
                name=="edit"?"M4,20 L5,15 16,4 C18,2 22,6 20,8 L9,19 Z M14,6 L18,10":
                name=="down"?"M6,9 L12,15 18,9":
                name=="queue"?"M4,4 L4,16 Q4,19 7,19 L20,19 M17,16 L20,19 17,22 M9,5 L20,5 M9,10 L17,10 M9,14 L14,14":
                name=="steer"?"M4,5 L4,10 Q4,13 8,13 L20,13 M15,8 L20,13 15,18":
                name=="mic"?"M9,6 C9,2 15,2 15,6 L15,12 C15,16 9,16 9,12 Z M5,11 L5,12 C5,21 19,21 19,12 L19,11 M12,19 L12,22":
                name=="trash"?"M4,6 L20,6 M9,6 L9,3 15,3 15,6 M6,6 L7,21 17,21 18,6 M10,10 L10,17 M14,10 L14,17":
                name=="settings"?"M4,7 L20,7 M4,17 L20,17 M9,4 L9,10 M15,14 L15,20":
                name=="phone"?"M3,10 C8,5 16,5 21,10 L20,15 C19,16 16,14 15,13 L15,10 C13,9 11,9 9,10 L9,13 C8,14 5,16 4,15 Z":"M6,6 L18,18 M18,6 L6,18";
            // Keep every glyph on the same 24-unit canvas. Stretching the path
            // itself made small shapes (especially stop/expand) look oversized.
            var canvas=new Canvas {Width=24,Height=24};
            // The arrowhead carries more visual weight than the stem. A small
            // optical correction centers the send glyph in both circular buttons.
            if(name=="send")canvas.RenderTransform=new TranslateTransform(0,1);
            Geometry geometry=name=="stop" || name=="stop-solid"?new RectangleGeometry(new Rect(6,6,12,12),1.5,1.5):Geometry.Parse(shape);
            if(name=="stop")ink=Ui.Brush("#CC4055");
            if(name=="settings")
            {
                var gear=new StreamGeometry();
                using(var context=gear.Open())
                {
                    for(int i=0;i<32;i++)
                    {
                        double angle=(i*11.25-5.625)*Math.PI/180,radius=i%4<2?9.5:7.5;
                        var point=new Point(12+Math.Cos(angle)*radius,12+Math.Sin(angle)*radius);
                        if(i==0)context.BeginFigure(point,false,true);else context.LineTo(point,true,false);
                    }
                }
                var group=new GeometryGroup();group.Children.Add(gear);group.Children.Add(new EllipseGeometry(new Point(12,12),3,3));geometry=group;
            }
            geometry.Freeze();
            canvas.Children.Add(new Path {Data=geometry,Stroke=ink,Fill=name=="phone" || name=="stop-solid"?ink:null,StrokeThickness=name=="queue"?1.5:1.7,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round});
            return new Viewbox {Width=18,Height=18,Child=canvas};
        }
        public static Button Button(string icon,string label,bool primary=false)
        {
            var button=Ui.Button(label,primary);button.Content=Image(icon,primary?Brushes.White:Ui.Ink);button.ToolTip=label;
            button.Width=36;button.Height=36;button.MinHeight=36;button.Padding=new Thickness(8);button.Margin=new Thickness(0,0,6,0);
            if(!primary){button.Background=Brushes.Transparent;button.BorderBrush=Brushes.Transparent;}
            System.Windows.Automation.AutomationProperties.SetName(button,label);return button;
        }
        public static CheckBox Speech(PetWindow pet)
        {
            var toggle=new CheckBox {Name="TtsOutput",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,0,0,0),Cursor=System.Windows.Input.Cursors.Hand,Focusable=true};
            toggle.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='CheckBox'><Border x:Name='b' Background='Transparent' CornerRadius='9' Padding='7' BorderBrush='Transparent' BorderThickness='1'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='b' Property='BorderBrush' Value='#456DE6'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='b' Property='Background' Value='#EDF3FF'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='b' Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            Action sync=delegate {toggle.IsChecked=pet.Settings.ReadReplies;toggle.Content=Image(pet.Settings.ReadReplies?"speaker":"muted",pet.Settings.ReadReplies?Ui.Blue:Ui.Muted);toggle.ToolTip=pet.Settings.ReadReplies?"关闭回复朗读":"开启回复朗读";System.Windows.Automation.AutomationProperties.SetName(toggle,(string)toggle.ToolTip);};
            toggle.Checked+=delegate {pet.SetReplySpeech(true);sync();};toggle.Unchecked+=delegate {pet.SetReplySpeech(false);sync();};
            toggle.Loaded+=delegate {sync();pet.ReplySpeechChanged+=sync;};toggle.Unloaded+=delegate {pet.ReplySpeechChanged-=sync;};sync();return toggle;
        }
    }
}
