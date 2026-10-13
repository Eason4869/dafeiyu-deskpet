using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;
using Forms=System.Windows.Forms;

namespace Dafeiyu
{
    public sealed class PetWindow : Window
    {
        private SpriteAtlas atlas;
        private readonly Image image;
        private readonly DispatcherTimer timer;
        private readonly Stopwatch watch=Stopwatch.StartNew();
        private readonly EventWaitHandle wake;
        private readonly Random random=new Random();
        private readonly Forms.NotifyIcon tray;
        private readonly System.Drawing.Icon trayIcon;
        private readonly Forms.ContextMenuStrip trayMenu;
        private SettingsWindow preferences;
        private ChatWindow chat;
        private ConversationSession conversation;
        private QuickChatWindow quick;
        private GeminiLiveSession live;
        private string liveSkinId;
        private bool closeAfterLive;
        private Forms.ToolStripMenuItem visibilityItem;
        private IntPtr handle;
        private bool dragging,moved,closing,walking;
        private Point dragStart,windowStart,lastCursor;
        private double previousTick,lastCursorMove,nextWalk=8,walkTarget;
        private int shownRow=-1,shownCol=-1;
        public AnimationPlayer Animation { get; private set; }
        public PetSettings Settings { get; private set; }
        public SettingsStore Store { get; private set; }
        public SkinCatalog Catalog { get; private set; }
        public SkinDefinition Skin { get; private set; }
        public bool Paused { get; private set; }
        public string Notice { get; private set; }
        public ConversationSession Conversation
        {
            get
            {
                if(conversation==null)
                {
                    var api=new ApiClient();var voice=new SpeechOutput(api,Path.Combine(Store.DirectoryPath,"audio-cache"),Dispatcher);
                    conversation=new ConversationSession(api,voice,delegate {return new AudioRecorder();});conversation.SelectPersona(Skin.Id);conversation.StateChanged+=ConversationChanged;
                }
                return conversation;
            }
        }
        public bool LiveActive {get{return live!=null && live.IsActive;}}
        public bool DialogueBusy {get{return LiveActive || (conversation!=null && conversation.IsBusy);}}
        public GeminiLiveSession Live
        {
            get
            {
                if(live==null)
                {
                    live=new GeminiLiveSession();live.StateChanged+=delegate {if(!closing)Dispatcher.BeginInvoke(new Action(ConversationChanged));};
                    live.TurnCompleted+=delegate(string user,string reply){string id=liveSkinId;if(!closing)Dispatcher.BeginInvoke(new Action(delegate {if(!closing)Conversation.AddLiveTurn(id,user,reply);}));};
                }
                return live;
            }
        }
        public async Task StartLiveAsync()
        {if(DialogueBusy)throw new InvalidOperationException("请先停止当前对话再开始 Live。");liveSkinId=Skin.Id;await Live.StartAsync(Settings,Settings.SystemPrompt.Replace("{pet_name}",Skin.Name));}
        public async Task StopDialogueAsync(){if(live!=null)await live.StopAsync();if(conversation!=null)await conversation.StopAsync();}

        public PetWindow(SpriteAtlas source,PetSettings settings,SettingsStore store,EventWaitHandle wakeSignal)
        {
            atlas=source; Settings=settings; Store=store; wake=wakeSignal; Settings.Normalize();
            Catalog=new SkinCatalog(store.DirectoryPath); Skin=Catalog.Find(Settings.SkinId);
            try { if(Skin.Id!="dafeiyu") atlas=Skin.Load(); }
            catch { Skin=Catalog.Find("dafeiyu"); Notice="之前导入的外观无法读取，已切回大肥鱼。"; }
            PersonaProfiles.Activate(Settings,Skin); Animation=new AnimationPlayer();
            Title="桌面伙伴 · "+Skin.Name; AllowsTransparency=true; WindowStyle=WindowStyle.None;
            ResizeMode=ResizeMode.NoResize; Background=Brushes.Transparent; ShowInTaskbar=false;
            ShowActivated=false; Focusable=false; Topmost=Settings.Topmost;
            Width=192*Settings.Scale; Height=208*Settings.Scale;
            image=new Image { Stretch=Stretch.Fill,Cursor=Cursors.Hand,ToolTip="左键拖动 · 单击招呼 · 双击快速对话 · 右键菜单" };
            RenderOptions.SetBitmapScalingMode(image,BitmapScalingMode.HighQuality); Content=image;
            image.MouseLeftButtonDown+=BeginDrag; image.MouseMove+=Drag; image.MouseLeftButtonUp+=EndDrag;
            image.LostMouseCapture+=delegate { if(dragging) { dragging=false; walking=false; SavePosition(); Animation.Play("idle",0); } };
            ContextMenu=CreateMenu();
            LocationChanged+=delegate {if(quick!=null && quick.IsVisible)quick.FollowPet();};
            SourceInitialized+=delegate
            {
                handle=new WindowInteropHelper(this).Handle;
                HwndSource.FromHwnd(handle).AddHook(WindowMessage);
                DesktopInterop.MakeToolWindow(handle);
            };
            Loaded+=delegate
            {
                if(Settings.X.HasValue && Settings.Y.HasValue) DesktopInterop.Move(handle,new Point(Settings.X.Value,Settings.Y.Value));
                else ReturnHome();
                DesktopInterop.ClampWindow(handle); lastCursor=DesktopInterop.Cursor(); previousTick=watch.Elapsed.TotalSeconds;
                UpdateFrame(); timer.Start();
            };
            using(Stream stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Dafeiyu.Icon")) trayIcon=(System.Drawing.Icon)new System.Drawing.Icon(stream).Clone();
            trayMenu=new Forms.ContextMenuStrip();
            visibilityItem=new Forms.ToolStripMenuItem("显示 / 隐藏伙伴"){Checked=true};visibilityItem.Click+=delegate {Dispatcher.BeginInvoke(new Action(delegate {if(IsVisible)HidePet();else RestorePet();}));};trayMenu.Items.Add(visibilityItem);
            trayMenu.Opening+=delegate {visibilityItem.Checked=IsVisible;};
            AddTray("快速对话",ShowQuickChat);AddTray("打开完整对话",ShowChat); AddTray("外观与设置",ShowSettings);
            AddTray("回到屏幕角落",ReturnHome);
            trayMenu.Items.Add(new Forms.ToolStripSeparator()); AddTray("退出桌宠",Close);
            tray=new Forms.NotifyIcon { Icon=trayIcon,Text="桌面伙伴 · "+Skin.Name,ContextMenuStrip=trayMenu,Visible=true };
            tray.DoubleClick+=delegate { Dispatcher.BeginInvoke(new Action(delegate { RestorePet(); ShowSettings(); })); };
            timer=new DispatcherTimer(DispatcherPriority.Background) { Interval=TimeSpan.FromMilliseconds(40) };
            timer.Tick+=Tick; Closing+=OnClosing; UpdateFrame();
        }
        private void AddTray(string text,Action action)
        { trayMenu.Items.Add(text,null,delegate { Dispatcher.BeginInvoke(action); }); }
        private ContextMenu CreateMenu()
        {
            var menu=new ContextMenu();
            AddMenu(menu,"快速对话…",ShowQuickChat);AddMenu(menu,"打开完整对话…",ShowChat); AddMenu(menu,"外观与设置…",ShowSettings); menu.Items.Add(new Separator());
            var actions=new MenuItem { Header="做个动作" };
            for(int i=0;i<AnimationPlayer.States.Length;i++)
            {
                string state=AnimationPlayer.States[i]; var item=new MenuItem { Header=AnimationPlayer.Labels[i] };
                item.Click+=delegate { PlayAction(state,state=="jumping" ? .7 : 2); }; actions.Items.Add(item);
            }
            menu.Items.Add(actions);
            var pause=new MenuItem { Header="暂停动画",IsCheckable=true };
            pause.Click+=delegate { SetPaused(pause.IsChecked); }; menu.Items.Add(pause);
            var follow=new MenuItem { Header="视线跟随鼠标",IsCheckable=true };
            follow.Click+=delegate { Settings.FollowCursor=follow.IsChecked; ApplySettings(); }; menu.Items.Add(follow);
            var wander=new MenuItem { Header="偶尔散步",IsCheckable=true };
            wander.Click+=delegate { Settings.Wander=wander.IsChecked; ApplySettings(); }; menu.Items.Add(wander);
            var top=new MenuItem { Header="保持置顶",IsCheckable=true };
            top.Click+=delegate { Settings.Topmost=top.IsChecked; ApplySettings(); }; menu.Items.Add(top);
            menu.Opened+=delegate { pause.IsChecked=Paused; follow.IsChecked=Settings.FollowCursor; wander.IsChecked=Settings.Wander; top.IsChecked=Settings.Topmost; };
            menu.Items.Add(new Separator()); AddMenu(menu,"回到屏幕角落",ReturnHome); AddMenu(menu,"隐藏到托盘",HidePet); AddMenu(menu,"退出桌宠",Close);
            return menu;
        }
        private static void AddMenu(ContextMenu menu,string text,Action action)
        { var item=new MenuItem { Header=text }; item.Click+=delegate { action(); }; menu.Items.Add(item); }
        private void Tick(object sender,EventArgs args)
        {
            if(closing) return;
            if(wake!=null && wake.WaitOne(0)) { RestorePet(); ShowSettings(); }
            double now=watch.Elapsed.TotalSeconds,delta=Math.Min(.2,Math.Max(0,now-previousTick)); previousTick=now;
            if(!IsVisible || Paused) return;
            if(quick!=null && quick.IsVisible)quick.FollowPet();
            Point cursor=DesktopInterop.Cursor(); if((cursor-lastCursor).Length>2) { lastCursor=cursor; lastCursorMove=now; }
            if(!dragging) Wander(delta,now,cursor);
            Animation.Advance(delta); UpdateFrame();
        }
        private void Wander(double delta,double now,Point cursor)
        {
            if(DialogueBusy || (quick!=null && quick.IsVisible)) return;
            if(!Settings.Wander && walking) { walking=false; Animation.Play("idle",0); SavePosition(); }
            if(walking)
            {
                Rect bounds=DesktopInterop.Bounds(handle),area=DesktopInterop.WorkArea(handle);
                walkTarget=PetMath.ClampPosition(walkTarget,bounds.Top,bounds.Width,bounds.Height,area).X;
                double dx=walkTarget-bounds.Left;
                double step=70*delta*Settings.Scale;
                if(Math.Abs(dx)<=step) { DesktopInterop.Move(handle,new Point(walkTarget,bounds.Top)); walking=false; Animation.Play("idle",0); SavePosition(); nextWalk=now+10+random.Next(12); }
                else DesktopInterop.Move(handle,new Point(bounds.Left+Math.Sign(dx)*step,bounds.Top));
                return;
            }
            if(Settings.Wander && Animation.State=="idle" && now>=nextWalk && !IsMouseOver && (preferences==null || !preferences.IsVisible))
            {
                Rect bounds=DesktopInterop.Bounds(handle),area=DesktopInterop.WorkArea(handle);
                double desired=bounds.Left+(random.Next(2)==0 ? -1 : 1)*(80+random.Next(120));
                walkTarget=PetMath.ClampPosition(desired,bounds.Top,bounds.Width,bounds.Height,area).X;
                if(Math.Abs(walkTarget-bounds.Left)>10) { walking=true; Animation.Play(walkTarget>bounds.Left ? "running-right" : "running-left",0); }
                nextWalk=now+12;
            }
        }
        private void UpdateFrame()
        {
            int row=Animation.Row,col=Animation.Frame;
            if(!Paused && !dragging && !walking && Settings.FollowCursor && Animation.State=="idle" && handle!=IntPtr.Zero && watch.Elapsed.TotalSeconds-lastCursorMove<1.4)
            {
                Rect bounds=DesktopInterop.Bounds(handle); Point cursor=DesktopInterop.Cursor();
                int direction=PetMath.LookDirection(cursor.X-(bounds.X+bounds.Width*.5),cursor.Y-(bounds.Y+bounds.Height*.4));
                if(direction>=0) { row=9+direction/8; col=direction%8; }
            }
            if(row==shownRow && col==shownCol) return;
            shownRow=row; shownCol=col; image.Source=atlas.GetFrame(row,col);
        }
        private void BeginDrag(object sender,MouseButtonEventArgs args)
        {
            if(args.ClickCount==2) { ShowQuickChat(); args.Handled=true; return; }
            walking=false; dragStart=DesktopInterop.Cursor(); Rect bounds=DesktopInterop.Bounds(handle); windowStart=bounds.TopLeft;
            moved=false; dragging=true; image.CaptureMouse(); args.Handled=true;
        }
        private void Drag(object sender,MouseEventArgs args)
        {
            if(!dragging || args.LeftButton!=MouseButtonState.Pressed) return;
            Point cursor=DesktopInterop.Cursor(); Vector delta=cursor-dragStart;
            if(delta.Length<4 && !moved) return;
            moved=true; Rect bounds=DesktopInterop.Bounds(handle); Rect area=DesktopInterop.WorkArea(cursor);
            DesktopInterop.Move(handle,PetMath.ClampPosition(windowStart.X+delta.X,windowStart.Y+delta.Y,bounds.Width,bounds.Height,area));
            if(quick!=null && quick.IsVisible)quick.FollowPet();
            if(!Paused) { string state=delta.X<0 ? "running-left":"running-right"; if(Animation.State!=state) Animation.Play(state,0); }
        }
        private void EndDrag(object sender,MouseButtonEventArgs args)
        {
            if(!dragging) return; dragging=false; image.ReleaseMouseCapture(); SavePosition();
            if(!Paused) PlayAction("waving",.85); args.Handled=true;
        }
        private IntPtr WindowMessage(IntPtr hwnd,int message,IntPtr wParam,IntPtr lParam,ref bool handled)
        {
            if(message==0x21) { handled=true; return new IntPtr(3); }
            if(message==0x84 && !dragging)
            {
                long p=lParam.ToInt64(); int x=(short)(p&0xffff),y=(short)((p>>16)&0xffff); Rect b=DesktopInterop.Bounds(hwnd);
                int sx=(int)((x-b.Left)/b.Width*atlas.CellWidth),sy=(int)((y-b.Top)/b.Height*atlas.CellHeight);
                if(atlas.AlphaAt(Math.Max(0,shownRow),Math.Max(0,shownCol),sx,sy)<12) { handled=true; return new IntPtr(-1); }
            }
            if(message==0x7e || message==0x2e0) Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(delegate { if(!closing) DesktopInterop.ClampWindow(handle); }));
            return IntPtr.Zero;
        }
        public void PlayAction(string state,double duration)
        { walking=false; Paused=false; Animation.Play(state,duration); UpdateFrame(); if(preferences!=null) preferences.RefreshState(); }
        public void SetPaused(bool value)
        { Paused=value; if(value && walking) { walking=false; Animation.Play("idle",0); SavePosition(); } if(preferences!=null) preferences.RefreshState(); }
        public void ApplySettings()
        {
            Settings.Normalize(); Rect before=handle==IntPtr.Zero ? Rect.Empty : DesktopInterop.Bounds(handle);
            Width=192*Settings.Scale; Height=208*Settings.Scale; Topmost=Settings.Topmost;
            if(!Settings.Wander && walking) { walking=false; Animation.Play("idle",0); }
            if(handle!=IntPtr.Zero) Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(delegate
            {
                if(closing) return; Rect after=DesktopInterop.Bounds(handle);
                DesktopInterop.Move(handle,new Point(before.Left,before.Bottom-after.Height)); DesktopInterop.ClampWindow(handle); SavePosition();
            }));
            UpdateFrame(); SaveSettings(); if(preferences!=null) preferences.RefreshState();
        }
        public bool SelectSkin(SkinDefinition skin)
        {
            if(skin.Id==Skin.Id)return true;
            if(DialogueBusy){Notice="请先停止对话或结束 Live，再切换伙伴。";return false;}
            try
            {
                var replacement=skin.Load(); atlas=replacement; Skin=skin;PersonaProfiles.Activate(Settings,skin);if(conversation!=null)conversation.SelectPersona(skin.Id);
                shownRow=shownCol=-1; walking=false; Animation.Play("idle",0); UpdateFrame();
                Title="桌面伙伴 · "+skin.Name; tray.Text=Title;if(chat!=null)chat.ReloadHistory();if(quick!=null)quick.RefreshPersona();RefreshChatAppearance(); SaveSettings(); return true;
            }
            catch(Exception e)
            {
                if(!(e is IOException || e is UnauthorizedAccessException || e is InvalidOperationException || e is NotSupportedException || e is System.IO.FileFormatException)) throw;
                Notice="外观无法载入，请检查 PNG 精灵图。"; return false;
            }
        }
        public void ShowSettings()
        { ShowSettings(false); }
        public void ShowSettings(bool connections)
        {
            if(preferences==null) { preferences=new SettingsWindow(this); preferences.Closed+=delegate { preferences=null; }; }
            if(!preferences.IsVisible) preferences.Show(); preferences.Activate(); preferences.RefreshState();
            if(connections) preferences.SelectConnections();
        }
        public void ShowChat()
        {if(closing)return;if(chat==null){chat=new ChatWindow(this);chat.Closed+=delegate {chat=null;};}if(!chat.IsVisible)chat.Show();if(chat.WindowState==WindowState.Minimized)chat.WindowState=WindowState.Normal;chat.Activate();}
        public void ShowQuickChat()
        {if(closing)return;RestorePet();if(quick==null){quick=new QuickChatWindow(this);quick.Closed+=delegate {quick=null;};}if(!quick.IsVisible)quick.Show();quick.FollowPet();quick.Activate();quick.FocusInput();}
        public void RefreshChatAppearance(){if(chat!=null)chat.RefreshAppearance();if(quick!=null)quick.RefreshPersona();}
        public void ReloadConversation(){if(chat!=null)chat.ReloadHistory();if(quick!=null)quick.RefreshPersona();}
        public void UpdateVoiceVolume(int volume){if(conversation!=null)conversation.SetVolume(volume);if(live!=null)live.SetVolume(volume);}
        public void StopReplySpeech(){if(conversation!=null)conversation.StopSpeech();}
        private void ConversationChanged()
        {
            if(closing)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(ConversationChanged));return;}
            walking=false;if(Paused)return;var stage=conversation==null ? ConversationStage.Idle : conversation.Stage;string state=LiveActive ? "waiting" : stage==ConversationStage.Recording?"waiting":stage==ConversationStage.Recognizing?"review":stage==ConversationStage.Thinking?"running":stage==ConversationStage.Speaking?"waving":"idle";Animation.Play(state,0);UpdateFrame();
        }
        public void HidePet() { walking=false; Animation.Play("idle",0); SavePosition();if(quick!=null)quick.Hide(); Hide();visibilityItem.Checked=false; timer.Interval=TimeSpan.FromMilliseconds(250); }
        public void RestorePet() { if(!IsVisible) Show();visibilityItem.Checked=true; DesktopInterop.ClampWindow(handle); timer.Interval=TimeSpan.FromMilliseconds(40); previousTick=watch.Elapsed.TotalSeconds; }
        public void ReturnHome()
        {
            if(handle==IntPtr.Zero) return; walking=false; Animation.Play("idle",0); UpdateFrame();
            Rect bounds=DesktopInterop.Bounds(handle),area=DesktopInterop.WorkArea(handle);
            DesktopInterop.Move(handle,PetMath.ClampPosition(area.Right-bounds.Width-28,area.Bottom-bounds.Height-16,bounds.Width,bounds.Height,area)); SavePosition();
        }
        public void SaveSettings()
        {
            try { Store.Save(Settings); Notice=Store.Warning; }
            catch(Exception e) { if(!(e is IOException || e is UnauthorizedAccessException)) throw; Notice="设置暂时无法保存，请将桌宠放在可写入的文件夹。"; }
            if(preferences!=null) preferences.ShowNotice(Notice);
        }
        private void SavePosition()
        { if(handle==IntPtr.Zero) return; Rect bounds=DesktopInterop.Bounds(handle); Settings.X=bounds.Left; Settings.Y=bounds.Top; SaveSettings(); }
        private async void OnClosing(object sender,System.ComponentModel.CancelEventArgs args)
        {
            if(LiveActive && !closeAfterLive){args.Cancel=true;if(closing)return;closing=true;try{await live.StopAsync();}catch(OperationCanceledException){}closeAfterLive=true;Close();return;}
            closing=true; timer.Stop(); SavePosition();if(quick!=null)quick.Close(); if(chat!=null)chat.Close();if(live!=null)live.Dispose();if(conversation!=null){conversation.StateChanged-=ConversationChanged;conversation.Dispose();}if(preferences!=null) preferences.Close();
            tray.Visible=false; tray.Dispose(); trayMenu.Dispose(); trayIcon.Dispose();
        }
    }
}
