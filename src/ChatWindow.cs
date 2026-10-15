using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.IO;
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
        public bool HasDraft { get { return !String.IsNullOrWhiteSpace(input.Text); } }
        private readonly TextBlock status;
        private readonly Image avatar;
        private readonly Button send,record,stop,clear,liveButton;
        private readonly DispatcherTimer recordTimer;
        private readonly Stopwatch recording=new Stopwatch();
        private bool closed,showingWelcome,ownsOperation,reloading;
        private int visibleTurns=50;
        private readonly Button earlier,export;
        private readonly Dictionary<string,TextBox> rendered=new Dictionary<string,TextBox>();
        private readonly Dictionary<string,TextBlock> statusLabels=new Dictionary<string,TextBlock>();
        private readonly Dictionary<string,ChatMessage> displayed=new Dictionary<string,ChatMessage>();
        private readonly MessageUpdatePump messagePump;
        public ChatWindow(PetWindow companion)
        {
            pet=companion;session=pet.Conversation;Ui.Window(this,"桌面伙伴 · 对话",700,800);MinWidth=520;MinHeight=560;
            var root=new Grid {Margin=new Thickness(22,18,22,18)};
            root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            var header=new DockPanel {Margin=new Thickness(0,0,0,16)};
            var settings=UiIcons.Button("settings","AI 配置");settings.Margin=new Thickness(10,0,0,0);settings.VerticalAlignment=VerticalAlignment.Center;
            settings.Click+=delegate {pet.ShowSettings(true);};DockPanel.SetDock(settings,Dock.Right);header.Children.Add(settings);
            avatar=new Image {Name="ChatAvatar",Width=34,Height=40,Stretch=Stretch.Uniform,VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)};RenderOptions.SetBitmapScalingMode(avatar,BitmapScalingMode.HighQuality);DockPanel.SetDock(avatar,Dock.Left);header.Children.Add(avatar);
            var identity=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};identity.Children.Add(UiIcons.Speech(pet));header.Children.Add(identity);root.Children.Add(header);
            messages=new StackPanel {Margin=new Thickness(2,8,2,12)};scroll=Ui.Scroll(messages);scroll.Name="ChatHistoryScroll";
            var messageArea=new Grid();messageArea.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});messageArea.RowDefinitions.Add(new RowDefinition());
            var historyTools=new DockPanel{LastChildFill=false};
            earlier=Ui.Button("加载更早记录");earlier.Name="HistoryEarlier";earlier.FontSize=11;earlier.Click+=delegate{double offset=scroll.VerticalOffset,extent=scroll.ExtentHeight;visibleTurns+=50;ReloadHistory(false);Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(delegate{scroll.UpdateLayout();scroll.ScrollToVerticalOffset(offset+Math.Max(0,scroll.ExtentHeight-extent));}));};historyTools.Children.Add(earlier);
            export=Ui.Button("导出记录");export.Name="ExportChat";export.FontSize=11;export.Click+=delegate{Export();};DockPanel.SetDock(export,Dock.Right);historyTools.Children.Add(export);messageArea.Children.Add(historyTools);
            Grid.SetRow(scroll,1);messageArea.Children.Add(scroll);Grid.SetRow(messageArea,1);root.Children.Add(messageArea);
            status=Ui.Text("",11,Ui.Muted);status.Name="ChatStatus";
            input=Ui.Field("",true);input.Name="MessageInput";input.MaxLength=8000;input.ToolTip="Enter 发送，Shift + Enter 换行";input.PreviewKeyDown+=delegate(object sender,KeyEventArgs e) {if(e.Key==Key.Enter && (Keyboard.Modifiers&ModifierKeys.Shift)==0){e.Handled=true;if(!pet.DialogueBusy)SendText();}};
            var controls=new StackPanel {Orientation=Orientation.Horizontal};
            liveButton=UiIcons.Button("live","开始 Live 通话");liveButton.Name="StartLiveCall";liveButton.Margin=new Thickness(0);liveButton.Click+=delegate{pet.ShowLiveCall(this);};
            var leading=new StackPanel{Orientation=Orientation.Horizontal};liveButton.Margin=new Thickness(0,0,6,0);leading.Children.Add(liveButton);
            record=UiIcons.Button("mic","语音输入");record.Margin=new Thickness(0);record.Name="VoiceRecord";record.Click+=delegate {if(session.Stage==ConversationStage.Recording)FinishRecording();else StartRecording();};leading.Children.Add(record);
            send=ChatUi.Action("send","发送消息");send.Name="SendMessage";send.Click+=delegate {SendText();};controls.Children.Add(send);
            stop=ChatUi.Action("stop","停止回复");stop.Name="StopConversation";stop.Margin=new Thickness(6,0,0,0);stop.Click+=async delegate {try{await pet.StopDialogueAsync();}catch(Exception e){ShowError(e);}};controls.Children.Add(stop);

            clear=UiIcons.Button("trash","清空当前聊天记录");clear.Name="ClearChat";clear.Margin=new Thickness(6,0,0,0);clear.Click+=delegate {try{session.Clear();ShowStatus("已清空当前会话。");}catch(Exception e){ShowError(e);}};DockPanel.SetDock(clear,Dock.Right);historyTools.Children.Add(clear);
            var composerSurface=ChatUi.Composer(input,leading,controls,status,false);composerSurface.Margin=new Thickness(0,12,0,0);Grid.SetRow(composerSurface,2);root.Children.Add(composerSurface);Content=root;
            recordTimer=new DispatcherTimer {Interval=TimeSpan.FromMilliseconds(200)};recordTimer.Tick+=delegate {if(session.Stage!=ConversationStage.Recording){recordTimer.Stop();return;}ShowStatus("正在录音 · "+(int)recording.Elapsed.TotalSeconds+" 秒 · 再次点击结束并发送");if(recording.Elapsed.TotalSeconds>=60)FinishRecording();};
            messagePump=new MessageUpdatePump(session,UpdateMessage);ReloadHistory();session.MessageAdded+=AddMessage;session.HistoryChanged+=HistoryChanged;session.StateChanged+=RefreshState;pet.Live.StateChanged+=RefreshState;RefreshAppearance();RefreshState();
            Closed+=async delegate {closed=true;recordTimer.Stop();messagePump.Dispose();session.MessageAdded-=AddMessage;session.HistoryChanged-=HistoryChanged;session.StateChanged-=RefreshState;pet.Live.StateChanged-=RefreshState;try{if(ownsOperation)await session.StopAsync();}catch(OperationCanceledException){}catch(ObjectDisposedException){}catch(InvalidOperationException){}};
            Loaded+=delegate {input.Focus();};
        }
        public void RefreshAppearance()
        {if(!(Content is LiveCallView))Title=pet.Skin.Name+" · 对话";avatar.Source=SkinCatalog.GetPreview(pet.Skin);avatar.ToolTip=pet.Skin.Name;}
        public void ReloadHistory(bool scrollToBottom=true)
        {
            reloading=true;messages.Children.Clear();rendered.Clear();statusLabels.Clear();displayed.Clear();showingWelcome=false;
            var turns=session.History.Messages.GroupBy(m=>m.TurnId).ToList();
            foreach(var turn in turns.Skip(Math.Max(0,turns.Count-visibleTurns)))foreach(var message in turn)AddMessage(message);
            earlier.IsEnabled=turns.Count>visibleTurns;export.IsEnabled=turns.Count>0;
            if(messages.Children.Count==0)Welcome();reloading=false;if(scrollToBottom)Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate{if(!closed)scroll.ScrollToEnd();}));
        }
        private void HistoryChanged(){if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(HistoryChanged));return;}visibleTurns=50;ReloadHistory();}
        private void Export()
        {
            var dialog=new Microsoft.Win32.SaveFileDialog{Filter="Markdown 记录 (*.md)|*.md",FileName="DeskPet-chat-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".md"};
            if(dialog.ShowDialog(this)!=true)return;
            try{File.WriteAllText(dialog.FileName,session.ExportHistory(),new System.Text.UTF8Encoding(false));ShowStatus("当前伙伴记录已导出。");}
            catch(Exception e){if(!(e is IOException || e is UnauthorizedAccessException || e is System.Security.SecurityException))throw;ShowStatus("导出失败，请选择可写入的位置。");}
        }
        private void UpdateMessage(ChatMessage message)
        {
            if(closed || !rendered.ContainsKey(message.Id))return;
            bool follow=scroll.ScrollableHeight-scroll.VerticalOffset<32;
            displayed[message.Id]=message;rendered[message.Id].Text=message.Content;statusLabels[message.Id].Text=MessagePresentation.Status(message.Status);
            if(follow)Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate{if(!closed)scroll.ScrollToEnd();}));
        }
        private void Welcome()
        {showingWelcome=true;var panel=new StackPanel {Margin=new Thickness(26,28,26,26)};var character=new Image{Source=SkinCatalog.GetPreview(pet.Skin),Width=154,Height=164,Stretch=Stretch.Uniform,HorizontalAlignment=HorizontalAlignment.Left};RenderOptions.SetBitmapScalingMode(character,BitmapScalingMode.HighQuality);panel.Children.Add(character);var heading=Ui.Text("一起聊聊今天的想法",24);heading.FontWeight=FontWeights.SemiBold;panel.Children.Add(heading);panel.Children.Add(Ui.Text("聊代码、设计，或刚想到的小点子。\n每位伙伴拥有各自的人格与会话。",13,Ui.Muted));var starters=new WrapPanel{Margin=new Thickness(0,18,0,0)};foreach(string idea in new[]{"帮我梳理一个想法","一起检查这段代码"}){string value=idea;var button=Ui.Button(idea);button.Margin=new Thickness(0,0,8,8);button.Click+=delegate {input.Text=value;input.Focus();};starters.Children.Add(button);}panel.Children.Add(starters);messages.Children.Add(panel);}
        private void AddMessage(ChatMessage message)
        {
            if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(delegate {AddMessage(message);}));return;}
            if(!session.History.Messages.Any(m=>m.Id==message.Id))return;if(rendered.ContainsKey(message.Id)){UpdateMessage(message);return;}bool follow=scroll.ScrollableHeight-scroll.VerticalOffset<32;
            if(showingWelcome){messages.Children.Clear();showingWelcome=false;}bool user=message.Role=="user";if(!reloading && !follow && user)visibleTurns=Math.Min(200,visibleTurns+1);displayed[message.Id]=message;
            var panel=new StackPanel();var heading=new DockPanel();if(!user){var copy=Ui.Button("复制");copy.MinHeight=23;copy.Padding=new Thickness(7,0,7,0);copy.Margin=new Thickness(0);copy.FontSize=10;copy.Click+=delegate {try{Clipboard.SetText(displayed[message.Id].Content);}catch(System.Runtime.InteropServices.COMException){ShowStatus("剪贴板正忙，请再试一次。");}};DockPanel.SetDock(copy,Dock.Right);heading.Children.Add(copy);}heading.Children.Add(Ui.Text(user?"你":pet.Skin.Name,10,user?Ui.Pale:Ui.Muted));panel.Children.Add(heading);
            var text=new TextBox {Text=message.Content,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,BorderThickness=new Thickness(0),Background=Brushes.Transparent,Foreground=user?Brushes.White:Ui.Ink,Padding=new Thickness(0,5,0,0),FontSize=14,MaxHeight=420,VerticalScrollBarVisibility=ScrollBarVisibility.Auto};panel.Children.Add(text);rendered[message.Id]=text;
            var state=Ui.Text(MessagePresentation.Status(message.Status),10,Ui.Muted);statusLabels[message.Id]=state;if(!user)panel.Children.Add(state);
            var bubble=new Border {Child=panel,Background=user?Ui.Blue:Brushes.White,BorderBrush=user?Ui.Blue:Ui.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(14),Padding=new Thickness(14,10,14,12),Margin=user?new Thickness(52,0,0,16):new Thickness(0,0,24,16),Tag=message.Id};messages.Children.Add(bubble);
            while(messages.Children.Count>visibleTurns*2){var old=messages.Children[0] as Border;if(old!=null){string id=old.Tag as string;if(id!=null){rendered.Remove(id);statusLabels.Remove(id);displayed.Remove(id);}}messages.Children.RemoveAt(0);}
            if(!reloading){earlier.IsEnabled=session.History.Messages.Select(m=>m.TurnId).Distinct().Count()>visibleTurns;export.IsEnabled=true;if(follow)Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate{if(!closed)scroll.ScrollToEnd();}));}
        }
        private void RefreshState()
        {
            if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(RefreshState));return;}
            bool busy=pet.DialogueBusy;liveButton.IsEnabled=!busy || pet.LiveActive;send.IsEnabled=!busy;clear.IsEnabled=!busy;stop.IsEnabled=busy;record.IsEnabled=!busy || session.Stage==ConversationStage.Recording;RefreshAppearance();
            string recordLabel=session.Stage==ConversationStage.Recording?"结束并发送":"语音输入";record.Content=UiIcons.Image(session.Stage==ConversationStage.Recording?"stop":"mic",Ui.Ink);record.ToolTip=recordLabel;System.Windows.Automation.AutomationProperties.SetName(record,recordLabel);
            if(pet.LiveActive){recordTimer.Stop();ShowStatus(pet.Live.Status);return;}

            if(session.Stage==ConversationStage.Recording){if(ownsOperation && !recordTimer.IsEnabled){recording.Restart();recordTimer.Start();}ShowStatus("正在录音 · 再次点击结束并发送");}
            else
            {recordTimer.Stop();ShowStatus(session.Stage==ConversationStage.Recognizing?"正在识别语音…":session.Stage==ConversationStage.Thinking?"正在思考…":session.Stage==ConversationStage.Speaking?"正在朗读 · 可以随时停止":String.IsNullOrEmpty(session.HistoryWarning)?(String.IsNullOrEmpty(session.LastNotice)?"Enter 发送 · Shift + Enter 换行":session.LastNotice):session.HistoryWarning);}
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
        private void ShowStatus(string text){if(!closed)status.Text=text;}
        private void ShowError(Exception error)
        {if(error is OperationCanceledException)ShowStatus("已停止。");else if(error is ApiException || error is InvalidOperationException)ShowStatus(error.Message);else ShowStatus("操作未完成，请检查连接设置、麦克风或音频输出。当前文字回复会保留。");}
    }
}
