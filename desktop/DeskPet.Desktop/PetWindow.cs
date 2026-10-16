using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Threading;
using Dafeiyu;
using DeskPet.Platform.Mac;

namespace DeskPet.Desktop;

internal sealed class PetWindow : Window
{
    private readonly DesktopController owner;
    private readonly SpriteAssets assets = new();
    private readonly SpriteView sprite;
    private readonly DispatcherTimer timer;
    private readonly Stopwatch clock = Stopwatch.StartNew();
    private readonly Random random = new();
    private readonly DropMotion drop = new();
    private readonly DropReaction reaction;
    private readonly IdleSleepSchedule sleep;
    private readonly GazeController gaze = new();
    private string action = "idle";
    private bool original, dragging, moved, nativeIgnored;
    private Point press;
    private PixelPoint start;
    private double lastTime, frameElapsed, wanderAt, runUntil;
    private int direction = 1;
    public bool Paused { get; set; }
    public PetWindow(DesktopController controller)
    {
        owner = controller;
        reaction = new DropReaction(random.NextDouble);
        sleep = new IdleSleepSchedule(0, random.NextDouble);
        SystemDecorations = SystemDecorations.None;
        TransparencyLevelHint = new[] { WindowTransparencyLevel.Transparent };
        Background = Brushes.Transparent; CanResize = false; ShowInTaskbar = false;
        Title = "DeskPet";
        sprite = new SpriteView { Assets = assets, Skin = owner.Skin };
        Content = sprite;
        PointerPressed += Pressed; PointerMoved += Moved; PointerReleased += Released;
        PointerCaptureLost += (_, _) => { if (dragging) FinishDrag(); };
        DoubleTapped += (_, _) => owner.ShowChat(true);
        ContextMenu = BuildMenu();
        Opened += (_, _) => { ApplySettings(); Clamp(); ConfigureNative(false); };
        Closed += (_, _) => { timer.Stop(); assets.Dispose(); };
        timer = new DispatcherTimer(TimeSpan.FromMilliseconds(16), DispatcherPriority.Render, (_, _) => Tick());
        Play("idle", false);
    }
    public void ApplySettings()
    {
        Width = 192 * owner.Settings.Scale; Height = 208 * owner.Settings.Scale;
        Topmost = owner.Settings.Topmost;
        if (owner.Settings.X is double x && owner.Settings.Y is double y && !IsVisible) Position = new PixelPoint((int)x, (int)y);
        Clamp(); ConfigureNative(false);
    }
    public void SelectSkin()
    {
        drop.Cancel(); sprite.Skin = owner.Skin; Play("idle", false); ContextMenu = BuildMenu();
    }
    private void ConfigureNative(bool ignored)
    {
        if (!OperatingSystem.IsMacOS() || !IsVisible) return;
        var handle = TryGetPlatformHandle();
        if (handle == null) return;
        MacServices.ConfigureWindow(handle.Handle, owner.Settings.Topmost, ignored, true); nativeIgnored = ignored;
    }
    private ContextMenu BuildMenu()
    {
        var menu = new ContextMenu(); var items = new List<object>();
        void Add(string label, Action command) { var item = new MenuItem { Header = label }; item.Click += (_, _) => command(); items.Add(item); }
        Add("快速对话", () => owner.ShowChat(true)); Add("完整聊天与队列", () => owner.ShowChat(false));
        Add("四格轮盘", ShowPalette); Add("喂零食", () => Interact("feeding")); Add("摸摸", () => Interact("petting"));
        Add("小憩 / 唤醒", () => { if (action == "sleeping") Play("idle", false); else Interact("sleeping"); });
        Add("伙伴招牌", () => Interact("special")); Add("第二招牌", () => Interact("special2"));
        Add("暂停 / 继续", () => Paused = !Paused); Add("隐藏伙伴", Hide);
        items.Add(new Separator());
        foreach (var skin in owner.Skins) Add("切换 · " + skin.Name, () => owner.Run(() => owner.SelectSkinAsync(skin)));
        Add("设置", owner.ShowSettings); Add("使用引导", owner.ShowGuide); Add("关于与更新", owner.ShowAbout); Add("退出", () => owner.Run(owner.ExitAsync));
        menu.ItemsSource = items; return menu;
    }
    public void ShowPalette()
    {
        var grid = new Grid { ColumnDefinitions = new ColumnDefinitions("*,*"), RowDefinitions = new RowDefinitions("*,*") };
        var choices = new (string, Action)[] { ("聊天", () => owner.ShowChat(true)), ("喂零食", () => Interact("feeding")), ("小憩", () => Interact("sleeping")), ("招牌动作", () => Interact(random.Next(2) == 0 ? "special" : "special2")) };
        var window = Ui.Window("伙伴互动", grid, 290, 280); window.Topmost = true;
        for (int i = 0; i < choices.Length; i++) { var c = choices[i]; var b = Ui.Button(c.Item1, () => { c.Item2(); window.Close(); }); b.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Stretch; b.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Stretch; b.Margin = new Thickness(5); Grid.SetColumn(b, i % 2); Grid.SetRow(b, i / 2); grid.Children.Add(b); }
        window.Show();
    }
    public void Interact(string name)
    {
        if (owner.GuideLocked || dragging) return;
        sleep.Touch(clock.Elapsed.TotalSeconds); drop.Cancel();
        if (!owner.Skin.BuiltIn && !owner.Skin.Clips.ContainsKey(name) && !SpriteAssets.Interactions.Contains(name)) return;
        Play(name, owner.Skin.BuiltIn && random.Next(2) == 0);
    }
    private void Play(string name, bool useOriginal)
    {
        action = name; original = useOriginal; sprite.Frame = 0; frameElapsed = 0;
        sprite.Clip = assets.Resolve(owner.Skin, name, useOriginal);
        sprite.InvalidateVisual();
    }
    private void Pressed(object? sender, PointerPressedEventArgs e)
    {
        if (owner.GuideLocked || !sprite.Hits(e.GetPosition(sprite))) return;
        sleep.Touch(clock.Elapsed.TotalSeconds);
        var point = e.GetCurrentPoint(this);
        if (!point.Properties.IsLeftButtonPressed || owner.Settings.LockPosition) return;
        dragging = true; moved = false; press = e.GetPosition(this); start = Position; drop.Cancel();
        original = owner.Skin.BuiltIn && random.Next(2) == 0; Play("lifted", original);
        e.Pointer.Capture(this); e.Handled = true;
    }
    private void Moved(object? sender, PointerEventArgs e)
    {
        if (!dragging) return;
        var local = e.GetPosition(this); var scale = RenderScaling;
        var dx = (local.X - press.X) * scale; var dy = (local.Y - press.Y) * scale;
        if (Math.Abs(dx) + Math.Abs(dy) > 3) moved = true;
        Position = new PixelPoint(Position.X + (int)Math.Round(dx), Position.Y + (int)Math.Round(dy)); Clamp();
    }
    private void Released(object? sender, PointerReleasedEventArgs e)
    {
        if (!dragging) return;
        FinishDrag(); e.Pointer.Capture(null); e.Handled = true;
    }
    private void FinishDrag()
    {
        dragging = false;
        if (!moved) { Play("poked", false); return; }
        var stumble = reaction.TryStumble(true, owner.Settings.StumbleEnabled, owner.Skin.BuiltIn, clock.Elapsed.TotalSeconds);
        Play(stumble ? "stumble" : "landing", original);
        var area = WorkArea();
        drop.Start(Position.Y, Height * RenderScaling, area.Bottom);
        SavePosition();
    }
    private PixelRect WorkArea() => (Screens.ScreenFromWindow(this) ?? Screens.Primary)?.WorkingArea ?? new PixelRect(0, 0, 1920, 1080);
    public void Clamp()
    {
        var area = WorkArea(); var w = (int)(Width * RenderScaling); var h = (int)(Height * RenderScaling);
        var next = new PixelPoint(Math.Clamp(Position.X, area.X, Math.Max(area.X, area.Right - w)), Math.Clamp(Position.Y, area.Y, Math.Max(area.Y, area.Bottom - h)));
        if (Position != next) Position = next;
    }
    private void SavePosition() { owner.Settings.X = Position.X; owner.Settings.Y = Position.Y; owner.Save(); }
    private void Tick()
    {
        var now = clock.Elapsed.TotalSeconds; var dt = Math.Clamp(now - lastTime, 0, .125); lastTime = now;
        if (Paused || !IsVisible) return;
        if (drop.Active) { drop.Advance(dt); Position = new PixelPoint(Position.X, (int)Math.Round(drop.Position)); if (!drop.Active) SavePosition(); }
        if (!dragging)
        {
            var idle = OperatingSystem.IsMacOS() ? MacServices.GetIdleTime().TotalSeconds : 0;
            if (sleep.TrySleep(now, idle, !owner.Session.IsBusy && !owner.Live.IsActive && action == "idle" && !owner.GuideLocked, owner.Settings.AutoSleepEnabled)) Interact("sleeping");
        }
        if (!dragging && !drop.Active && action is "idle" or "running-right" or "running-left" && !owner.Session.IsBusy && !owner.GuideLocked && !owner.Settings.LockPosition)
        {
            if (owner.Settings.FollowCursor && OperatingSystem.IsMacOS())
            {
                var pointer = MacServices.GetPointerPosition(); var dx = pointer.X - Position.X - Width * RenderScaling / 2;
                if (Math.Abs(dx) > Width) { direction = Math.Sign(dx); runUntil = now + .2; }
            }
            if (owner.Settings.Wander && now >= wanderAt) { direction = random.Next(2) == 0 ? -1 : 1; runUntil = now + 2 + random.NextDouble() * 3; wanderAt = now + 15 + random.NextDouble() * 25; }
            if (now < runUntil)
            {
                var desired = direction < 0 ? "running-left" : "running-right";
                if (action != desired) Play(desired, false);
                Position = new PixelPoint(Position.X + (int)Math.Round(direction * 95 * dt * RenderScaling), Position.Y); Clamp();
            }
            else if (action != "idle") { Play("idle", false); SavePosition(); }
        }
        frameElapsed += dt;
        var spec = sprite.Clip!;
        if (frameElapsed >= 1.0 / spec.Fps)
        {
            // One displayed frame per tick. A suspended UI never skips artwork.
            frameElapsed = Math.Min(frameElapsed - 1.0 / spec.Fps, 1.0 / spec.Fps);
            sprite.Frame++;
            if (sprite.Frame >= spec.Frames)
            {
                if (spec.Loop) sprite.Frame = 0;
                else if (action == "feeding") Play("happy", original);
                else Play("idle", false);
            }
        }
        if (OperatingSystem.IsMacOS())
        {
            var pointer = MacServices.GetPointerPosition();
            var local = new Point((pointer.X - Position.X) / RenderScaling, (pointer.Y - Position.Y) / RenderScaling);
            if (action == "idle" && !owner.Settings.Wander && !owner.Settings.FollowCursor)
            {
                gaze.Advance(pointer.X - Position.X - Width * RenderScaling / 2, pointer.Y - Position.Y - Height * RenderScaling / 2, dt);
                sprite.Clip = assets.Gaze(owner.Skin); sprite.Frame = gaze.Frame(1);
            }
            else if (action == "idle" && sprite.Clip?.Frames == 16) Play("idle", false);
            var ignored = !dragging && (owner.Settings.ClickThrough || !sprite.Hits(local));
            if (ignored != nativeIgnored) ConfigureNative(ignored);
        }
        sprite.InvalidateVisual();
    }
}
