using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace Dafeiyu
{
    // The guide points at the live editor. It never owns a second copy of settings.
    public sealed class OnboardingWindow : Window
    {
        private readonly PetWindow pet;
        private readonly SettingsWindow settings;
        private readonly Image avatar;
        private readonly TextBlock introduction,title,instruction,status;
        private readonly StackPanel actions;
        private readonly Button next,back;
        private readonly Border[] chips=new Border[6];
        private FrameworkElement target;
        private AdornerLayer layer;
        private GuideAdorner highlight;
        private bool finished,navigating,positioning,voiceAsr;
        public int Step {get;private set;}
        public string TargetName {get;private set;}
        public bool HighlightVisible {get{return highlight!=null && target!=null && target.IsVisible;}}

        public OnboardingWindow(PetWindow companion,SettingsWindow editor)
        {
            pet=companion;settings=editor;
            Step=pet.Settings.OnboardingState=="completed"?0:Math.Max(0,Math.Min(5,pet.Settings.OnboardingStep));
            if(pet.Settings.OnboardingState=="completed" || pet.Settings.OnboardingState=="skipped")pet.Settings.OnboardingState="deferred";
            Ui.Window(this,"新手引导 · 桌面伙伴",360,430);
            Owner=settings;ShowInTaskbar=false;WindowStyle=WindowStyle.ToolWindow;ResizeMode=ResizeMode.NoResize;
            MinWidth=280;MinHeight=240;WindowStartupLocation=WindowStartupLocation.Manual;
            var root=new Grid {Margin=new Thickness(20,16,20,18)};
            root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            root.RowDefinitions.Add(new RowDefinition());
            root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            var greeting=new DockPanel {Margin=new Thickness(0,0,0,15)};
            avatar=new Image {Source=SkinCatalog.GetPreview(pet.Skin),Width=56,Height=64,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,13,0)};
            RenderOptions.SetBitmapScalingMode(avatar,BitmapScalingMode.HighQuality);DockPanel.SetDock(avatar,Dock.Left);greeting.Children.Add(avatar);
            var hello=new StackPanel {VerticalAlignment=VerticalAlignment.Center};introduction=Ui.Text("和"+pet.Skin.Name+"一起开始",17);introduction.FontWeight=FontWeights.SemiBold;hello.Children.Add(introduction);
            hello.Children.Add(Ui.Text("跟着蓝色标记，直接试一试。",11,Ui.Muted));greeting.Children.Add(hello);root.Children.Add(greeting);
            var progress=new UniformGrid {Rows=1,Columns=6,Margin=new Thickness(0,0,0,17)};
            string[] names={"伙伴","互动","文字","语音","Live","开始"};
            for(int i=0;i<6;i++)
            {
                var label=Ui.Text((i+1)+" "+names[i],10);label.HorizontalAlignment=HorizontalAlignment.Center;
                chips[i]=new Border {Child=label,CornerRadius=new CornerRadius(7),Padding=new Thickness(2,5,2,5),Margin=new Thickness(0,0,i==5?0:3,0)};progress.Children.Add(chips[i]);
            }
            Grid.SetRow(progress,1);root.Children.Add(progress);
            var body=new StackPanel();title=Ui.Text("",20);title.FontWeight=FontWeights.SemiBold;title.Margin=new Thickness(0,0,0,9);body.Children.Add(title);
            instruction=Ui.Text("",12,Ui.Muted);instruction.Margin=new Thickness(0,0,0,12);body.Children.Add(instruction);
            actions=new StackPanel();body.Children.Add(actions);
            status=Ui.Text("",11,Ui.Blue);status.Name="GuideNotice";status.Margin=new Thickness(0,9,0,0);body.Children.Add(status);
            var returnTarget=Ui.Button("回到这一步的设置");returnTarget.Name="GuideReturnTarget";returnTarget.FontSize=11;returnTarget.MinHeight=28;returnTarget.Padding=new Thickness(0,3,0,3);returnTarget.Background=Brushes.Transparent;returnTarget.BorderBrush=Brushes.Transparent;returnTarget.Foreground=Ui.Blue;returnTarget.HorizontalAlignment=HorizontalAlignment.Left;returnTarget.Click+=delegate {Navigate();};body.Children.Add(returnTarget);
            var scroll=Ui.Scroll(body);Grid.SetRow(scroll,2);root.Children.Add(scroll);
            var footer=new StackPanel {Margin=new Thickness(0,14,0,0)};
            var navigation=new Grid();navigation.ColumnDefinitions.Add(new ColumnDefinition {Width=GridLength.Auto});navigation.ColumnDefinitions.Add(new ColumnDefinition());
            back=Ui.Button("上一步");back.Name="GuideBack";back.Click+=delegate {if(Step>0){Step--;voiceAsr=false;Navigate();}};navigation.Children.Add(back);
            next=Ui.Button("下一步",true);next.Name="GuideNext";next.IsDefault=true;next.Margin=new Thickness(0);next.Click+=delegate {Advance();};Grid.SetColumn(next,1);navigation.Children.Add(next);footer.Children.Add(navigation);
            var exits=new Grid {Margin=new Thickness(0,9,0,0)};exits.ColumnDefinitions.Add(new ColumnDefinition());exits.ColumnDefinitions.Add(new ColumnDefinition());
            var later=SmallButton("稍后再设置","GuideLater");later.Click+=delegate {Finish("deferred");};exits.Children.Add(later);
            var skip=SmallButton("跳过引导","GuideSkip");skip.Click+=delegate {Finish("skipped");};Grid.SetColumn(skip,1);exits.Children.Add(skip);footer.Children.Add(exits);
            Grid.SetRow(footer,3);root.Children.Add(footer);Content=root;
            Loaded+=delegate {Navigate();};
            settings.LocationChanged+=OwnerMoved;settings.SizeChanged+=OwnerSized;
            Closing+=ClosingGuide;Closed+=delegate {RemoveHighlight();settings.LocationChanged-=OwnerMoved;settings.SizeChanged-=OwnerSized;};
        }

        private static Button SmallButton(string text,string name)
        {
            var button=Ui.Button(text);button.Name=name;button.FontSize=11;button.MinHeight=26;button.Padding=new Thickness(2,3,2,3);button.Margin=new Thickness(0);button.Background=Brushes.Transparent;button.BorderBrush=Brushes.Transparent;button.Foreground=Ui.Muted;return button;
        }
        private void AddAction(string text,string name,Action action)
        {
            var button=Ui.Button(text);button.Name=name;button.FontSize=12;button.Margin=new Thickness(0,0,0,6);button.HorizontalAlignment=HorizontalAlignment.Left;button.Click+=delegate {action();};actions.Children.Add(button);
        }
        private void Advance()
        {
            if(Step>=2 && Step<=4 && !settings.SaveAiConfiguration())
            {status.Text="还有一项设置需要调整。请查看设置页下方的提示，修改后再继续。";return;}
            if(Step==5){Finish("completed");return;}
            Step++;voiceAsr=false;Navigate();
        }
        private void Navigate()
        {
            if(finished || navigating)return;navigating=true;
            try
            {
                RemoveHighlight();status.Text="";actions.Children.Clear();
                introduction.Text="和"+pet.Skin.Name+"一起开始";avatar.Source=SkinCatalog.GetPreview(pet.Skin);
                for(int i=0;i<chips.Length;i++){chips[i].Background=i==Step?Ui.Blue:i<Step?Ui.Mint:Ui.Pale;((TextBlock)chips[i].Child).Foreground=i==Step?Brushes.White:Ui.Muted;}
                back.IsEnabled=Step>0;next.Content=Step==5?"完成引导":"下一步";
                if(Step==0)
                {
                    title.Text="选一位桌面伙伴";instruction.Text="在蓝色标记处选择喜欢的角色。外观会立即切换，各自的人格和音色也会跟着切换。";
                    settings.SelectPage(0);TargetName="SkinPicker";
                }
                else if(Step==1)
                {
                    title.Text="让它适合你的桌面";instruction.Text="拖动大小滑块，找到舒服的尺寸。也可以试试摸摸头，之后用鼠标中键打开动作面板。";
                    settings.SelectPage(0);TargetName="SizeSlider";
                    AddAction("试试互动","GuideTryInteraction",delegate {pet.RestorePet();pet.PlayInteraction("petting");});
                }
                else if(Step==2)
                {
                    title.Text="连接文字对话";instruction.Text="选一个服务预设，再填写地址、模型和密钥。没有账号也可以先留空；点击下一步会保存这里的设置。";
                    settings.SelectAiSection(0);TargetName="TextServicePreset";
                }
                else if(Step==3)
                {
                    title.Text="听懂你，也说给你听";instruction.Text="语音识别把录音转成文字；朗读把回复读出来。云端朗读可开启后选音色，也可以保留系统语音。两项都可以稍后再配。";
                    settings.SelectAiSection(voiceAsr?1:2);TargetName=voiceAsr?"TranscriptionModel":"SpeechVoice";
                    AddAction("设置语音识别","GuideAsr",delegate {voiceAsr=true;Navigate();});
                    AddAction("设置声音与朗读","GuideTts",delegate {voiceAsr=false;Navigate();});
                }
                else if(Step==4)
                {
                    title.Text="想实时聊，再配置 Live";instruction.Text="Live 支持边说边听。填写兼容服务和音色，暂时不用可以留空。之后在聊天窗口点击开始，才会连接并使用麦克风。";
                    settings.SelectAiSection(3);TargetName="LiveVoice";
                }
                else
                {
                    title.Text="准备好，开始陪伴";instruction.Text="双击伙伴打开快速对话，中键打开动作面板。这里还能设置快捷键和免打扰，按自己的工作节奏来。";
                    settings.SelectPage(7);TargetName="Hotkeyquick";
                    AddAction("打开快速对话","GuideOpenChat",pet.ShowQuickChat);
                }
                settings.UpdateLayout();target=settings.FocusGuideTarget(TargetName);
                if(target!=null)
                {
                    target.IsVisibleChanged+=TargetVisibilityChanged;
                    layer=AdornerLayer.GetAdornerLayer(target);
                    if(layer!=null){highlight=new GuideAdorner(target);layer.Add(highlight);}
                }
                pet.Settings.OnboardingStep=Step;pet.SaveSettings();
                UpdateLayout();PositionNearTarget();
                // A posted pass only updates geometry after BringIntoView scrolls.
                Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(delegate {if(!finished){if(highlight!=null)highlight.InvalidateVisual();PositionNearTarget();}}));
            }
            finally{navigating=false;}
        }
        private void TargetVisibilityChanged(object sender,DependencyPropertyChangedEventArgs args)
        {
            if(!navigating && target!=null && !target.IsVisible){RemoveHighlight();status.Text="你切换了设置页，可点下方按钮回到当前步骤。";}
        }
        private void RemoveHighlight()
        {
            if(target!=null)target.IsVisibleChanged-=TargetVisibilityChanged;
            if(layer!=null && highlight!=null)layer.Remove(highlight);target=null;layer=null;highlight=null;
        }
        private void OwnerMoved(object sender,EventArgs args){PositionNearTarget();}
        private void OwnerSized(object sender,SizeChangedEventArgs args){if(highlight!=null)highlight.InvalidateVisual();PositionNearTarget();}
        private void PositionNearTarget()
        {
            if(!IsVisible || positioning)return;positioning=true;
            try
            {
                IntPtr mine=new WindowInteropHelper(this).Handle,owner=new WindowInteropHelper(settings).Handle;
                if(mine==IntPtr.Zero || owner==IntPtr.Zero)return;
                Rect area=DesktopInterop.WorkArea(owner),editor=DesktopInterop.Bounds(owner),bounds=DesktopInterop.Bounds(mine);
                var source=PresentationSource.FromVisual(this);Matrix toDevice=source==null?Matrix.Identity:source.CompositionTarget.TransformToDevice;
                Width=Math.Max(280,Math.Min(360,(area.Width-24)/toDevice.M11));Height=Math.Max(240,Math.Min(430,(area.Height-24)/toDevice.M22));
                double width=Width*toDevice.M11,height=Height*toDevice.M22;
                Rect focus=new Rect(editor.Left+editor.Width*.5,editor.Top+editor.Height*.5,1,1);
                if(target!=null && target.IsVisible)
                {
                    Point a=target.PointToScreen(new Point()),b=target.PointToScreen(new Point(target.ActualWidth,target.ActualHeight));focus=new Rect(a,b);
                }
                Point[] candidates={new Point(editor.Right+12,focus.Top),new Point(editor.Left-width-12,focus.Top),new Point(focus.Right+16,focus.Top),new Point(focus.Left-width-16,focus.Top),new Point(focus.Left,focus.Bottom+16),new Point(focus.Left,focus.Top-height-16),area.TopLeft,new Point(area.Right-width,area.Top),new Point(area.Left,area.Bottom-height),new Point(area.Right-width,area.Bottom-height)};
                double best=Double.MaxValue;Point chosen=bounds.TopLeft;
                foreach(Point candidate in candidates)
                {
                    Point clamped=PetMath.ClampPosition(candidate.X,candidate.Y,width,height,area);Rect rectangle=new Rect(clamped,new Size(width,height)),overlap=Rect.Intersect(rectangle,focus),editorOverlap=Rect.Intersect(rectangle,editor);
                    double score=(overlap.IsEmpty?0:overlap.Width*overlap.Height)*10000+(editorOverlap.IsEmpty?0:editorOverlap.Width*editorOverlap.Height)+Math.Abs(clamped.Y-focus.Y)*.01;
                    if(score<best){best=score;chosen=clamped;}
                }
                DesktopInterop.Move(mine,chosen);
            }
            finally{positioning=false;}
        }
        private void Finish(string state)
        {
            if(finished)return;pet.Settings.OnboardingState=state;pet.Settings.OnboardingStep=Step;pet.SaveSettings();finished=true;Close();
        }
        private void ClosingGuide(object sender,CancelEventArgs args)
        {
            if(!finished){pet.Settings.OnboardingState="deferred";pet.Settings.OnboardingStep=Step;pet.SaveSettings();finished=true;}
        }
        private sealed class GuideAdorner : Adorner
        {
            public GuideAdorner(UIElement element):base(element){IsHitTestVisible=false;}
            protected override void OnRender(DrawingContext drawing)
            {
                Rect rect=new Rect(AdornedElement.RenderSize);rect.Inflate(4,4);
                drawing.DrawRoundedRectangle(null,new Pen(Ui.Blue,2),rect,10,10);
            }
        }
    }
}
