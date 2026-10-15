using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Dafeiyu
{
    public sealed class ActionPaletteWindow : Window
    {
        readonly PetWindow pet;
        readonly UniformGrid actions;
        readonly Button interactionsTab,basicTab;
        readonly TextBlock hint;
        readonly List<Button> actionButtons=new List<Button>();
        readonly DispatcherTimer availability=new DispatcherTimer { Interval=TimeSpan.FromMilliseconds(200) };
        bool basic,closed;

        public ActionPaletteWindow(PetWindow companion)
        {
            pet=companion;
            Ui.Window(this,"和"+pet.Skin.Name+"互动",354,434);
            Owner=pet;ShowInTaskbar=false;WindowStyle=WindowStyle.ToolWindow;ResizeMode=ResizeMode.NoResize;
            MinWidth=330;MinHeight=0;Topmost=pet.Topmost;WindowStartupLocation=WindowStartupLocation.Manual;
            var root=new Grid {Margin=new Thickness(16,12,16,14)};
            root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            var title=Ui.Text("一起玩一会儿",20);title.FontWeight=FontWeights.SemiBold;title.Margin=new Thickness(4,0,0,8);root.Children.Add(title);
            var tabs=new UniformGrid {Rows=1,Columns=2,Margin=new Thickness(0,0,0,12)};
            interactionsTab=Ui.Button("技能");interactionsTab.Name="PaletteInteractionsTab";interactionsTab.Margin=new Thickness(0,0,4,0);
            basicTab=Ui.Button("基础动作");basicTab.Name="PaletteBasicTab";basicTab.Margin=new Thickness(4,0,0,0);
            interactionsTab.Click+=delegate {SelectSection(false);};basicTab.Click+=delegate {SelectSection(true);};
            tabs.Children.Add(interactionsTab);tabs.Children.Add(basicTab);Grid.SetRow(tabs,1);root.Children.Add(tabs);
            actions=new UniformGrid {Columns=3,VerticalAlignment=VerticalAlignment.Top};
            var scroll=Ui.Scroll(actions);Grid.SetRow(scroll,2);root.Children.Add(scroll);
            hint=Ui.Text("点选动作播放 · Esc 或点击外部收起",11,Ui.Muted);hint.Margin=new Thickness(4,10,4,0);Grid.SetRow(hint,3);root.Children.Add(hint);Content=root;
            PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape){e.Handled=true;Close();}};
            Deactivated+=delegate {if(IsVisible && !closed)Close();};
            Closed+=delegate {closed=true;availability.Stop();};
            availability.Tick+=delegate {RefreshAvailability();};
            SelectSection(false);
        }

        public void ShowForPet()
        {
            if(closed || pet.DialogueBusy)return;
            if(!IsVisible)Show();
            UpdateLayout();PlaceNearPet();Activate();
            if(actionButtons.Count>0)actionButtons[0].Focus();
            availability.Start();
        }

        void SelectSection(bool showBasic)
        {
            basic=showBasic;actions.Children.Clear();actionButtons.Clear();
            SetTab(interactionsTab,!basic);SetTab(basicTab,basic);
            if(basic)
            {
                for(int index=0;index<AnimationPlayer.States.Length;index++)
                    AddAction(AnimationPlayer.Labels[index],AnimationPlayer.States[index],false);
            }
            else
            {
                AddAction("喂零食","feeding",true);
                AddAction(pet.IsSleeping?"叫醒":"小憩","sleeping",true);AddAction("专属动作","special",true);
            }
            Height=basic?550:300;
            RefreshAvailability();
            if(IsVisible){UpdateLayout();PlaceNearPet();}
        }

        static void SetTab(Button button,bool selected)
        {
            button.Background=selected?Ui.Blue:Brushes.White;button.Foreground=selected?Brushes.White:Ui.Ink;
            button.BorderBrush=selected?Ui.Blue:Ui.Line;button.FontWeight=selected?FontWeights.SemiBold:FontWeights.Normal;
            System.Windows.Automation.AutomationProperties.SetHelpText(button,selected?"当前动作分类":"切换动作分类");
        }

        void AddAction(string label,string action,bool interaction)
        {
            var tile=new StackPanel {HorizontalAlignment=HorizontalAlignment.Center};
            var image=new Image {Name="PalettePreview_"+action.Replace('-','_'),Source=pet.GetActionPreview(action,interaction),Width=72,Height=78,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,0,4)};
            RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality);tile.Children.Add(image);
            var caption=Ui.Text(label,12);caption.HorizontalAlignment=HorizontalAlignment.Center;tile.Children.Add(caption);
            var button=Ui.Button(label);button.Name=(interaction?"PaletteInteraction_":"PaletteAction_")+action.Replace('-','_');button.Content=tile;
            button.Padding=new Thickness(5);button.Margin=new Thickness(3);button.MinHeight=116;button.ToolTip=Description(action);
            System.Windows.Automation.AutomationProperties.SetName(button,label);
            button.Click+=delegate
            {
                if(pet.DialogueBusy){RefreshAvailability();return;}
                if(interaction)pet.PlayInteraction(action=="sleeping" && pet.IsSleeping?"wake":action);
                else pet.PlayAction(action,action=="jumping"?.7:2);
                Close();
            };
            actionButtons.Add(button);actions.Children.Add(button);
        }

        string Description(string action)
        {
            switch(action)
            {
                case "petting":return "轻轻摸摸头，看看伙伴的反应。";
                case "poked":return "戳一戳伙伴，逗它回应你。";
                case "feeding":return "请伙伴吃份零食，吃完会开心一下。";
                case "happy":return "让伙伴做个开心的小表情。";
                case "sleeping":return pet.IsSleeping?"叫醒正在小憩的伙伴。":"让伙伴安静地小憩，再点一次叫醒。";
                case "special":return "看看这位伙伴独有的招牌动作。";
                case "idle":return "回到平静的待机姿态。";
                case "running-right":return "原地演示向右奔跑的动作。";
                case "running-left":return "原地演示向左奔跑的动作。";
                case "waving":return "挥挥手，和你打个招呼。";
                case "jumping":return "轻快地跳一下。";
                case "failed":return "露出委屈的小表情。";
                case "waiting":return "安静地等待一会儿。";
                case "running":return "认真想一想。";
                default:return "认真检查一下成果。";
            }
        }

        void RefreshAvailability()
        {
            bool enabled=!pet.DialogueBusy;
            foreach(var button in actionButtons)button.IsEnabled=enabled;
            hint.Text=enabled?"点选动作播放 · Esc 或点击外部收起":"当前对话或更新结束后，就能继续互动。";
        }

        void PlaceNearPet()
        {
            var mine=new WindowInteropHelper(this).Handle;var owner=new WindowInteropHelper(pet).Handle;
            if(mine!=IntPtr.Zero && owner!=IntPtr.Zero)DesktopInterop.PlaceBeside(mine,owner);
        }
    }
}
