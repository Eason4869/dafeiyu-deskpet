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
        public bool HasDraft { get { return !String.IsNullOrWhiteSpace(input.Text) || (composer!=null && composer.HasEdit); } }
        private readonly TextBlock status;
        private readonly Image avatar;
        private readonly Button record,clear;
        private readonly ConversationComposer composer;
        private bool closed,showingWelcome,reloading;
        private int visibleTurns=50;
        private readonly Button earlier,export;
        private readonly Dictionary<string,ChatMessageView> rendered=new Dictionary<string,ChatMessageView>();
        private readonly Dictionary<string,ChatMessage> displayed=new Dictionary<string,ChatMessage>();
        private readonly MessageUpdatePump messagePump;
        private readonly AgentTaskPanel taskPanel;
        readonly Grid workspaceShell,messageArea,conversationSurface;readonly ContentControl inspectorHost;readonly Border inspector;readonly AgentWorkspaceNavigation navigation;
        bool workspaceLayout,inspectorLayout,compactTaskExpanded;readonly double normalMaxHeight;
        public ChatWindow(PetWindow companion)
        {
            pet=companion;session=pet.Conversation;Ui.Window(this,"桌面伙伴 · 对话",700,860);MinWidth=520;MinHeight=560;
            Background=Brushes.White;
            normalMaxHeight=MaxHeight;
            var root=new Grid {Margin=new Thickness(22,18,22,18)};
            root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});root.RowDefinitions.Add(new RowDefinition());root.RowDefinitions.Add(new RowDefinition {Height=GridLength.Auto});
            var header=new DockPanel {Margin=new Thickness(0,0,0,16)};
            var settings=UiIcons.Button("settings","模型配置");settings.Margin=new Thickness(10,0,0,0);settings.VerticalAlignment=VerticalAlignment.Center;
            settings.Click+=delegate {pet.ShowSettings(true);};DockPanel.SetDock(settings,Dock.Right);header.Children.Add(settings);
            avatar=new Image {Name="ChatAvatar",Width=34,Height=34,Stretch=Stretch.Uniform,Clip=new RectangleGeometry(new Rect(0,0,34,34),6.8,6.8),VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(0,0,10,0)};RenderOptions.SetBitmapScalingMode(avatar,BitmapScalingMode.HighQuality);DockPanel.SetDock(avatar,Dock.Left);header.Children.Add(avatar);
            var identity=new StackPanel{Orientation=Orientation.Horizontal,VerticalAlignment=VerticalAlignment.Center};identity.Children.Add(UiIcons.Speech(pet));header.Children.Add(identity);root.Children.Add(header);
            messages=new StackPanel {Margin=new Thickness(2,8,2,12)};scroll=Ui.Scroll(messages);scroll.Name="ChatHistoryScroll";
            conversationSurface=root;messageArea=new Grid();messageArea.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});messageArea.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});messageArea.RowDefinitions.Add(new RowDefinition());
            var historyTools=new DockPanel{LastChildFill=false};
            earlier=Ui.Button("加载更早记录");earlier.Name="HistoryEarlier";earlier.FontSize=11;earlier.Click+=delegate{double offset=scroll.VerticalOffset,extent=scroll.ExtentHeight;visibleTurns+=50;ReloadHistory(false);Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(delegate{scroll.UpdateLayout();scroll.ScrollToVerticalOffset(offset+Math.Max(0,scroll.ExtentHeight-extent));}));};historyTools.Children.Add(earlier);
            export=Ui.Button("导出记录");export.Name="ExportChat";export.FontSize=11;export.Click+=delegate{Export();};DockPanel.SetDock(export,Dock.Right);historyTools.Children.Add(export);messageArea.Children.Add(historyTools);
            taskPanel=new AgentTaskPanel(pet);Grid.SetRow(taskPanel,1);messageArea.Children.Add(taskPanel);
            Grid.SetRow(scroll,2);messageArea.Children.Add(scroll);Grid.SetRow(messageArea,1);root.Children.Add(messageArea);
            status=Ui.Text("",11,Ui.Muted);status.Name="ChatStatus";
            input=Ui.Field("",true);input.Name="MessageInput";input.MaxLength=8000;
            record=UiIcons.Button("mic","听写");record.Name="VoiceRecord";
            clear=UiIcons.Button("trash","清空当前聊天记录");clear.Name="ClearChat";clear.Margin=new Thickness(6,0,0,0);clear.Click+=delegate {try{session.Clear();ShowStatus("已清空当前会话。");}catch(Exception e){ShowError(e);}};DockPanel.SetDock(clear,Dock.Right);historyTools.Children.Add(clear);
            composer=new ConversationComposer(pet,this,input,record,status,false);var composerSurface=composer;composerSurface.Margin=new Thickness(0,12,0,0);Grid.SetRow(composerSurface,2);root.Children.Add(composerSurface);
            workspaceShell=new Grid();workspaceShell.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(0)});workspaceShell.ColumnDefinitions.Add(new ColumnDefinition());workspaceShell.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(0)});
            Grid.SetColumn(root,1);workspaceShell.Children.Add(root);
            navigation=new AgentWorkspaceNavigation(pet,delegate{scroll.ScrollToEnd();input.Focus();},delegate{taskPanel.IsExpanded=true;taskPanel.Focus();});navigation.Visibility=Visibility.Collapsed;workspaceShell.Children.Add(navigation);
            inspectorHost=new ContentControl{VerticalContentAlignment=VerticalAlignment.Stretch,HorizontalContentAlignment=HorizontalAlignment.Stretch};
            var inspectorBody=new Grid();inspectorBody.RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});inspectorBody.RowDefinitions.Add(new RowDefinition());var inspectorTitle=Ui.Text("任务与变更",15);inspectorTitle.FontWeight=FontWeights.SemiBold;inspectorTitle.Margin=new Thickness(16,24,16,18);inspectorBody.Children.Add(inspectorTitle);Grid.SetRow(inspectorHost,1);inspectorBody.Children.Add(inspectorHost);
            inspector=new Border{Name="AgentWorkspaceInspector",Child=inspectorBody,BorderBrush=Ui.Line,BorderThickness=new Thickness(1,0,0,0),Background=Ui.Brush("#FCFCFD"),Visibility=Visibility.Collapsed};Grid.SetColumn(inspector,2);workspaceShell.Children.Add(inspector);Content=workspaceShell;
            StateChanged+=delegate{RefreshWorkspaceLayout();};SizeChanged+=delegate{RefreshWorkspaceLayout();};
            messagePump=new MessageUpdatePump(session,UpdateMessage);ReloadHistory();session.MessageAdded+=AddMessage;session.HistoryChanged+=HistoryChanged;session.StateChanged+=RefreshState;pet.Live.StateChanged+=RefreshState;RefreshAppearance();RefreshState();
            Closed+=delegate {closed=true;navigation.Dispose();taskPanel.Dispose();messagePump.Dispose();session.MessageAdded-=AddMessage;session.HistoryChanged-=HistoryChanged;session.StateChanged-=RefreshState;pet.Live.StateChanged-=RefreshState;};
            Loaded+=delegate {input.Focus();};
        }
        void RefreshWorkspaceLayout()
        {
            if(workspaceShell==null || closed)return;bool wide=WindowState==WindowState.Maximized;bool showInspector=wide && ActualWidth>=1120;
            if(workspaceLayout!=wide){if(wide)compactTaskExpanded=taskPanel.IsExpanded;workspaceLayout=wide;MaxHeight=wide?Double.PositiveInfinity:normalMaxHeight;conversationSurface.MaxWidth=wide?1000:Double.PositiveInfinity;navigation.Visibility=wide?Visibility.Visible:Visibility.Collapsed;}
            workspaceShell.ColumnDefinitions[0].Width=new GridLength(wide?Math.Min(240,Math.Max(190,ActualWidth*.17)):0);workspaceShell.ColumnDefinitions[2].Width=new GridLength(showInspector?Math.Min(360,Math.Max(280,ActualWidth*.23)):0);
            if(inspectorLayout==showInspector)return;bool follow=scroll.ScrollableHeight-scroll.VerticalOffset<32;double offset=scroll.VerticalOffset;inspectorLayout=showInspector;
            if(showInspector){messageArea.Children.Remove(taskPanel);inspectorHost.Content=taskPanel;taskPanel.SetWorkspaceLayout(true);taskPanel.IsExpanded=true;inspector.Visibility=Visibility.Visible;}
            else{inspectorHost.Content=null;taskPanel.SetWorkspaceLayout(false);Grid.SetRow(taskPanel,1);messageArea.Children.Add(taskPanel);taskPanel.IsExpanded=compactTaskExpanded;inspector.Visibility=Visibility.Collapsed;}
            Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(delegate{if(closed)return;if(follow)scroll.ScrollToEnd();else scroll.ScrollToVerticalOffset(offset);}));
        }
        public void RefreshAppearance()
        {if(!(Content is LiveCallView))Title=pet.Skin.Name+" · 对话";avatar.Source=SkinCatalog.GetAvatar(pet.Skin);avatar.ToolTip=pet.Skin.Name;if(composer!=null)composer.RefreshModels();if(taskPanel!=null && !session.IsBusy)session.LoadAgentTask(pet.Settings);if(navigation!=null)navigation.Refresh();}
        public void ReloadHistory(bool scrollToBottom=true)
        {
            reloading=true;messages.Children.Clear();rendered.Clear();displayed.Clear();showingWelcome=false;
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
            displayed[message.Id]=message;rendered[message.Id].Update(message);
            if(follow)Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate{if(!closed)scroll.ScrollToEnd();}));
        }
        private void Welcome()
        {showingWelcome=true;var panel=new StackPanel {Margin=new Thickness(26,28,26,26)};var character=new Image{Source=SkinCatalog.GetAvatar(pet.Skin),Width=154,Height=154,Stretch=Stretch.Uniform,Clip=new RectangleGeometry(new Rect(0,0,154,154),30.8,30.8),HorizontalAlignment=HorizontalAlignment.Left};RenderOptions.SetBitmapScalingMode(character,BitmapScalingMode.HighQuality);panel.Children.Add(character);var heading=Ui.Text("一起聊聊今天的想法",24);heading.FontWeight=FontWeights.SemiBold;panel.Children.Add(heading);panel.Children.Add(Ui.Text("聊代码、设计，或刚想到的小点子。\n每位伙伴拥有各自的人格与会话。",13,Ui.Muted));var starters=new WrapPanel{Margin=new Thickness(0,18,0,0)};foreach(string idea in new[]{"帮我梳理一个想法","一起检查这段代码"}){string value=idea;var button=Ui.Button(idea);button.Margin=new Thickness(0,0,8,8);button.Click+=delegate {input.Text=value;input.Focus();};starters.Children.Add(button);}panel.Children.Add(starters);messages.Children.Add(panel);}
        private void AddMessage(ChatMessage message)
        {
            if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(delegate {AddMessage(message);}));return;}
            if(!session.History.Messages.Any(m=>m.Id==message.Id))return;if(rendered.ContainsKey(message.Id)){UpdateMessage(message);return;}bool follow=scroll.ScrollableHeight-scroll.VerticalOffset<32;
            if(showingWelcome){messages.Children.Clear();showingWelcome=false;}bool user=message.Role=="user";if(!reloading && !follow && user)visibleTurns=Math.Min(200,visibleTurns+1);displayed[message.Id]=message;
            var view=new ChatMessageView(message,messages,pet,this);rendered[message.Id]=view;messages.Children.Add(view);
            while(messages.Children.Count>visibleTurns*2){var old=messages.Children[0] as Border;if(old!=null){string id=old.Tag as string;if(id!=null){rendered.Remove(id);displayed.Remove(id);}}messages.Children.RemoveAt(0);}
            if(!reloading){earlier.IsEnabled=session.History.Messages.Select(m=>m.TurnId).Distinct().Count()>visibleTurns;export.IsEnabled=true;if(follow)Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(delegate{if(!closed)scroll.ScrollToEnd();}));}
        }
        private void RefreshState()
        {
            if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(RefreshState));return;}
            clear.IsEnabled=!pet.DialogueBusy;RefreshAppearance();if(pet.DictationActive)return;
            if(pet.LiveActive){ShowStatus(pet.Live.Status);return;}
            ShowStatus(session.Stage==ConversationStage.Recognizing?"正在识别语音…":session.Stage==ConversationStage.Thinking?"正在思考…":session.Stage==ConversationStage.Speaking?"正在朗读 · 可以随时停止":String.IsNullOrEmpty(session.AgentTaskWarning)?String.IsNullOrEmpty(session.HistoryWarning)?(String.IsNullOrEmpty(session.LastNotice)?"Enter 发送 · Shift + Enter 换行":session.LastNotice):session.HistoryWarning:session.AgentTaskWarning);
        }
        private void ShowStatus(string text){if(!closed)status.Text=text;}
        private void ShowError(Exception error)
        {if(error is OperationCanceledException)ShowStatus("已停止。");else if(error is ApiException || error is InvalidOperationException)ShowStatus(error.Message);else ShowStatus("操作未完成，请检查连接设置、麦克风或音频输出。当前文字回复会保留。");}
    }
}
