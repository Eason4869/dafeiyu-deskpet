using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Dafeiyu
{
    // A shared composer keeps the small bubble and full conversation consistent.
    internal static class ChatUi
    {
        static ControlTemplate actionTemplate;
        public static FrameworkElement Composer(TextBox input,UIElement leading,UIElement trailing,TextBlock status,bool compact)
        {
            input.BorderThickness=new Thickness(0);input.Background=Brushes.Transparent;
            input.Padding=new Thickness(2,6,2,6);input.FontSize=14;input.VerticalContentAlignment=VerticalAlignment.Center;
            input.MinHeight=compact?48:62;input.MaxHeight=compact?92:140;
            input.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
            System.Windows.Automation.AutomationProperties.SetName(input,"消息输入框");
            var field=new Grid();field.Children.Add(input);
            var placeholder=Ui.Text("想聊些什么？",14,Ui.Muted);
            placeholder.Margin=new Thickness(3,0,0,0);placeholder.VerticalAlignment=VerticalAlignment.Center;placeholder.IsHitTestVisible=false;
            field.Children.Add(placeholder);
            input.TextChanged+=delegate{placeholder.Visibility=input.Text.Length==0?Visibility.Visible:Visibility.Collapsed;};
            var body=new Grid();body.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});body.ColumnDefinitions.Add(new ColumnDefinition());body.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            var left=new Border{Child=leading,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,8,0)};
            if(leading==null)left.Visibility=Visibility.Collapsed;
            var right=new Border{Child=trailing,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,0,0,0)};
            body.Children.Add(left);Grid.SetColumn(field,1);body.Children.Add(field);Grid.SetColumn(right,2);body.Children.Add(right);
            var surface=new Border{Child=body,Background=Brushes.White,BorderBrush=Ui.Brush("#E3E7ED"),BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(22),Padding=new Thickness(8)};
            input.GotKeyboardFocus+=delegate{surface.BorderBrush=Ui.Blue;};
            input.LostKeyboardFocus+=delegate{surface.BorderBrush=Ui.Brush("#E3E7ED");};
            var result=new StackPanel();result.Children.Add(surface);
            status.FontSize=11;status.Margin=new Thickness(3,7,3,0);status.MaxHeight=38;
            result.Children.Add(status);
            return result;
        }
        public static Button Action(string icon,string label)
        {
            var button=UiIcons.Button(icon,label);button.Content=UiIcons.Image(icon,icon=="send"?Ui.Blue:Ui.Ink);
            ((FrameworkElement)button.Content).Width=20;((FrameworkElement)button.Content).Height=20;
            if(actionTemplate==null)actionTemplate=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='Button'><Border x:Name='surface' Background='{TemplateBinding Background}' BorderBrush='{TemplateBinding BorderBrush}' BorderThickness='1' CornerRadius='18'><ContentPresenter HorizontalAlignment='Center' VerticalAlignment='Center'/></Border><ControlTemplate.Triggers><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='surface' Property='BorderBrush' Value='#AAB8CC'/></Trigger><Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='surface' Property='BorderBrush' Value='#456DE6'/></Trigger><Trigger Property='IsPressed' Value='True'><Setter TargetName='surface' Property='Opacity' Value='0.65'/></Trigger><Trigger Property='IsEnabled' Value='False'><Setter TargetName='surface' Property='Opacity' Value='0.45'/></Trigger></ControlTemplate.Triggers></ControlTemplate>");
            button.Template=actionTemplate;
            button.Background=Ui.Brush(icon=="send"?"#F0F4FF":"#FFF3F5");
            button.BorderBrush=Ui.Brush(icon=="send"?"#DBE4FC":"#F5DCE1");button.Margin=new Thickness(0);
            return button;
        }
    }
}
