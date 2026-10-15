using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
namespace Dafeiyu
{
    public sealed class LiveCallWindow : Window
    {
        readonly PetWindow pet;readonly GeminiLiveSession session;readonly VoiceOrbit orbit;readonly TextBlock status,subtitle,mode;
        readonly Button connect,end;readonly DispatcherTimer meter;bool connecting,closed,stopping,stopCompleted,returnToChat;
        public LiveCallWindow(PetWindow companion)
        {
            pet=companion;session=pet.Live;Ui.Window(this,pet.Skin.Name+" · Live 通话",460,640);MinWidth=360;MinHeight=480;ShowInTaskbar=false;Owner=pet;
            var root=new Grid{Margin=new Thickness(28,22,28,24)};root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var header=new DockPanel();var back=UiIcons.Button("expand","结束通话并返回文字对话");back.Name="LiveBackToText";back.Click+=delegate{returnToChat=true;Close();};DockPanel.SetDock(back,Dock.Right);header.Children.Add(back);
            var heading=new StackPanel();var title=Ui.Text(pet.Skin.Name,24);title.FontWeight=FontWeights.SemiBold;heading.Children.Add(title);heading.Children.Add(Ui.Text("Live 语音通话",12,Ui.Muted));header.Children.Add(heading);root.Children.Add(header);
            var center=new Grid{Margin=new Thickness(0,12,0,12)};center.RowDefinitions.Add(new RowDefinition());center.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var stage=new Grid{MaxWidth=330,MaxHeight=330};orbit=new VoiceOrbit{Name="LiveVoiceOrbit"};stage.Children.Add(orbit);
            var avatar=new Image{Source=SkinCatalog.GetPreview(pet.Skin),Width=118,Height=132,Stretch=Stretch.Uniform};RenderOptions.SetBitmapScalingMode(avatar,BitmapScalingMode.HighQuality);stage.Children.Add(avatar);center.Children.Add(stage);
            var captions=new StackPanel{Margin=new Thickness(0,10,0,0)};mode=Ui.Text("准备连接",15,Ui.Blue);mode.HorizontalAlignment=HorizontalAlignment.Center;captions.Children.Add(mode);
            status=Ui.Text("",11,Ui.Muted);status.Name="LiveCallStatus";status.TextAlignment=TextAlignment.Center;status.Margin=new Thickness(0,5,0,10);captions.Children.Add(status);
            subtitle=Ui.Text("说话时，声音会在这里变成字幕。",14);subtitle.Name="LiveCallTranscript";subtitle.TextAlignment=TextAlignment.Center;
            captions.Children.Add(new Border{Child=new ScrollViewer{Content=subtitle,MaxHeight=88,VerticalScrollBarVisibility=ScrollBarVisibility.Auto},Padding=new Thickness(14),Background=Brushes.White,CornerRadius=new CornerRadius(16)});Grid.SetRow(captions,1);center.Children.Add(captions);Grid.SetRow(center,1);root.Children.Add(center);
            var footer=new StackPanel();var buttons=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Center};
            connect=Ui.Button("重新连接",true);connect.Name="LiveReconnect";connect.Click+=async delegate{await ConnectAsync();};buttons.Children.Add(connect);
            end=UiIcons.Button("phone","结束通话");end.Name="LiveEndCall";end.Width=56;end.Height=48;end.Background=Ui.Brush("#FCE8EC");end.BorderBrush=Ui.Brush("#E6A5B5");end.Click+=delegate{Close();};buttons.Children.Add(end);
            var settings=Ui.Button("AI 配置");settings.Name="LiveCallSettings";settings.Click+=delegate{pet.ShowLiveSettings();};buttons.Children.Add(settings);footer.Children.Add(buttons);
            var privacy=Ui.Text("连接成功后才打开麦克风；结束通话会关闭收音。",11,Ui.Muted);privacy.TextAlignment=TextAlignment.Center;privacy.Margin=new Thickness(0,14,0,0);footer.Children.Add(privacy);Grid.SetRow(footer,2);root.Children.Add(footer);Content=root;
            meter=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(40)};meter.Tick+=delegate{orbit.Level=Math.Max(session.InputLevel,session.OutputLevel);mode.Text=connecting?"正在连接":!session.IsActive?"通话已结束":session.OutputLevel>.025?"正在回应":session.InputLevel>.025?"正在听你说":"正在聆听";};
            session.StateChanged+=Refresh;session.PartialTranscript+=Transcript;Loaded+=async delegate{meter.Start();await ConnectAsync();};Closing+=ClosingCall;
            Closed+=delegate{closed=true;meter.Stop();session.StateChanged-=Refresh;session.PartialTranscript-=Transcript;if(returnToChat)pet.ShowChat();};
        }
        public async System.Threading.Tasks.Task ConnectAsync()
        {
            if(closed || stopping || connecting || session.IsActive)return;connecting=true;Refresh();
            try{await pet.StartLiveAsync();}catch(Exception e){status.Text=e is ApiException || e is InvalidOperationException?e.Message:"连接未完成，请检查 AI 配置和网络。";}
            finally{connecting=false;if(!closed){connect.Visibility=session.IsActive?Visibility.Collapsed:Visibility.Visible;connect.IsEnabled=true;}}
        }
        void Refresh(){if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(Refresh));return;}status.Text=session.Status;connect.Visibility=session.IsActive || connecting?Visibility.Collapsed:Visibility.Visible;connect.IsEnabled=!connecting;}
        void Transcript(string role,string text){if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(()=>Transcript(role,text)));return;}subtitle.Text=(role=="user"?"你：":pet.Skin.Name+"：")+text;}
        async void ClosingCall(object sender,CancelEventArgs args)
        {
            if(stopCompleted || closed)return;args.Cancel=true;if(stopping)return;stopping=true;IsEnabled=false;
            try{await session.StopAsync();}finally{stopCompleted=true;if(!closed){DispatcherOperation pending=Dispatcher.BeginInvoke(new Action(delegate{if(!closed)Close();}));}}
        }
    }
    public sealed class VoiceOrbit : FrameworkElement
    {
        static readonly Pen outline=new Pen(Ui.Line,1),blue=new Pen(Ui.Blue,2.5),mint=new Pen(Ui.Brush("#6AAFA4"),2.5);
        double level;
        public double Level {get{return level;}set{level=level*.55+Math.Max(0,Math.Min(1,value))*.45;InvalidateVisual();}}
        protected override void OnRender(DrawingContext drawing)
        {
            Point center=new Point(ActualWidth/2,ActualHeight/2);double radius=Math.Min(ActualWidth,ActualHeight)*.30;
            drawing.DrawEllipse(Ui.Pale,null,center,radius,radius);drawing.DrawEllipse(null,outline,center,radius+13,radius+13);
            for(int i=0;i<64;i++){double angle=i*Math.PI/32;double envelope=.35+.65*Math.Abs(Math.Sin(angle*3+level*5));double length=3+level*32*envelope;
                drawing.DrawLine(i<32?blue:mint,new Point(center.X+Math.Cos(angle)*(radius+24),center.Y+Math.Sin(angle)*(radius+24)),new Point(center.X+Math.Cos(angle)*(radius+24+length),center.Y+Math.Sin(angle)*(radius+24+length)));}
        }
    }
}
