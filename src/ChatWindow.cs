using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace Dafeiyu
{
    public sealed class ChatWindow : Window
    {
        private readonly PetWindow pet;
        private readonly ConversationSession session;
        private readonly StackPanel messages;
        private readonly ScrollViewer scroll;
        private readonly TextBox input;
        private readonly TextBlock status,title,service,liveCaption;
        private readonly Image avatar;
        private readonly Button send,record,stop,clear;
        private readonly ComboBox voiceMode;
        private readonly DispatcherTimer recordTimer;
        private readonly Stopwatch recording=new Stopwatch();
        private bool closed,showingWelcome,ownsOperation,ownsLive,refreshing;
        public ChatWindow(PetWindow companion)
        {
            pet=companion;session=pet.Conversation;Ui.Window(this,"桌面伙伴 · 对话",660,780);MinWidth=560;
            var root=new Grid {Margin=new Thickness(22,18,22,18)};
            root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            var header=new DockPanel {Margin=new Thickness(0,0,0,16)};
            var settings=Ui.Button("连接设置");settings.Margin=new Thickness(10,0,0,0);settings.VerticalAlignment=VerticalAlignment.Center;
            settings.Click+=delegate {pet.ShowSettings(true);};DockPanel.SetDock(settings,Dock.Right);header.Children.Add(settings);
            avatar=new Image {Width=62,Height=72,Stretch=Stretch.Uniform,Margin=new Thickness(0,0,16,0)};RenderOptions.SetBitmapScalingMode(avatar,BitmapScalingMode.HighQuality);DockPanel.SetDock(avatar,Dock.Left);header.Children.Add(avatar);
            var introduction=new StackPanel {VerticalAlignment=VerticalAlignment.Center};title=Ui.Text("",23);title.FontWeight=FontWeights.SemiBold;service=Ui.Text("",11,Ui.Muted);introduction.Children.Add(title);introduction.Children.Add(service);header.Children.Add(introduction);root.Children.Add(header);
            messages=new StackPanel {Margin=new Thickness(2,8,2,12)};scroll=Ui.Scroll(messages);Grid.SetRow(scroll,1);root.Children.Add(scroll);
            var composer=new StackPanel();
            liveCaption=Ui.Text("",12,Ui.Blue);liveCaption.Name="LiveCaption";liveCaption.MaxHeight=60;composer.Children.Add(liveCaption);
            status=Ui.Text("",12,Ui.Muted);status.Name="ChatStatus";status.Margin=new Thickness(0,0,0,8);composer.Children.Add(status);
            input=Ui.Field("",true);input.Name="MessageInput";input.MinHeight=76;input.MaxHeight=140;input.MaxLength=8000;input.ToolTip="Enter 发送，Shift + Enter 换行";input.PreviewKeyDown+=delegate(object sender,KeyEventArgs e) {if(e.Key==Key.Enter && (Keyboard.Modifiers&ModifierKeys.Shift)==0){e.Handled=true;if(!pet.DialogueBusy)SendText();}};composer.Children.Add(input);
            var controls=new DockPanel {Margin=new Thickness(0,10,0,0),LastChildFill=false};
            var tts=Ui.ReplySpeechToggle(pet);tts.Margin=new Thickness(12,3,0,0);
            send=Ui.Button("发送",true);send.Name="SendMessage";send.Margin=new Thickness(8,0,0,0);send.Click+=delegate {SendText();};DockPanel.SetDock(send,Dock.Right);controls.Children.Add(send);
            clear=Ui.Button("清空");clear.Name="ClearChat";clear.Click+=delegate {try{session.Clear();messages.Children.Clear();Welcome();ShowStatus("已清空当前会话。");}catch(Exception e){ShowError(e);}};DockPanel.SetDock(clear,Dock.Right);controls.Children.Add(clear);
            record=Ui.Button("点击说话");record.Name="VoiceRecord";record.Click+=delegate {if(pet.LiveActive || pet.Settings.VoiceMode=="live")ToggleLive();else if(session.Stage==ConversationStage.Recording)FinishRecording();else StartRecording();};DockPanel.SetDock(record,Dock.Left);controls.Children.Add(record);
            stop=Ui.Button("停止");stop.Name="StopConversation";stop.Click+=async delegate {try{await pet.StopDialogueAsync();}catch(Exception e){ShowError(e);}};DockPanel.SetDock(stop,Dock.Left);controls.Children.Add(stop);composer.Children.Add(controls);
            voiceMode=new ComboBox{Name="ChatVoiceMode",ItemsSource=new[]{"ASR + TTS","Live 实时语音"},SelectedIndex=pet.Settings.VoiceMode=="live"?1:0,MinHeight=30,Width=155,Padding=new Thickness(6),Margin=new Thickness(0,10,0,6),HorizontalAlignment=HorizontalAlignment.Left};voiceMode.SelectionChanged+=delegate {if(refreshing)return;if(pet.DialogueBusy){RefreshAppearance();ShowStatus("请先结束当前对话，再切换语音模式。");return;}pet.Settings.VoiceMode=voiceMode.SelectedIndex==1?"live":"pipeline";pet.SaveSettings();RefreshState();};var voiceBar=new DockPanel{Margin=new Thickness(0,10,0,6),LastChildFill=false};DockPanel.SetDock(tts,Dock.Right);voiceBar.Children.Add(tts);voiceMode.Margin=new Thickness(0);DockPanel.SetDock(voiceMode,Dock.Left);voiceBar.Children.Add(voiceMode);composer.Children.Add(voiceBar);
            composer.Children.Add(Ui.Text("Enter 发送 · Shift + Enter 换行",10,Ui.Muted));var composerSurface=new Border{Child=composer,Padding=new Thickness(16,12,16,12),Margin=new Thickness(0,12,0,0),CornerRadius=new CornerRadius(18),Background=Brushes.White,BorderBrush=Ui.Line,BorderThickness=new Thickness(1)};Grid.SetRow(composerSurface,2);root.Children.Add(composerSurface);Content=root;
            recordTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(200)};recordTimer.Tick+=delegate {if(session.Stage!=ConversationStage.Recording){recordTimer.Stop();return;}ShowStatus("正在录音 · "+(int)recording.Elapsed.TotalSeconds+" 秒 · 再次点击结束并发送");if(recording.Elapsed.TotalSeconds>=60)FinishRecording();};
            ReloadHistory();session.MessageAdded+=AddMessage;session.StateChanged+=RefreshState;pet.Live.StateChanged+=RefreshState;pet.Live.PartialTranscript+=PartialTranscript;RefreshAppearance();RefreshState();
            Closed+=async delegate {closed=true;recordTimer.Stop();session.MessageAdded-=AddMessage;session.StateChanged-=RefreshState;pet.Live.StateChanged-=RefreshState;pet.Live.PartialTranscript-=PartialTranscript;try{if(ownsLive)await pet.Live.StopAsync();if(ownsOperation)await session.StopAsync();}catch(OperationCanceledException){}catch(ObjectDisposedException){}catch(InvalidOperationException){}};
            Loaded+=delegate {input.Focus();};
        }
        public void RefreshAppearance()
        {if(!pet.LiveActive)liveCaption.Text="";title.Text=pet.Skin.Name;Title=pet.Skin.Name+" · 对话";avatar.Source=SkinCatalog.GetPreview(pet.Skin);service.Text=String.IsNullOrWhiteSpace(pet.Settings.Text.Model)?pet.Skin.Description+" · 填写连接设置开始聊天":pet.Skin.Description+"\n"+pet.Settings.Text.Model;if(voiceMode!=null){refreshing=true;voiceMode.SelectedIndex=pet.Settings.VoiceMode=="live"?1:0;refreshing=false;}}
        public void ReloadHistory(){messages.Children.Clear();showingWelcome=false;foreach(ChatMessage message in session.History.Messages)AddMessage(message);if(messages.Children.Count==0)Welcome();}
        private void Welcome()
        {showingWelcome=true;var panel=new StackPanel {Margin=new Thickness(26,28,26,26)};var character=new Image{Source=SkinCatalog.GetPreview(pet.Skin),Width=154,Height=164,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left};RenderOptions.SetBitmapScalingMode(character,BitmapScalingMode.HighQuality);panel.Children.Add(character);var heading=Ui.Text("一起聊聊今天的想法",24);heading.FontWeight=FontWeights.SemiBold;panel.Children.Add(heading);panel.Children.Add(Ui.Text("聊代码、设计，或刚想到的小点子。\n每位伙伴拥有各自的人格与会话。",13,Ui.Muted));var starters=new WrapPanel{Margin=new Thickness(0,18,0,0)};foreach(string idea in new[]{"帮我梳理一个想法","一起检查这段代码"}){string value=idea;var button=Ui.Button(idea);button.Margin=new Thickness(0,0,8,8);button.Click+=delegate {input.Text=value;input.Focus();};starters.Children.Add(button);}panel.Children.Add(starters);messages.Children.Add(panel);}
        private void AddMessage(ChatMessage message)
        {
            if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(delegate {AddMessage(message);}));return;}
            if(showingWelcome){messages.Children.Clear();showingWelcome=false;}bool user=message.Role=="user";
            var panel=new StackPanel();var heading=new DockPanel();if(!user){var copy=Ui.Button("复制");copy.MinHeight=23;copy.Padding=new Thickness(7,0,7,0);copy.Margin=new Thickness(0);copy.FontSize=10;copy.Click+=delegate {try{Clipboard.SetText(message.Content);}catch(System.Runtime.InteropServices.COMException){ShowStatus("剪贴板正忙，请再试一次。");}};DockPanel.SetDock(copy,Dock.Right);heading.Children.Add(copy);}heading.Children.Add(Ui.Text(user?"你":pet.Skin.Name,10,user?Ui.Pale:Ui.Muted));panel.Children.Add(heading);
            var text=new TextBox {Text=message.Content,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,BorderThickness=new Thickness(0),Background=Brushes.Transparent,Foreground=user?Brushes.White:Ui.Ink,Padding=new Thickness(0,5,0,0),FontSize=14,MaxHeight=420,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};panel.Children.Add(text);
            var bubble=new Border {Child=panel,Background=user?Ui.Blue:Brushes.White,BorderBrush=user?Ui.Blue:Ui.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Padding=new Thickness(14,10,14,12),Margin=user?new Thickness(52,0,0,16):new Thickness(0,0,24,16)};messages.Children.Add(bubble);
            while(messages.Children.Count>40)messages.Children.RemoveAt(0);Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(scroll.ScrollToEnd));
        }
        private void RefreshState()
        {
            if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(RefreshState));return;}
            bool busy=pet.DialogueBusy;send.IsEnabled=!busy;clear.IsEnabled=!busy;stop.IsEnabled=busy;record.IsEnabled=!busy || session.Stage==ConversationStage.Recording || pet.LiveActive;voiceMode.IsEnabled=!busy;RefreshAppearance();
            record.Content=pet.LiveActive?"结束 Live":pet.Settings.VoiceMode=="live"?"开始 Live":session.Stage==ConversationStage.Recording?"结束并发送":"点击说话";
            if(pet.LiveActive){recordTimer.Stop();ShowStatus(pet.Live.Status);return;}
            if(pet.Settings.VoiceMode=="live" && session.Stage==ConversationStage.Idle){recordTimer.Stop();ShowStatus(pet.Live.Status);return;}
            if(session.Stage==ConversationStage.Recording){if(!recordTimer.IsEnabled){recording.Restart();recordTimer.Start();}ShowStatus("正在录音 · 再次点击结束并发送");}
            else
            {recordTimer.Stop();ShowStatus(session.Stage==ConversationStage.Recognizing?"正在识别语音…":session.Stage==ConversationStage.Thinking?"正在思考…":session.Stage==ConversationStage.Speaking?"正在朗读 · 可以随时停止":String.IsNullOrEmpty(session.LastNotice)?"准备好了。":session.LastNotice);}
        }
        private async void SendText()
        {
            if(pet.DialogueBusy)return;
            string text=input.Text.Trim();if(text.Length==0){ShowStatus("先写下一句话吧。");return;}input.Clear();
            try {ownsOperation=true;await session.SendTextAsync(text,pet.Settings,pet.Skin.Name);}
            catch(Exception e){if(!closed && input.Text.Length==0)input.Text=text;ShowError(e);}
            finally{ownsOperation=false;}
        }
        private void StartRecording(){if(pet.DialogueBusy){ShowStatus("请先结束当前对话。");return;}try{ownsOperation=true;session.StartRecording(pet.Settings);}catch(Exception e){ownsOperation=false;ShowError(e);}}
        private async void FinishRecording(){recordTimer.Stop();try{await session.FinishRecordingAsync(pet.Skin.Name);}catch(Exception e){ShowError(e);}finally{ownsOperation=false;}}
        private async void ToggleLive(){try{if(pet.LiveActive){await pet.Live.StopAsync();ownsLive=false;}else{ownsLive=true;await pet.StartLiveAsync();}}catch(Exception e){ownsLive=false;ShowError(e);}}
        private void PartialTranscript(string role,string text){if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(()=>PartialTranscript(role,text)));return;}if(!pet.LiveActive)return;liveCaption.Text=(role=="user"?"你：":pet.Skin.Name+"：")+text;}
        private void ShowStatus(string text){if(!closed)status.Text=text;}
        private void ShowError(Exception error)
        {if(error is OperationCanceledException)ShowStatus("已停止。");else if(error is ApiException || error is InvalidOperationException)ShowStatus(error.Message);else ShowStatus("操作未完成，请检查连接设置、麦克风或音频输出。当前文字回复会保留。");}
    }
}
