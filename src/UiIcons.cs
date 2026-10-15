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
                name=="expand"?"M3,9 L3,3 9,3 M15,3 L21,3 21,9 M21,15 L21,21 15,21 M9,21 L3,21 3,15":
                name=="phone"?"M4,15 C7,7 17,7 20,15 L17,18 14,15 14,12 10,12 10,15 7,18 Z":"M6,6 L18,18 M18,6 L6,18";
            return new Viewbox {Width=20,Height=20,Child=new Path {Data=Geometry.Parse(shape),Stroke=ink,StrokeThickness=1.8,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,StrokeLineJoin=PenLineJoin.Round,Stretch=Stretch.Uniform,Width=24,Height=24}};
        }
        public static Button Button(string icon,string label,bool primary=false)
        {
            var button=Ui.Button(label,primary);button.Content=Image(icon,primary?Brushes.White:Ui.Ink);button.ToolTip=label;
            button.Width=36;button.MinHeight=34;button.Padding=new Thickness(7);System.Windows.Automation.AutomationProperties.SetName(button,label);return button;
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
