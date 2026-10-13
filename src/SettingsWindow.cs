using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Interop;
using Microsoft.Win32;

namespace Dafeiyu
{
    public sealed class SettingsWindow : Window
    {
        private readonly PetWindow pet;
        private readonly TextBlock name,notice,sizeLabel;
        private readonly Image avatar;
        private readonly ComboBox skins;
        private readonly CheckBox follow,wander,top,pause,autoStart;
        private readonly StartupRegistration startup=new StartupRegistration(Process.GetCurrentProcess().MainModule.FileName);
        private readonly Slider size;
        private readonly ListBox navigation;
        private readonly ContentControl content;
        private readonly PersonaPanel persona;
        private readonly ConnectionSettingsPanel connections;
        private readonly ScrollViewer companionPage;
        private readonly AboutPanel about;
        private bool refreshing;
        public SettingsWindow(PetWindow companion)
        {
            pet=companion; Ui.Window(this,"桌面伙伴 · 设置",850,790);MinWidth=700;
            Icon=SkinCatalog.GetPreview(pet.Skin);
            var root=new StackPanel { Margin=new Thickness(24,20,24,20) };
            var header=new Grid { Margin=new Thickness(0,0,0,18) };
            header.ColumnDefinitions.Add(new ColumnDefinition { Width=new GridLength(118) }); header.ColumnDefinitions.Add(new ColumnDefinition());
            avatar=new Image { Source=SkinCatalog.GetPreview(pet.Skin),Width=106,Height=120,Stretch=Stretch.Uniform };
            RenderOptions.SetBitmapScalingMode(avatar,BitmapScalingMode.HighQuality);
            var avatarCard=new Border { Child=avatar,CornerRadius=new CornerRadius(24),Background=Ui.Pale,Padding=new Thickness(2),Margin=new Thickness(0,0,14,0) };
            header.Children.Add(avatarCard);
            var introduction=new StackPanel { VerticalAlignment=VerticalAlignment.Center };
            var eyebrow=Ui.Text("桌 面 伙 伴",11,Ui.Blue); eyebrow.Margin=new Thickness(0,0,0,3); introduction.Children.Add(eyebrow);
            name=Ui.Text(pet.Skin.Name,30); name.FontWeight=FontWeights.SemiBold; introduction.Children.Add(name);
            introduction.Children.Add(Ui.Text("陪你写代码，也陪你发会儿呆。",12,Ui.Muted));
            Grid.SetColumn(introduction,1); header.Children.Add(introduction); root.Children.Add(header);

            var appearance=new StackPanel(); appearance.Children.Add(Heading("选择伙伴"));
            var appearanceRow=new Grid { Margin=new Thickness(0,8,0,6) };
            appearanceRow.ColumnDefinitions.Add(new ColumnDefinition()); appearanceRow.ColumnDefinitions.Add(new ColumnDefinition { Width=GridLength.Auto });
            skins=new ComboBox { Name="SkinPicker",ItemsSource=pet.Catalog.Skins,SelectedItem=pet.Skin,MinHeight=36,Padding=new Thickness(8,4,8,4),Margin=new Thickness(0,0,10,0) };
            skins.SelectionChanged+=delegate
            {
                if(refreshing || skins.SelectedItem==null) return;
                var selected=(SkinDefinition)skins.SelectedItem;
                if(pet.SelectSkin(selected)) RefreshAvatar(); else { RefreshState(); ShowNotice(pet.Notice); }
            };
            appearanceRow.Children.Add(skins);
            var import=Ui.Button("导入外观"); import.Name="ImportSkin"; import.Margin=new Thickness(0); import.Click+=ImportSkin;
            Grid.SetColumn(import,1); appearanceRow.Children.Add(import); appearance.Children.Add(appearanceRow);
            appearance.Children.Add(Ui.Text("导入 8 列 × 11 行透明 PNG，或带人格预设的 pet.json。",11,Ui.Muted));
            var guide=new Expander{Name="ImportGuide",Header="如何制作和导入桌宠？",Margin=new Thickness(0,12,0,0)};var tutorial=new StackPanel{Margin=new Thickness(0,8,0,0)};
            tutorial.Children.Add(Ui.Text("1. 打开模板文件夹，复制 pet.json 和精灵图参考。\n2. 制作 8 列 × 11 行透明 PNG，推荐单格 480 × 520。\n3. 在 pet.json 填写名称、atlas 文件名与 system_prompt 人格。\n4. 将两个文件放在一起；外观包 ZIP 请先解压，再点“导入外观”选择 pet.json。\n直接导入 PNG 也可，之后到“人格”页编辑预设。",12,Ui.Muted));
            var guideButtons=new WrapPanel{Margin=new Thickness(0,10,0,0)};var templates=Ui.Button("打开模板文件夹");templates.Name="OpenTemplates";templates.Click+=delegate {OpenGuide("templates");};guideButtons.Children.Add(templates);var fullGuide=Ui.Button("查看完整教程");fullGuide.Name="OpenImportGuide";fullGuide.Click+=delegate {OpenGuide(Path.Combine("docs","skin-format.md"));};guideButtons.Children.Add(fullGuide);tutorial.Children.Add(guideButtons);guide.Content=tutorial;appearance.Children.Add(guide);root.Children.Add(Ui.Card(appearance));

            var behavior=new StackPanel(); behavior.Children.Add(Heading("陪伴方式"));
            follow=Ui.Toggle("视线跟随鼠标",pet.Settings.FollowCursor,delegate(bool value) { if(!refreshing) { pet.Settings.FollowCursor=value; pet.ApplySettings(); } }); follow.Name="FollowCursor"; behavior.Children.Add(follow);
            wander=Ui.Toggle("偶尔在桌面上散步",pet.Settings.Wander,delegate(bool value) { if(!refreshing) { pet.Settings.Wander=value; pet.ApplySettings(); } }); wander.Name="Wander"; behavior.Children.Add(wander);
            top=Ui.Toggle("保持在其他窗口上方",pet.Settings.Topmost,delegate(bool value) { if(!refreshing) { pet.Settings.Topmost=value; pet.ApplySettings(); } }); top.Name="Topmost"; behavior.Children.Add(top);
            autoStart=Ui.Toggle("开启自启",false,SetAutoStart);autoStart.Name="AutoStart";behavior.Children.Add(autoStart);
            behavior.Children.Add(Ui.Text("登录 Windows 时启动桌宠；关闭后立即撤销。",11,Ui.Muted));
            pause=Ui.Toggle("暂停动画",pet.Paused,delegate(bool value) { if(!refreshing) pet.SetPaused(value); }); pause.Name="Pause"; behavior.Children.Add(pause);
            var sizeRow=new DockPanel { Margin=new Thickness(0,10,0,8) };
            sizeLabel=Ui.Text("",12,Ui.Blue); DockPanel.SetDock(sizeLabel,Dock.Right); sizeRow.Children.Add(sizeLabel); sizeRow.Children.Add(Ui.Text("伙伴大小",12)); behavior.Children.Add(sizeRow);
            size=new Slider { Name="SizeSlider",Minimum=.75,Maximum=2.25,Value=pet.Settings.Scale,TickFrequency=.05,IsSnapToTickEnabled=true,SmallChange=.05,LargeChange=.25,Margin=new Thickness(0,0,0,4) };
            size.ValueChanged+=delegate { sizeLabel.Text=Math.Round(size.Value*100)+"%"; if(!refreshing) { pet.Settings.Scale=size.Value; pet.ApplySettings(); } }; behavior.Children.Add(size);
            behavior.Children.Add(Ui.Text("高清原图素材 · 适配 Windows 显示缩放",11,Ui.Muted)); root.Children.Add(Ui.Card(behavior));

            var actionPanel=new StackPanel(); actionPanel.Children.Add(Heading("打个招呼"));
            var actions=new WrapPanel { Margin=new Thickness(0,9,0,0) };
            AddAction(actions,"挥挥手","waving",1.2); AddAction(actions,"跳一下","jumping",.7); AddAction(actions,"想一想","running",2);
            AddAction(actions,"检查成果","review",2); AddAction(actions,"委屈一下","failed",2); AddAction(actions,"歇一会儿","waiting",2);
            actionPanel.Children.Add(actions); root.Children.Add(Ui.Card(actionPanel));
            root.Children.Add(Ui.Text("左键拖动 · 单击招呼 · 双击快速对话\n右键打开菜单；隐藏后可从系统托盘找回。",12,Ui.Muted));
            var footer=new WrapPanel { Margin=new Thickness(0,15,0,0) };
            var home=Ui.Button("回到角落",true); home.Name="ReturnHome"; home.Click+=delegate { pet.RestorePet(); pet.ReturnHome(); }; footer.Children.Add(home);
            var chat=Ui.Button("打开对话"); chat.Click+=delegate { pet.ShowChat(); }; footer.Children.Add(chat);
            var hide=Ui.Button("隐藏伙伴"); hide.Click+=delegate { pet.HidePet(); }; footer.Children.Add(hide);
            var exit=Ui.Button("退出桌宠"); exit.Click+=delegate { pet.Close(); }; footer.Children.Add(exit); root.Children.Add(footer);
            notice=Ui.Text("",11,Ui.Muted); notice.Margin=new Thickness(0,12,0,0); root.Children.Add(notice);
            companionPage=Ui.Scroll(root);persona=new PersonaPanel(pet);connections=new ConnectionSettingsPanel(pet);about=new AboutPanel(pet);content=new ContentControl();
            navigation=new ListBox{Name="SettingsNavigation",BorderThickness=new Thickness(0),Background=Brushes.Transparent,Margin=new Thickness(12,0,12,0),Padding=new Thickness(0)};
            foreach(string label in new[]{"伙伴与外观","人格","对话模型","语音识别","声音与朗读","Live 实时语音","关于与更新"})navigation.Items.Add(new ListBoxItem{Content=label,Padding=new Thickness(14,12,14,12),Margin=new Thickness(0,0,0,6),FontSize=13});
            var shell=new Grid();shell.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(174)});shell.ColumnDefinitions.Add(new ColumnDefinition());var sidebar=new StackPanel();var brand=Ui.Text("桌面伙伴",19,Ui.Blue);brand.FontWeight=FontWeights.SemiBold;brand.Margin=new Thickness(26,30,12,2);sidebar.Children.Add(brand);var sub=Ui.Text("给陪伴一点个性",11,Ui.Muted);sub.Margin=new Thickness(26,0,12,28);sidebar.Children.Add(sub);sidebar.Children.Add(navigation);
            shell.Children.Add(new Border{Child=sidebar,Background=Ui.Pale,BorderBrush=Ui.Line,BorderThickness=new Thickness(0,0,1,0)});Grid.SetColumn(content,1);shell.Children.Add(content);Content=shell;
            navigation.SelectionChanged+=delegate {int index=navigation.SelectedIndex;if(index==0)content.Content=companionPage;else if(index==1)content.Content=persona;else if(index==6)content.Content=about;else{content.Content=connections;connections.ShowSection(index-2);}};navigation.SelectedIndex=0;Closed+=delegate {about.Dispose();};
            Loaded+=delegate { DesktopInterop.PlaceBeside(new WindowInteropHelper(this).Handle,new WindowInteropHelper(pet).Handle); };
            RefreshState(); ShowNotice(pet.Notice ?? pet.Store.Warning);
        }
        private static TextBlock Heading(string text) { var label=Ui.Text(text,14); label.FontWeight=FontWeights.SemiBold; return label; }
        private void AddAction(Panel panel,string caption,string state,double duration)
        { var button=Ui.Button(caption); button.Margin=new Thickness(0,0,8,8); button.Click+=delegate { pet.PlayAction(state,duration); }; panel.Children.Add(button); }
        private void ImportSkin(object sender,RoutedEventArgs args)
        {
            var dialog=new OpenFileDialog { Title="导入桌宠外观",Filter="桌宠外观 (*.png;pet.json)|*.png;*.json|透明 PNG 精灵图|*.png|外观说明 (*.json)|*.json",CheckFileExists=true,Multiselect=false };
            if(dialog.ShowDialog(this)!=true) return;
            try
            {
                var skin=pet.Catalog.Import(dialog.FileName);
                refreshing=true; skins.ItemsSource=null; skins.ItemsSource=pet.Catalog.Skins; refreshing=false;
                skins.SelectedItem=skin; ShowNotice("已导入「"+skin.Name+"」，外观保存在桌宠的 data 文件夹。");
            }
            catch(Exception e)
            {
                refreshing=false;
                if(!(e is IOException || e is UnauthorizedAccessException || e is FileFormatException || e is InvalidOperationException || e is NotSupportedException || e is ArgumentException || e is System.Runtime.Serialization.SerializationException)) throw;
                ShowNotice("导入失败：请使用有效的透明 PNG 精灵图（8 列 × 11 行，每个动作都应有画面），或同文件夹内的 pet.json。");
            }
        }
        private void RefreshAvatar() { name.Text=pet.Skin.Name; avatar.Source=SkinCatalog.GetPreview(pet.Skin); Icon=avatar.Source;if(persona!=null)persona.RefreshPersona(); }
        public void RefreshState()
        {
            if(name.Text!=pet.Skin.Name)RefreshAvatar();
            refreshing=true; follow.IsChecked=pet.Settings.FollowCursor; wander.IsChecked=pet.Settings.Wander;
            top.IsChecked=pet.Settings.Topmost; pause.IsChecked=pet.Paused; size.Value=pet.Settings.Scale;
            sizeLabel.Text=Math.Round(pet.Settings.Scale*100)+"%"; skins.SelectedItem=pet.Skin; refreshing=false;
            RefreshAutoStart();
        }
        private void SetAutoStart(bool enabled)
        {
            if(refreshing)return;
            try{startup.SetEnabled(enabled);ShowNotice(enabled?"已开启自启，登录 Windows 时启动。":"已关闭自启。");}
            catch(Exception e){if(!(e is UnauthorizedAccessException || e is System.Security.SecurityException || e is IOException))throw;ShowNotice("无法修改当前用户的自启设置，请检查 Windows 权限。");}
            RefreshAutoStart();
        }
        private void RefreshAutoStart()
        {
            refreshing=true;
            try{autoStart.IsChecked=startup.IsEnabled;}
            catch(Exception e){if(!(e is UnauthorizedAccessException || e is System.Security.SecurityException || e is IOException))throw;autoStart.IsEnabled=false;ShowNotice("无法读取当前用户的自启设置。");}
            finally{refreshing=false;}
        }
        public void ShowNotice(string text) { if(notice!=null) notice.Text=text ?? ""; }
        public void SelectConnections() { navigation.SelectedIndex=2; }
        public void SelectPage(int index){navigation.SelectedIndex=index;}
        private void OpenGuide(string relative)
        {
            try{string path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,relative);if(!File.Exists(path) && !Directory.Exists(path))path=Path.Combine(Path.GetDirectoryName(AppDomain.CurrentDomain.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar)),relative);if(!File.Exists(path) && !Directory.Exists(path))throw new InvalidOperationException();Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
            catch(Exception e){if(!(e is IOException || e is InvalidOperationException || e is System.ComponentModel.Win32Exception))throw;ShowNotice("找不到教程或模板。请完整解压桌宠 ZIP，保留 docs 和 templates 文件夹。");}
        }
    }
}
