using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Dafeiyu
{
    public sealed class ProviderPickerWindow : Window
    {
        public ServicePreset SelectedProvider {get;private set;}
        public ProviderPickerWindow(Window owner,string kind="text")
        {
            Ui.Window(this,"选择服务商",520,kind=="text"?550:300);Owner=owner;ShowInTaskbar=false;
            WindowStartupLocation=owner==null?WindowStartupLocation.CenterScreen:WindowStartupLocation.CenterOwner;
            var layout=new DockPanel{Margin=new Thickness(20)};
            var heading=new StackPanel{Margin=new Thickness(0,0,0,14)};
            var title=Ui.Text("选择要接入的服务商",19);title.FontWeight=FontWeights.SemiBold;heading.Children.Add(title);
            heading.Children.Add(Ui.Text(kind=="text"?"下一步填写密钥，获取并添加你需要的模型。":"自动填入地址和模型；填写密钥后即可保存，可手动修改模型。",12,Ui.Muted));DockPanel.SetDock(heading,Dock.Top);layout.Children.Add(heading);
            var cancel=Ui.Button("取消");cancel.IsCancel=true;cancel.HorizontalAlignment=HorizontalAlignment.Right;cancel.Margin=new Thickness(0,12,0,0);cancel.Click+=delegate{DialogResult=false;};DockPanel.SetDock(cancel,Dock.Bottom);layout.Children.Add(cancel);
            var choices=new WrapPanel();var seen=new HashSet<string>();
            foreach(var preset in ServicePresets.For(kind))
            {
                string brand=ProviderIdentity.Brand(preset.Url);if(!seen.Add(brand))continue;
                var config=new ConnectionSettings{BaseUrl=preset.Url};string name=ProviderIdentity.Name(config);
                var tile=new StackPanel{HorizontalAlignment=HorizontalAlignment.Center};tile.Children.Add(ProviderLogo.Create(preset.Url,name,29));
                var label=Ui.Text(name,12);label.Margin=new Thickness(0,7,0,0);label.HorizontalAlignment=HorizontalAlignment.Center;tile.Children.Add(label);
                var choice=Ui.Button("");choice.Name="ChooseProvider_"+(brand.Length==0?"custom":brand);choice.Content=tile;choice.Width=132;choice.Height=76;choice.Margin=new Thickness(4);choice.ToolTip=preset.Url.Length==0?"接入自己的兼容接口":preset.Url;
                choice.Click+=delegate{SelectedProvider=preset;DialogResult=true;};choices.Children.Add(choice);
            }
            layout.Children.Add(Ui.Scroll(choices));Content=layout;
        }
    }
}
