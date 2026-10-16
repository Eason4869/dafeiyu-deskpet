using System;
using System.Globalization;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dafeiyu
{
    // Both Windows chat surfaces render the same message and keep expansion state
    // while streaming snapshots update only their text.
    internal sealed class ChatMessageView : Border
    {
        public readonly MarkdownView Body;
        readonly TextBlock state,preview,thinkingLabel;
        readonly TextBlock elapsed;
        readonly System.Windows.Threading.DispatcherTimer clock=new System.Windows.Threading.DispatcherTimer{Interval=TimeSpan.FromMilliseconds(250)};
        readonly TextBox details;
        readonly Expander thinking;
        readonly StackPanel toolLog=new StackPanel();
        readonly Dictionary<string,Expander> toolRows=new Dictionary<string,Expander>();
        readonly StackPanel thoughtArea=new StackPanel{Name="ThinkingProcess"};
        readonly Border previewRow;
        string previousReasoning="",previousTools="";bool latestIsTool;
        bool inspectThinking;bool detailedProcess;readonly PetWindow pet;
        readonly bool user;
        readonly StackPanel actions;ChatMessage current;
        public ChatMessageView(ChatMessage message,FrameworkElement history,PetWindow pet,Window host)
        {
            this.pet=pet;detailedProcess=pet.Settings.AgentDetailedProcess;user=message.Role=="user";Name=user?"UserMessage":"AssistantMessage";Tag=message.Id;
            HorizontalAlignment=user?HorizontalAlignment.Right:HorizontalAlignment.Stretch;
            Background=Brushes.Transparent;BorderThickness=new Thickness(0);
            Padding=user?new Thickness(0):new Thickness(2,8,2,4);Margin=new Thickness(0,0,0,user?24:28);
            if(user)SetBinding(MaxWidthProperty,new Binding("ActualWidth"){Source=history,Converter=new ChatWidthConverter(.5,0)});
            var panel=new StackPanel();Child=panel;
            elapsed=Ui.Text("",12,Ui.Brush("#969BA3"));elapsed.Name="ReplyElapsedTime";
            if(!user)panel.Children.Add(new Border{Child=elapsed,BorderBrush=Ui.Brush("#ECECEC"),BorderThickness=new Thickness(0,0,0,1),Padding=new Thickness(0,0,0,8),Margin=new Thickness(0,0,0,10)});
            clock.Tick+=delegate{RefreshElapsed();};Loaded+=delegate{RefreshElapsed();};Unloaded+=delegate{clock.Stop();};
            thinkingLabel=Ui.Text("",12,Ui.Muted);thinkingLabel.Name="ProcessStatus";
            preview=Ui.Text("",14,Ui.Ink);preview.Name="ProcessPreview";preview.TextWrapping=TextWrapping.NoWrap;preview.TextTrimming=TextTrimming.CharacterEllipsis;
            previewRow=new Border{BorderBrush=Ui.Brush("#ECECEC"),BorderThickness=new Thickness(0,1,0,0),Padding=new Thickness(0,9,0,0),Margin=new Thickness(0,5,0,14),Child=preview};
            details=TextBox("",14,Ui.Ink);details.MaxHeight=240;details.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;details.Margin=new Thickness(20,7,0,8);
            var processDetails=new StackPanel();processDetails.Children.Add(details);processDetails.Children.Add(toolLog);
            thinking=new Expander{Name="ReasoningDetails",Header=thinkingLabel,Content=processDetails,IsExpanded=detailedProcess,BorderThickness=new Thickness(0),Background=Brushes.Transparent,Visibility=Visibility.Collapsed};
            ChatUi.ProcessExpander(thinking);
            thinking.Expanded+=delegate{previewRow.Visibility=Visibility.Collapsed;};thinking.Collapsed+=delegate{previewRow.Visibility=String.IsNullOrEmpty(preview.Text)?Visibility.Collapsed:Visibility.Visible;};
            if(!user){thoughtArea.Children.Add(thinking);thoughtArea.Children.Add(previewRow);panel.Children.Add(thoughtArea);}
            if(message.Attachments!=null && message.Attachments.Count>0)
            {
                var files=new WrapPanel{Name="SentAttachments",HorizontalAlignment=user?HorizontalAlignment.Right:HorizontalAlignment.Left,Margin=new Thickness(0,0,0,8)};
                foreach(var file in message.Attachments)files.Children.Add(AttachmentCard(file));
                panel.Children.Add(files);
            }
            Body=new MarkdownView{Text=message.Content};
            if(user)panel.Children.Add(new Border{Name="SentUserBubble",Background=Ui.Brush("#E8F2FF"),CornerRadius=new CornerRadius(18),Padding=new Thickness(15,10,15,10),Child=Body,Visibility=String.IsNullOrEmpty(message.Content)?Visibility.Collapsed:Visibility.Visible});
            else panel.Children.Add(Body);
            state=Ui.Text("",11,Ui.Muted);state.Margin=new Thickness(0,6,0,0);if(!user)panel.Children.Add(state);
            actions=new StackPanel{Name="ReplyActions",Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Left,Margin=new Thickness(0,7,0,0)};
            if(user)
            {
                var userActions=new StackPanel{Name="UserMessageActions",Orientation=Orientation.Horizontal,HorizontalAlignment=HorizontalAlignment.Right,Margin=new Thickness(0,3,0,0),Opacity=0};
                var sent=Ui.Text(message.Timestamp.ToLocalTime().ToString("HH:mm"),11,Ui.Brush("#969BA3"));sent.Name="UserMessageTime";sent.VerticalAlignment=VerticalAlignment.Center;sent.Margin=new Thickness(0,0,7,0);sent.ToolTip=message.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss");userActions.Children.Add(sent);
                var copyUser=ActionIcon("copy","复制消息","CopyUserMessage");copyUser.Margin=new Thickness(0);copyUser.Click+=delegate{try{Clipboard.SetText(current.Content??"");}catch(System.Runtime.InteropServices.COMException){pet.Conversation.ReportNotice("剪贴板正忙，请再试一次。");}};userActions.Children.Add(copyUser);panel.Children.Add(userActions);
                Action syncActions=delegate{userActions.Opacity=IsMouseOver || IsKeyboardFocusWithin?1:0;};MouseEnter+=delegate{syncActions();};MouseLeave+=delegate{syncActions();};GotKeyboardFocus+=delegate{syncActions();};LostKeyboardFocus+=delegate{Dispatcher.BeginInvoke(syncActions);};
            }
            if(!user)
            {
                var copy=ActionIcon("copy","复制","CopyReply");copy.Click+=delegate{try{Clipboard.SetText(current.Content);}catch(System.Runtime.InteropServices.COMException){pet.Conversation.ReportNotice("剪贴板正忙，请再试一次。");}};actions.Children.Add(copy);
                var speak=ActionIcon("speaker","朗读","ReadReply");speak.Click+=async delegate{try{if(pet.Conversation.Stage==ConversationStage.Speaking)pet.StopReplySpeech();else {pet.InputQueue.Hold();await pet.Conversation.SpeakReplyAsync(current.Id,pet.Settings);}}catch(Exception e){pet.Conversation.ReportNotice(e is ApiException || e is InvalidOperationException?e.Message:"朗读未完成，请检查语音设置。");}};actions.Children.Add(speak);
                var more=ActionIcon("more","更多","MoreReply");var menu=ChatUi.Menu();var regenerate=new MenuItem{Header="重新生成此回复",Name="RegenerateReply"};regenerate.Click+=async delegate{try{pet.InputQueue.Hold();await pet.Conversation.RegenerateAsync(current.Id,pet.Settings,pet.Skin.Name);}catch(Exception e){pet.Conversation.ReportNotice(e is ApiException || e is InvalidOperationException?e.Message:"重新生成未完成。");}};menu.Items.Add(regenerate);
                var inspect=new MenuItem{Header="查看思考与工具记录",IsCheckable=true};inspect.Click+=delegate{inspectThinking=inspect.IsChecked;thinking.IsExpanded=inspectThinking;Update(current);};menu.Items.Add(inspect);
                var plain=new MenuItem{Header="复制纯文本"};plain.Click+=delegate{try{Clipboard.SetText(MarkdownContent.PlainText(current.Content));}catch(System.Runtime.InteropServices.COMException){pet.Conversation.ReportNotice("剪贴板正忙，请再试一次。");}};menu.Items.Add(plain);
                var export=new MenuItem{Header="导出本条回复…"};export.Click+=delegate{var picker=new Microsoft.Win32.SaveFileDialog{Filter="Markdown (*.md)|*.md",FileName="DeskPet-reply-"+current.Timestamp.ToLocalTime().ToString("yyyyMMdd-HHmmss")+".md"};if(picker.ShowDialog(host)==true)try{File.WriteAllText(picker.FileName,current.Content,new System.Text.UTF8Encoding(false));}catch(IOException){pet.Conversation.ReportNotice("导出失败，请选择可写入的位置。");}catch(UnauthorizedAccessException){pet.Conversation.ReportNotice("导出失败，请检查目录权限。");}};menu.Items.Add(export);more.ContextMenu=menu;more.Click+=delegate{regenerate.IsEnabled=!pet.DialogueBusy && !pet.InputQueue.IsRunning;menu.PlacementTarget=more;menu.IsOpen=true;};actions.Children.Add(more);panel.Children.Add(actions);
            }
            Update(message);
        }
        void RefreshElapsed()
        {
            if(user || current==null){clock.Stop();return;}
            elapsed.Text=current.ProcessingStartedAt.HasValue?current.ProcessingLabel(DateTime.UtcNow):"";
            ((Border)elapsed.Parent).Visibility=current.ProcessingStartedAt.HasValue?Visibility.Visible:Visibility.Collapsed;
            if(IsLoaded && current.Status=="pending" && current.ProcessingStartedAt.HasValue)clock.Start();else clock.Stop();
        }
        static Border AttachmentCard(ChatAttachment file)
        {
            var card=new Border{Name="SentAttachmentCard",Width=file.IsImage?80:160,Height=80,CornerRadius=new CornerRadius(10),BorderBrush=Ui.Brush("#E2E3E6"),BorderThickness=new Thickness(1),Background=Ui.Brush("#FAFAFA"),Margin=new Thickness(6,0,0,0),ToolTip=file.Name};
            if(file.IsImage)try
            {
                var bitmap=new BitmapImage();using(var stream=new MemoryStream(Convert.FromBase64String(file.Data))){bitmap.BeginInit();bitmap.CacheOption=BitmapCacheOption.OnLoad;bitmap.DecodePixelWidth=160;bitmap.StreamSource=stream;bitmap.EndInit();bitmap.Freeze();}
                // Painting the border itself clips the thumbnail to its rounded corners.
                card.Background=new ImageBrush(bitmap){Stretch=Stretch.UniformToFill};return card;
            }
            catch(Exception e){if(!(e is IOException || e is FormatException || e is NotSupportedException || e is ArgumentException))throw;}
            var content=new StackPanel{Margin=new Thickness(10,8,10,6)};var icon=UiIcons.Image("file",Ui.Muted);icon.HorizontalAlignment=HorizontalAlignment.Left;content.Children.Add(icon);
            var label=Ui.Text(file.Name,11,Ui.Ink);label.TextWrapping=TextWrapping.NoWrap;label.TextTrimming=TextTrimming.CharacterEllipsis;label.Margin=new Thickness(0,8,0,0);content.Children.Add(label);card.Child=content;return card;
        }
        static Button ActionIcon(string icon,string label,string name){var button=UiIcons.Button(icon,label);button.Name=name;button.Width=button.Height=button.MinHeight=28;button.Padding=new Thickness(3);button.Content=UiIcons.Image(icon,Ui.Brush("#969BA3"));button.Margin=new Thickness(0,0,5,0);return button;}
        static TextBox TextBox(string text,int size,Brush color)
        {return new TextBox{Text=text,IsReadOnly=true,TextWrapping=TextWrapping.Wrap,AcceptsReturn=true,BorderThickness=new Thickness(0),Background=Brushes.Transparent,Foreground=color,Padding=new Thickness(0),FontSize=size,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};}
        public void Update(ChatMessage message)
        {
            current=message;
            RefreshElapsed();
            bool detailChanged=detailedProcess!=pet.Settings.AgentDetailedProcess;
            if(detailChanged){detailedProcess=pet.Settings.AgentDetailedProcess;thinking.IsExpanded=detailedProcess;}
            Body.Text=message.Content;Body.Visibility=String.IsNullOrEmpty(message.Content)?Visibility.Collapsed:Visibility.Visible;
            bool hasThinking=!String.IsNullOrWhiteSpace(message.Reasoning),active=message.Status=="pending" && String.IsNullOrEmpty(message.Content);
            bool hasTools=message.ToolEvents!=null && message.ToolEvents.Count>0;
            thoughtArea.Visibility=active || inspectThinking && (hasThinking || hasTools)?Visibility.Visible:Visibility.Collapsed;
            thinking.Visibility=Visibility.Visible;
            details.Visibility=hasThinking?Visibility.Visible:Visibility.Collapsed;
            var events=message.ToolEvents??new List<AgentToolEvent>();
            foreach(var id in toolRows.Keys.Where(id=>!events.Any(e=>e.Id==id)).ToArray()){toolLog.Children.Remove(toolRows[id]);toolRows.Remove(id);}
            foreach(var item in events)
            {
                Expander row;if(!toolRows.TryGetValue(item.Id,out row))
                {
                    var line=Ui.Text("",12,Ui.Brush("#969BA3"));line.Name="AgentToolLog";line.TextWrapping=TextWrapping.NoWrap;line.TextTrimming=TextTrimming.CharacterEllipsis;line.SetBinding(MaxWidthProperty,new Binding("ActualWidth"){Source=this,Converter=new ChatWidthConverter(1,30)});
                    var toolDetails=TextBox("",12,Ui.Muted);toolDetails.MaxHeight=200;toolDetails.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;
                    row=new Expander{Name="AgentToolDetails",Header=line,Content=toolDetails,IsExpanded=detailedProcess,Margin=new Thickness(3,2,0,3)};ChatUi.ProcessExpander(row);toolRows[item.Id]=row;toolLog.Children.Add(row);
                }
                else if(active && detailChanged)row.IsExpanded=detailedProcess;
                string summary=(item.Summary??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??item.Name;
                ((TextBlock)row.Header).Text=(item.Status=="running"?"◌ ":item.Status=="error"?"! ":"⌘ ")+summary+(item.Status=="running"?" · 执行中":"");((TextBox)row.Content).Text=item.Summary??"";
            }
            details.Text=message.Reasoning??"";
            UpdateProcess(message,events,active);
            previewRow.Visibility=!thinking.IsExpanded && !String.IsNullOrEmpty(preview.Text)?Visibility.Visible:Visibility.Collapsed;
            state.Text=active?"":MessagePresentation.Status(message.Status);
            state.Visibility=String.IsNullOrEmpty(state.Text)?Visibility.Collapsed:Visibility.Visible;
            actions.Visibility=message.Status=="complete" && !String.IsNullOrWhiteSpace(message.Content)?Visibility.Visible:Visibility.Collapsed;
        }
        void UpdateProcess(ChatMessage message,List<AgentToolEvent> events,bool active)
        {
            string text=message.Reasoning??"",signature=String.Join("\n",events.Select(t=>t.Id+"\t"+t.Status+"\t"+t.Summary));
            if(text!=previousReasoning)latestIsTool=false;
            if(signature!=previousTools)latestIsTool=events.Count>0;
            previousReasoning=text;previousTools=signature;
            var running=events.LastOrDefault(t=>t.Status=="running" || t.Status=="pending");
            var latest=running??(latestIsTool || String.IsNullOrWhiteSpace(text)?events.LastOrDefault():null);
            preview.Text=latest!=null?((latest.Summary??"").Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()??latest.Name):text.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries).LastOrDefault()??"";
            preview.Foreground=latest!=null?Ui.Muted:Ui.Ink;
            string stage=running==null?(latest!=null || String.IsNullOrWhiteSpace(text) && message.WorkMode?"处理中":"思考中"):
                running.Name=="write_file" || running.Name=="edit_file" || running.Name=="apply_patch" || running.Name=="create_directory"?"编辑中":
                running.Name=="run_command" || running.Name=="exec_command" || running.Name=="shell" || running.Name=="open_directory"?"运行中":"处理中";
            thinkingLabel.Text=active?stage:"处理记录";
        }
    }
    internal sealed class ChatWidthConverter : IValueConverter
    {
        readonly double factor,inset;public ChatWidthConverter(double factor,double inset){this.factor=factor;this.inset=inset;}
        public object Convert(object value,Type targetType,object parameter,CultureInfo culture){return Math.Max(1,(double)value*factor-inset);}
        public object ConvertBack(object value,Type targetType,object parameter,CultureInfo culture){throw new NotSupportedException();}
    }
}
