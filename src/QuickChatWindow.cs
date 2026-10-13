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
        readonly PetWindow pet;readonly ConversationSession session;readonly TextBox input,reply;readonly TextBlock name,status;readonly Button send,stop;readonly Polygon tail;
        bool closed;readonly MessageUpdatePump messagePump;
        public QuickChatWindow(PetWindow companion)
        {
            pet=companion;session=pet.Conversation;Icon=Ui.ApplicationIcon();Width=380;Height=350;AllowsTransparency=true;WindowStyle=WindowStyle.None;ResizeMode=ResizeMode.NoResize;ShowInTaskbar=false;Topmost=pet.Settings.Topmost;Background=Brushes.Transparent;FontFamily=new FontFamily("Microsoft YaHei UI");FontSize=12;Foreground=Ui.Ink;
            var root=new Grid();root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition{Height=new GridLength(14)});
            var panel=new Grid{Margin=new Thickness(18,14,18,14)};panel.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});panel.RowDefinitions.Add(new RowDefinition());panel.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});panel.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});panel.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var header=new DockPanel();var close=Ui.Button("×");close.Name="CloseQuickChat";close.MinHeight=26;close.Padding=new Thickness(8,1,8,1);close.Margin=new Thickness(6,0,0,0);close.Click+=delegate {Hide();};DockPanel.SetDock(close,Dock.Right);header.Children.Add(close);
            var expand=Ui.Button("展开聊天");expand.Name="ExpandChat";expand.MinHeight=26;expand.Padding=new Thickness(8,1,8,1);expand.Margin=new Thickness(0);expand.Click+=delegate {pet.ShowChat();};DockPanel.SetDock(expand,Dock.Right);header.Children.Add(expand);name=Ui.Text("",14,Ui.Blue);name.FontWeight=FontWeights.SemiBold;header.Children.Add(name);panel.Children.Add(header);
            reply=new TextBox{Name="QuickReply",Text="写下一句话，我在这里。",IsReadOnly=true,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,BorderThickness=new Thickness(0),Background=Brushes.Transparent,Foreground=Ui.Ink,Padding=new Thickness(0,8,0,4),FontSize=13};Grid.SetRow(reply,1);panel.Children.Add(reply);
            status=Ui.Text("",10,Ui.Muted);status.Name="QuickStatus";status.Margin=new Thickness(0,4,0,7);Grid.SetRow(status,2);panel.Children.Add(status);
            input=Ui.Field("",true);input.Name="QuickMessageInput";input.Height=54;input.MaxLength=8000;input.ToolTip="Enter 发送，Shift + Enter 换行";input.PreviewKeyDown+=delegate(object sender,KeyEventArgs e){if(e.Key==Key.Enter && (Keyboard.Modifiers&ModifierKeys.Shift)==0){e.Handled=true;Send();}else if(e.Key==Key.Escape){e.Handled=true;Hide();}};Grid.SetRow(input,3);panel.Children.Add(input);
            var controls=new DockPanel{LastChildFill=false,Margin=new Thickness(0,9,0,0)};send=Ui.Button("发送",true);send.Name="SendQuickMessage";send.MinHeight=31;send.Padding=new Thickness(15,4,15,4);send.Margin=new Thickness(0);send.Click+=delegate {Send();};DockPanel.SetDock(send,Dock.Right);controls.Children.Add(send);
            stop=Ui.Button("停止");stop.Name="StopQuickConversation";stop.MinHeight=31;stop.Padding=new Thickness(12,4,12,4);stop.Click+=async delegate {try{await pet.StopDialogueAsync();}catch(Exception e){Error(e);}};DockPanel.SetDock(stop,Dock.Left);controls.Children.Add(stop);
            var tts=Ui.ReplySpeechToggle(pet);tts.Margin=new Thickness(5,6,0,0);DockPanel.SetDock(tts,Dock.Left);controls.Children.Add(tts);Grid.SetRow(controls,4);panel.Children.Add(controls);
            root.Children.Add(new Border{Child=panel,CornerRadius=new CornerRadius(18),BorderThickness=new Thickness(1),BorderBrush=Ui.Line,Background=Brushes.White});tail=new Polygon{Fill=Brushes.White,Stroke=Ui.Line,StrokeThickness=1,Points=new PointCollection(new[]{new Point(168,0),new Point(180,13),new Point(192,0)})};Grid.SetRow(tail,1);root.Children.Add(tail);Content=root;
            messagePump=new MessageUpdatePump(session,Message);session.MessageAdded+=Message;session.HistoryChanged+=HistoryChanged;session.StateChanged+=RefreshState;pet.Live.StateChanged+=RefreshState;pet.Live.PartialTranscript+=Partial;Loaded+=delegate {FollowPet();FocusInput();};IsVisibleChanged+=delegate {if(IsVisible)RefreshState();};Closed+=delegate {closed=true;messagePump.Dispose();session.MessageAdded-=Message;session.HistoryChanged-=HistoryChanged;session.StateChanged-=RefreshState;pet.Live.StateChanged-=RefreshState;pet.Live.PartialTranscript-=Partial;};RefreshPersona();RefreshState();
        }
        public void FocusInput(){input.Focus();}
        public void FollowPet(){if(!closed)BubblePlacement.Follow(this,pet,tail);}
        public void RefreshPersona(){name.Text=pet.Skin.Name;Title=pet.Skin.Name+" · 快速对话";var last=session.History.Messages.LastOrDefault(m=>m.Role=="assistant");reply.Text=MessagePresentation.QuickText(last);RefreshState();}
        void Message(ChatMessage message){if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(()=>Message(message)));return;}if(message.Role=="assistant" && session.History.Messages.LastOrDefault(m=>m.Role=="assistant")!=null && session.History.Messages.Last(m=>m.Role=="assistant").Id==message.Id)reply.Text=MessagePresentation.QuickText(message);}
        void HistoryChanged(){if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(HistoryChanged));return;}RefreshPersona();}
        void Partial(string role,string text){if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(()=>Partial(role,text)));return;}if(pet.LiveActive && role=="assistant")reply.Text=text;}
        void RefreshState()
        {
            if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(RefreshState));return;}
            send.IsEnabled=!pet.DialogueBusy;stop.IsEnabled=pet.DialogueBusy;
            status.Text=pet.LiveActive?pet.Live.Status:session.Stage==ConversationStage.Thinking?"正在思考…":session.Stage==ConversationStage.Speaking?"正在朗读…":session.Stage==ConversationStage.Recording?"正在录音…":session.Stage==ConversationStage.Recognizing?"正在识别…":String.IsNullOrEmpty(session.HistoryWarning)?(String.IsNullOrEmpty(session.LastNotice)?"Enter 发送 · Shift + Enter 换行":session.LastNotice):session.HistoryWarning;
        }
        async void Send()
        {
            if(pet.DialogueBusy)return;string text=input.Text.Trim();if(text.Length==0)return;input.Clear();
            try{await session.SendTextAsync(text,pet.Settings,pet.Skin.Name);}catch(Exception e){if(!closed && input.Text.Length==0)input.Text=text;Error(e);}
        }
        void Error(Exception e){if(closed)return;status.Text=e is OperationCanceledException?"已停止。":e is ApiException || e is InvalidOperationException?e.Message:"快速对话未完成，请打开设置检查服务。";}
    }
}
