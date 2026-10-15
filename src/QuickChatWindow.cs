using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shapes;
namespace Dafeiyu
{
    public sealed class QuickChatWindow : Window
    {
        readonly PetWindow pet;readonly ConversationSession session;readonly TextBox input,reply;readonly TextBlock status;readonly Button send,stop,liveButton,record;readonly Polygon tail;readonly Image avatar;
        bool closed,ownsVoice;readonly MessageUpdatePump messagePump;
        readonly System.Windows.Threading.DispatcherTimer recordTimer=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(200)};
        readonly System.Diagnostics.Stopwatch recording=new System.Diagnostics.Stopwatch();
        public QuickChatWindow(PetWindow companion)
        {
            pet=companion;session=pet.Conversation;Icon=Ui.ApplicationIcon();Width=400;Height=390;AllowsTransparency=true;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=pet.Settings.Topmost;Background=Brushes.Transparent;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=12;Foreground=Ui.Ink;UseLayoutRounding=true;
            var root=new Grid();root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(14)});
            var panel=new Grid{Margin=new Thickness(18,14,18,12)};
            panel.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});panel.RowDefinitions.Add(new RowDefinition());panel.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var header=new DockPanel();
            var close=UiIcons.Button("close","关闭快速对话");close.Name="CloseQuickChat";close.Margin=new Thickness(0);close.Click+=delegate {Hide();};DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);
            var expand=UiIcons.Button("expand","展开完整对话");expand.Name="ExpandChat";expand.Click+=delegate {pet.ShowChat();};DockPanel.SetDock(expand,Dock.Right);header.Children.Add(expand);
            avatar=new Image{Source=SkinCatalog.GetPreview(pet.Skin),Width=34,Height=40,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)};avatar.Name="QuickAvatar";RenderOptions.SetBitmapScalingMode(avatar,BitmapScalingMode.HighQuality);DockPanel.SetDock(avatar,Dock.Left);header.Children.Add(avatar);
            var identity=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};identity.Children.Add(UiIcons.Speech(pet));header.Children.Add(identity);panel.Children.Add(header);
            reply=new TextBox{Name="QuickReply",Text="写下一句话，我在这里。",IsReadOnly=true,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,BorderThickness=new Thickness(0),Background=Brushes.Transparent,Foreground=Ui.Ink,Padding=new Thickness(3,14,3,12),FontSize=14};Grid.SetRow(reply,1);panel.Children.Add(reply);
            status=Ui.Text("",11,Ui.Muted);status.Name="QuickStatus";
            input=Ui.Field("",true);input.Name="QuickMessageInput";input.MaxLength=8000;input.ToolTip="Enter 发送，Shift + Enter 换行";input.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Enter && (Keyboard.Modifiers&ModifierKeys.Shift)==0){e.Handled=true;Send();}else if(e.Key==Key.Escape){e.Handled=true;Hide();}};
            var controls=new StackPanel{Orientation=Orientation.Horizontal};
            send=ChatUi.Action("send","发送消息");send.Name="SendQuickMessage";send.Click+=delegate {Send();};controls.Children.Add(send);
            stop=ChatUi.Action("stop","停止回复");stop.Name="StopQuickConversation";stop.Margin=new Thickness(6,0,0,0);stop.Click+=async delegate {try{await pet.StopDialogueAsync();}catch(Exception e){Error(e);}};controls.Children.Add(stop);
            liveButton=UiIcons.Button("live","开始 Live 通话");liveButton.Name="StartLiveCall";liveButton.Margin=new Thickness(0);liveButton.Click+=delegate{pet.ShowLiveCall(this);};
            var leading=new StackPanel{Orientation=Orientation.Horizontal};liveButton.Margin=new Thickness(0,0,6,0);leading.Children.Add(liveButton);
            record=UiIcons.Button("mic","语音输入");record.Name="VoiceRecord";record.Margin=new Thickness(0);record.Click+=delegate{if(session.Stage==ConversationStage.Recording)FinishRecording();else StartRecording();};leading.Children.Add(record);
            recordTimer.Tick+=delegate{if(session.Stage!=ConversationStage.Recording){recordTimer.Stop();return;}status.Text="正在录音 · "+(int)recording.Elapsed.TotalSeconds+" 秒 · 再次点击结束并发送";if(recording.Elapsed.TotalSeconds>=60)FinishRecording();};
            var composer=ChatUi.Composer(input,leading,controls,status,true);Grid.SetRow(composer,2);panel.Children.Add(composer);
            root.Children.Add(new Border{Child=panel,CornerRadius=new CornerRadius(20),BorderThickness=new Thickness(1),BorderBrush=Ui.Line,Background=Ui.Brush("#F7F9FD")});tail=new Polygon{Fill=Ui.Brush("#F7F9FD"),Stroke=Ui.Line,StrokeThickness=1,Points=new PointCollection(new[]{new Point(168,0),new Point(180,13),new Point(192,0)})};Grid.SetRow(tail,1);root.Children.Add(tail);Content=root;
            messagePump=new MessageUpdatePump(session,Message);session.MessageAdded+=Message;session.HistoryChanged+=HistoryChanged;session.StateChanged+=RefreshState;pet.Live.StateChanged+=RefreshState;pet.Live.PartialTranscript+=Partial;Loaded+=delegate {FollowPet();FocusInput();};IsVisibleChanged+=async delegate {if(IsVisible)RefreshState();else if(ownsVoice)await CancelVoiceAsync();};Closed+=async delegate {closed=true;recordTimer.Stop();messagePump.Dispose();session.MessageAdded-=Message;session.HistoryChanged-=HistoryChanged;session.StateChanged-=RefreshState;pet.Live.StateChanged-=RefreshState;pet.Live.PartialTranscript-=Partial;if(ownsVoice)await CancelVoiceAsync();};RefreshPersona();RefreshState();
        }
        public bool HasDraft { get { return !String.IsNullOrWhiteSpace(input.Text); } }
        public void FocusInput(){if(input.IsVisible)input.Focus();}
        public void FollowPet(){if(!closed)BubblePlacement.Follow(this,pet,tail);}
        public void RefreshPersona(){avatar.Source=SkinCatalog.GetPreview(pet.Skin);avatar.ToolTip=pet.Skin.Name;if(!(Content is LiveCallView))Title=pet.Skin.Name+" · 快速对话";var last=session.History.Messages.LastOrDefault(m=>m.Role=="assistant");reply.Text=MessagePresentation.QuickText(last);RefreshState();}
        void Message(ChatMessage message){if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(()=>Message(message)));return;}if(message.Role=="assistant" && session.History.Messages.LastOrDefault(m=>m.Role=="assistant")!=null && session.History.Messages.Last(m=>m.Role=="assistant").Id==message.Id)reply.Text=MessagePresentation.QuickText(message);}
        void HistoryChanged(){if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(HistoryChanged));return;}RefreshPersona();}
        void Partial(string role,string text){if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(()=>Partial(role,text)));return;}if(pet.LiveActive && role=="assistant")reply.Text=text;}
        void RefreshState()
        {
            if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(RefreshState));return;}
            record.IsEnabled=!pet.DialogueBusy || session.Stage==ConversationStage.Recording;string label=session.Stage==ConversationStage.Recording?"结束并发送":"语音输入";
            record.Content=UiIcons.Image(session.Stage==ConversationStage.Recording?"stop":"mic",Ui.Ink);record.ToolTip=label;System.Windows.Automation.AutomationProperties.SetName(record,label);
            if(session.Stage!=ConversationStage.Recording)recordTimer.Stop();if(!session.IsBusy)ownsVoice=false;
            liveButton.IsEnabled=!pet.DialogueBusy || pet.LiveActive;send.IsEnabled=!pet.DialogueBusy;stop.IsEnabled=pet.DialogueBusy;
            status.Text=pet.LiveActive?pet.Live.Status:session.Stage==ConversationStage.Thinking?"正在思考…":session.Stage==ConversationStage.Speaking?"正在朗读…":session.Stage==ConversationStage.Recording?"正在录音…":session.Stage==ConversationStage.Recognizing?"正在识别…":String.IsNullOrEmpty(session.HistoryWarning)?(String.IsNullOrEmpty(session.LastNotice)?"Enter 发送 · Shift + Enter 换行":session.LastNotice):session.HistoryWarning;
        }
        void StartRecording()
        {
            if(pet.DialogueBusy)return;
            try{ownsVoice=true;session.StartRecording(pet.Settings);recording.Restart();recordTimer.Start();}
            catch(Exception e){ownsVoice=false;Error(e);}
        }
        async void FinishRecording()
        {
            recordTimer.Stop();if(session.Stage!=ConversationStage.Recording)return;
            try{ownsVoice=true;await session.FinishRecordingAsync(pet.Skin.Name);}catch(Exception e){Error(e);}finally{ownsVoice=false;}
        }
        async System.Threading.Tasks.Task CancelVoiceAsync()
        {recordTimer.Stop();try{await session.StopAsync();}catch(Exception e){Error(e);}finally{ownsVoice=false;}}
        async void Send()
        {
            if(pet.DialogueBusy)return;string text=input.Text.Trim();if(text.Length==0)return;input.Clear();
            try{await session.SendTextAsync(text,pet.Settings,pet.Skin.Name);}catch(Exception e){if(!closed && input.Text.Length==0)input.Text=text;Error(e);}
        }
        void Error(Exception e){if(closed)return;status.Text=e is OperationCanceledException?"已停止。":e is ApiException || e is InvalidOperationException?e.Message:"快速对话未完成，请打开设置检查服务。";}
    }
}
