using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Dafeiyu
{
    internal sealed class ConversationComposer : StackPanel
    {
        static readonly ControlTemplate hintTemplate=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' TargetType='ToolTip'><Border Background='#202329' CornerRadius='8' Padding='9,6'><ContentPresenter/></Border></ControlTemplate>");
        readonly PetWindow pet;readonly Window host;readonly TextBox input;readonly TextBlock status;readonly ConversationQueue queue;
        readonly Button primary,options;readonly Border queueBox;readonly StackPanel rows;readonly TextBlock queueLabel;readonly Button resume;
        readonly DictationSession dictation;readonly Button record,finishDictation,stopDictation;readonly FrameworkElement normalSurface;readonly Border recordingSurface;readonly DictationWaveform waveform;
        readonly System.Windows.Threading.DispatcherTimer meter=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(80)};
        readonly System.Diagnostics.Stopwatch recordingTime=new System.Diagnostics.Stopwatch();bool finishing;
        string signature="",editingId;TextBox editor;bool resumeAfterEdit,closed,subscribed;int editRevision;
        public bool HasEdit {get{return editingId!=null;}}
        public ConversationComposer(PetWindow pet,Window host,TextBox input,Button record,TextBlock status,bool compact)
        {
            this.pet=pet;this.host=host;this.input=input;this.status=status;queue=pet.InputQueue;dictation=pet.Dictation;this.record=record;
            var queueBody=new StackPanel();var heading=new DockPanel{Margin=new Thickness(10,4,7,1)};
            resume=SmallText("继续队列","ResumeQueue");resume.Foreground=Ui.Blue;resume.Click+=delegate{Try(delegate{queue.Resume();});};DockPanel.SetDock(resume,Dock.Right);heading.Children.Add(resume);
            queueLabel=Ui.Text("",10,Ui.Muted);queueLabel.VerticalAlignment=VerticalAlignment.Center;heading.Children.Add(queueLabel);queueBody.Children.Add(heading);
            rows=new StackPanel();var list=Ui.Scroll(rows);list.MaxHeight=compact?112:160;queueBody.Children.Add(list);
            queueBox=new Border{Child=queueBody,BorderBrush=Ui.Line,BorderThickness=new Thickness(1,1,1,0),CornerRadius=new CornerRadius(14,14,0,0),Margin=new Thickness(10,0,10,-10),Padding=new Thickness(0,0,0,14),Background=Ui.Brush("#FAFBFD")};Children.Add(queueBox);
            var controls=new StackPanel{Orientation=Orientation.Horizontal};record.Margin=new Thickness(0);record.Width=32;record.Height=32;record.MinHeight=32;
            options=UiIcons.Button("down","发送选项");options.Name="SendOptions";options.Width=22;options.MinWidth=0;options.Margin=new Thickness(0,0,3,0);options.Padding=new Thickness(2);controls.Children.Add(options);
            record.Margin=new Thickness(0,0,5,0);controls.Children.Add(record);
            var menu=new ContextMenu();var interrupt=new MenuItem{Header="打断并发送",InputGestureText="Ctrl+Enter"};interrupt.Click+=async delegate{await InterruptDraft();};menu.Items.Add(interrupt);options.ContextMenu=menu;options.Click+=delegate{menu.PlacementTarget=options;menu.IsOpen=true;};
            primary=ChatUi.Action("send","发送");primary.Name=compact?"SendQuickMessage":"SendMessage";primary.Width=primary.Height=primary.MinHeight=32;primary.Background=Ui.Brush("#357FF5");primary.BorderBrush=primary.Background;controls.Children.Add(primary);
            primary.Click+=async delegate
            {
                if(!String.IsNullOrWhiteSpace(input.Text)){Submit();return;}
                if(pet.DialogueBusy){try{await pet.StopDialogueAsync();}catch(Exception e){Error(e);}return;}
                pet.ShowLiveCall(host);
            };
            // The status line stays outside both surfaces while recording replaces
            // the composer body with a real microphone waveform and explicit controls.
            normalSurface=ChatUi.Composer(input,null,controls,new TextBlock{Visibility=Visibility.Collapsed},compact);Children.Add(normalSurface);
            var recordingBody=new StackPanel();recordingBody.Children.Add(Ui.Text("正在听写…",12,Ui.Muted));var recordingControls=new DockPanel{Margin=new Thickness(0,12,0,0)};
            stopDictation=UiIcons.Button("stop-solid","停止听写");stopDictation.Name="StopDictation";stopDictation.Width=32;stopDictation.Height=32;stopDictation.MinHeight=32;stopDictation.Content=UiIcons.Image("stop-solid",Ui.Brush("#202329"));stopDictation.Margin=new Thickness(0,0,5,0);stopDictation.Click+=async delegate{await FinishDictation(false);};
            finishDictation=ChatUi.Action("send","发送听写");finishDictation.Name="FinishDictation";finishDictation.Width=finishDictation.Height=finishDictation.MinHeight=32;finishDictation.Background=Ui.Brush("#202329");finishDictation.BorderBrush=finishDictation.Background;finishDictation.Content=UiIcons.Image("send",Brushes.White);finishDictation.Click+=async delegate{await FinishDictation(true);};DockPanel.SetDock(finishDictation,Dock.Right);recordingControls.Children.Add(finishDictation);
            DockPanel.SetDock(stopDictation,Dock.Right);recordingControls.Children.Add(stopDictation);
            waveform=new DictationWaveform();recordingControls.Children.Add(waveform);recordingBody.Children.Add(recordingControls);
            recordingSurface=new Border{Child=recordingBody,Background=Brushes.White,BorderBrush=Ui.Line,BorderThickness=new Thickness(1),CornerRadius=new CornerRadius(22),Padding=new Thickness(8,12,8,compact?14:22),Visibility=Visibility.Collapsed};Children.Add(recordingSurface);
            status.Margin=new Thickness(3,7,3,0);status.MinHeight=16;status.MaxHeight=38;Children.Add(status);
            Hint(record,"听写");Hint(stopDictation,"停止听写");Hint(finishDictation,"发送听写");record.Click+=delegate{Try(delegate{dictation.Start(this);recordingTime.Restart();waveform.Clear();});};
            meter.Tick+=async delegate{if(!dictation.Owns(this)){meter.Stop();return;}waveform.Push(dictation.InputLevel);if(dictation.Stage==DictationStage.Recording && recordingTime.Elapsed.TotalSeconds>=60)await FinishDictation(false);};
            host.PreviewKeyDown+=async delegate(object sender,KeyEventArgs e){if(e.Key==Key.Escape && dictation.Owns(this)){e.Handled=true;try{await dictation.CancelAsync(this);}catch(Exception error){Error(error);}}};
            input.ToolTip="Enter 发送或排队 · Shift + Enter 换行 · Ctrl + Enter 打断并发送";
            input.TextChanged+=delegate{RefreshAction();};input.PreviewKeyDown+=async delegate(object sender,KeyEventArgs e)
            {
                if(e.Key!=Key.Enter || (Keyboard.Modifiers&ModifierKeys.Shift)!=0)return;e.Handled=true;
                if((Keyboard.Modifiers&ModifierKeys.Control)!=0 && pet.DialogueBusy)await InterruptDraft();else Submit();
            };
            Loaded+=delegate{if(!subscribed){queue.Changed+=Refresh;pet.Live.StateChanged+=Refresh;dictation.Changed+=Refresh;subscribed=true;}Refresh();};
            host.IsVisibleChanged+=async delegate{if(!host.IsVisible && dictation.Owns(this))try{await dictation.CancelAsync(this);}catch(Exception e){Error(e);}};
            host.Closed+=async delegate{closed=true;meter.Stop();if(subscribed){queue.Changed-=Refresh;pet.Live.StateChanged-=Refresh;dictation.Changed-=Refresh;subscribed=false;}if(dictation.Owns(this))try{await dictation.CancelAsync(this);}catch(OperationCanceledException){}catch(ObjectDisposedException){}};
            Refresh();
        }
        void Submit()
        {
            if(String.IsNullOrWhiteSpace(input.Text) || pet.LiveActive || pet.IsUpdating)return;
            Try(delegate{queue.Enqueue(input.Text);input.Clear();input.Focus();});
        }
        async System.Threading.Tasks.Task InterruptDraft()
        {
            if(String.IsNullOrWhiteSpace(input.Text) || pet.LiveActive || pet.IsUpdating)return;string text=input.Text;
            try{await queue.InterruptAsync(text);if(input.Text==text)input.Clear();}catch(Exception e){Error(e);}
        }
        void Refresh()
        {
            if(closed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(Refresh));return;}
            RefreshAction();RefreshDictation();var items=queue.Items;
            string next=String.Join("|",items.Select(m=>m.Id+":"+m.Text))+"/"+queue.IsPaused+"/"+editingId;
            queueBox.Visibility=items.Count==0?Visibility.Collapsed:Visibility.Visible;
            queueLabel.Text=(queue.IsPaused?"已暂停 · ":"排队中 · ")+items.Count+" 条";
            resume.Visibility=queue.IsPaused && items.Count>0 && editingId==null?Visibility.Visible:Visibility.Collapsed;resume.IsEnabled=!pet.LiveActive;
            if(!String.IsNullOrEmpty(queue.Notice))status.Text=queue.Notice;
            if(signature==next)return;signature=next;
            string draft=editor==null?null:editor.Text;rows.Children.Clear();editor=null;
            foreach(var item in items)
            {
                var captured=item;var row=new Grid{Margin=new Thickness(9,2,7,2)};row.ColumnDefinitions.Add(new ColumnDefinition());row.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
                if(item.Id==editingId)
                {
                    var editPanel=new StackPanel();editor=Ui.Field(draft??item.Text,true);editor.Name="QueueEditInput";editor.MaxLength=8000;editor.MinHeight=46;editor.MaxHeight=72;editor.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;editPanel.Children.Add(editor);
                    var editTools=new StackPanel{Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right};var save=SmallText("保存","SaveQueuedEdit");save.Click+=delegate{Try(delegate{queue.Edit(captured.Id,editor.Text);EndEdit();});};editTools.Children.Add(save);var cancel=SmallText("取消","CancelQueuedEdit");cancel.Click+=delegate{EndEdit();};editTools.Children.Add(cancel);editPanel.Children.Add(editTools);Grid.SetColumnSpan(editPanel,2);row.Children.Add(editPanel);
                }
                else
                {
                    var text=Ui.Text(item.Text.Replace("\r"," ").Replace("\n"," "),12);text.TextWrapping=TextWrapping.NoWrap;text.TextTrimming=TextTrimming.CharacterEllipsis;text.VerticalAlignment=VerticalAlignment.Center;text.Margin=new Thickness(2,0,5,0);text.ToolTip=item.Text;row.Children.Add(text);
                    var tools=new StackPanel{Orientation=Orientation.Horizontal};Grid.SetColumn(tools,1);row.Children.Add(tools);
                    var guide=SmallText("引导","GuideQueuedMessage");var guideLabel=new StackPanel{Orientation=Orientation.Horizontal};var guideArrow=UiIcons.Image("steer",Ui.Muted);guideArrow.Width=guideArrow.Height=12;guideArrow.Margin=new Thickness(0,0,3,0);guideArrow.VerticalAlignment=VerticalAlignment.Center;guideLabel.Children.Add(guideArrow);guideLabel.Children.Add(Ui.Text("引导",11,Ui.Muted));guide.Content=guideLabel;guide.ToolTip="停止当前回复，优先发送这一条，再继续其他排队消息";guide.Click+=async delegate{try{await queue.InterruptAsync(captured.Text,captured.Id);}catch(Exception e){Error(e);}};tools.Children.Add(guide);
                    var remove=SmallIcon("trash","删除排队消息","RemoveQueuedMessage");remove.Click+=delegate{Try(()=>queue.Remove(captured.Id));};tools.Children.Add(remove);
                    var edit=SmallIcon("edit","编辑消息","EditQueuedMessage");edit.Click+=delegate{resumeAfterEdit=!queue.IsPaused;editRevision=queue.PauseRevision;editingId=captured.Id;queue.Hold();Refresh();if(editor!=null){editor.Focus();editor.SelectAll();}};tools.Children.Add(edit);
                }
                rows.Children.Add(row);
            }
            if(editingId!=null && !items.Any(m=>m.Id==editingId)){editingId=null;editor=null;}
        }
        void EndEdit(){editingId=null;editor=null;signature="";if(resumeAfterEdit && editRevision==queue.PauseRevision)Try(()=>queue.Resume());Refresh();}
        void RefreshAction()
        {
            bool text=!String.IsNullOrWhiteSpace(input.Text),busy=pet.DialogueBusy;string action=text?"send":busy?"stop":"live";
            string label=text?(busy || queue.Items.Count>0?"加入队列":"发送"):busy?"停止并暂停队列":"通话";
            primary.Tag=action;Hint(primary,label);
            primary.Content=UiIcons.Image(action=="stop"?"stop-solid":action,Brushes.White);((FrameworkElement)primary.Content).Width=18;((FrameworkElement)primary.Content).Height=18;
            primary.IsEnabled=!pet.IsUpdating && (!pet.LiveActive || !text) && (text || busy || !dictation.IsBusy);options.Visibility=text && busy && !pet.LiveActive?Visibility.Visible:Visibility.Collapsed;
            record.IsEnabled=!pet.IsUpdating && !pet.LiveActive && !dictation.IsBusy && pet.Conversation.Stage!=ConversationStage.Recording;
        }
        void RefreshDictation()
        {
            bool own=dictation.Owns(this);normalSurface.Visibility=own?Visibility.Collapsed:Visibility.Visible;recordingSurface.Visibility=own?Visibility.Visible:Visibility.Collapsed;
            finishDictation.IsEnabled=stopDictation.IsEnabled=own && dictation.Stage==DictationStage.Recording && !finishing;
            if(own){status.Text=dictation.Stage==DictationStage.Recognizing?"正在识别…":"";if(!meter.IsEnabled)meter.Start();}else{meter.Stop();}
        }
        async System.Threading.Tasks.Task FinishDictation(bool send)
        {
            if(finishing || !dictation.Owns(this))return;finishing=true;RefreshDictation();
            try
            {
                string text=await dictation.FinishAsync(this);if(closed || String.IsNullOrWhiteSpace(text))return;
                input.Text=String.IsNullOrWhiteSpace(input.Text)?text:input.Text+Environment.NewLine+text;input.CaretIndex=input.Text.Length;input.Focus();
                status.Text=input.Text.Length>8000?"听写内容已保留，请缩短到 8000 字以内再发送。":"听写已填入输入框，可修改后发送。";
                if(send)Submit();
            }
            catch(OperationCanceledException){if(!closed)status.Text="已取消听写。";}catch(Exception e){if(!closed)Error(e);}
            finally{finishing=false;RefreshAction();RefreshDictation();}
        }
        static Button SmallText(string text,string name){var b=Ui.Button(text);b.Name=name;b.FontSize=11;b.MinHeight=26;b.Padding=new Thickness(6,2,6,2);b.Margin=new Thickness(0);b.Background=Brushes.Transparent;b.BorderBrush=Brushes.Transparent;b.Foreground=Ui.Muted;return b;}
        static void Hint(Button button,string text)
        {
            var hint=button.ToolTip as ToolTip;
            if(hint==null)
            {
                hint=new ToolTip{Template=hintTemplate,Foreground=Brushes.White,FontSize=12,PlacementTarget=button,Placement=System.Windows.Controls.Primitives.PlacementMode.Custom};
                hint.CustomPopupPlacementCallback=delegate(Size popup,Size target,Point offset){return new[]{new System.Windows.Controls.Primitives.CustomPopupPlacement(new Point((target.Width-popup.Width)/2,-popup.Height-6),System.Windows.Controls.Primitives.PopupPrimaryAxis.Horizontal)};};
                button.ToolTip=hint;ToolTipService.SetInitialShowDelay(button,350);
            }
            hint.Content=text;System.Windows.Automation.AutomationProperties.SetName(button,text);
        }
        static Button SmallIcon(string icon,string label,string name){var b=UiIcons.Button(icon,label);b.Name=name;b.Width=26;b.Height=26;b.MinHeight=26;b.Margin=new Thickness(0);b.Padding=new Thickness(4);var glyph=(FrameworkElement)b.Content;glyph.Width=glyph.Height=14;return b;}
        void Try(Action action){try{action();}catch(Exception e){Error(e);}}
        void Error(Exception e){status.Text=e is ApiException || e is InvalidOperationException?e.Message:e is OperationCanceledException?"已停止。":"操作未完成，消息已保留。请检查配置或数据目录。";}
    }
}
