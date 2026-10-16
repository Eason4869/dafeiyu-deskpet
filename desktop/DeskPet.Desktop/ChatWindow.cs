using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Dafeiyu;

namespace DeskPet.Desktop;

internal sealed class ChatWindow : Window
{
    private readonly DesktopController owner;
    private readonly bool quick;
    private readonly TextBox draft = Ui.Input("", "输入消息；Ctrl/⌘ + Enter 发送");
    private readonly TextBlock status = Ui.Text("");
    private readonly StackPanel messages = Ui.Column();
    private readonly StackPanel queue = Ui.Column();
    private readonly ScrollViewer history;
    private readonly ComboBox models = new() { MinWidth = 180 };
    private readonly Dictionary<string, string> drafts = new();
    private string persona;
    private sealed record ModelChoice(string Provider, string Model, string Label) { public override string ToString() => Label; }
    public ChatWindow(DesktopController controller, bool isQuick)
    {
        owner = controller; quick = isQuick; persona = owner.Skin.Id;
        Title = quick ? "DeskPet · 快聊" : "DeskPet · 聊天与队列"; Width = quick ? 470 : 880; Height = quick ? 520 : 730;
        MinWidth = 390; MinHeight = 400;
        draft.AcceptsReturn = true; draft.MinHeight = 72; draft.MaxHeight = 160; draft.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
        draft.KeyDown += (s, e) => { if (e.Key == Key.Enter && (e.KeyModifiers.HasFlag(KeyModifiers.Control) || e.KeyModifiers.HasFlag(KeyModifiers.Meta))) { Send(); e.Handled = true; } };
        history = new ScrollViewer { Content = messages, VerticalScrollBarVisibility = Avalonia.Controls.Primitives.ScrollBarVisibility.Auto };
        var root = new DockPanel { Margin = new Thickness(16), LastChildFill = true };
        var header = Ui.Column(Ui.Row(models, Ui.Button("设置", owner.ShowSettings), Ui.Button(quick ? "完整聊天" : "快聊", () => owner.ShowChat(!quick))), status);
        DockPanel.SetDock(header, Dock.Top); root.Children.Add(header);
        var buttons = Ui.Row(Ui.Button("发送 / 排队", Send), Ui.AsyncButton("停止", () => owner.Queue.PauseAsync(), owner.Error), Ui.AsyncButton("听写 / 发送录音", DictateAsync, owner.Error), Ui.Button("Live", () => new LiveWindow(owner).Show()));
        var composer = Ui.Column(draft, buttons); DockPanel.SetDock(composer, Dock.Bottom); root.Children.Add(composer);
        if (!quick)
        {
            var sidebar = Ui.Column(Ui.Text("待发送队列", 17), Ui.Row(Ui.Button("继续", () => Try(owner.Queue.Resume)), Ui.Button("暂停", () => Try(owner.Queue.Hold))), new ScrollViewer { Content = queue, MaxHeight = 400 }, Ui.AsyncButton("导出聊天", ExportAsync, owner.Error), Ui.Button("清空记录", ConfirmClear));
            sidebar.Width = 245; sidebar.Margin = new Thickness(18, 8, 0, 8); DockPanel.SetDock(sidebar, Dock.Right); root.Children.Add(sidebar);
        }
        root.Children.Add(history); Content = root;
        models.SelectionChanged += (_, _) =>
        {
            if (models.SelectedItem is not ModelChoice item || owner.Settings.ActiveTextProviderId == item.Provider && owner.Settings.Text.Model == item.Model) return;
            if (owner.Session.IsBusy || owner.Live.IsActive) { owner.Error(new InvalidOperationException("请先停止当前对话再切换模型。")); RefreshModels(); return; }
            owner.Settings.SelectTextModel(item.Provider, item.Model); owner.Save();
        };
        owner.Changed += Refresh; Closed += (_, _) => { drafts[persona] = draft.Text ?? ""; owner.Changed -= Refresh; };
        Refresh();
    }
    private void Try(Action action) { try { action(); } catch (Exception e) { owner.Error(e); } }
    private void RefreshModels()
    {
        var all = owner.Settings.TextProviders.SelectMany(p => p.Models.Select(m => new ModelChoice(p.Id, m, p.Name + " · " + p.DisplayName(m)))).ToList();
        models.ItemsSource = all; models.SelectedItem = all.FirstOrDefault(m => m.Provider == owner.Settings.ActiveTextProviderId && m.Model == owner.Settings.Text.Model);
    }
    private void Send()
    {
        if (owner.Live.IsActive) { owner.Error(new InvalidOperationException("请先结束 Live 语音对话。")); return; }
        var text = (draft.Text ?? "").Trim(); if (text.Length == 0) return;
        try { owner.Queue.Enqueue(text); draft.Text = ""; } catch (Exception e) { owner.Error(e); }
    }
    private async Task DictateAsync()
    {
        if (owner.Live.IsActive) throw new InvalidOperationException("请先结束 Live。");
        if (owner.Session.Stage == ConversationStage.Recording) { await owner.Session.FinishRecordingAsync(owner.Skin.Name); return; }
        if (owner.Session.IsBusy) throw new InvalidOperationException("请先停止当前对话。");
        if (!await DeskPet.Platform.Mac.MacServices.RequestMicrophoneAccessAsync()) throw new InvalidOperationException("麦克风权限未授予。请在系统设置 → 隐私与安全性 → 麦克风中允许 DeskPet。");
        owner.Queue.Hold(); owner.Session.StartRecording(owner.Settings);
    }
    private void Refresh()
    {
        if (persona != owner.Skin.Id) { drafts[persona] = draft.Text ?? ""; persona = owner.Skin.Id; draft.Text = drafts.GetValueOrDefault(persona, ""); }
        status.Text = owner.Skin.Name + " · " + owner.Session.Stage + " · " + owner.Session.LastNotice + " " + owner.Queue.Notice + " " + owner.Session.HistoryWarning;
        Title = "DeskPet · " + owner.Skin.Name + (quick ? " · 快聊" : " · 完整聊天");
        RefreshModels(); messages.Children.Clear();
        foreach (var message in owner.Session.History.Messages.TakeLast(quick ? 10 : 200))
        {
            var body = new SelectableTextBlock { Text = message.Content, TextWrapping = Avalonia.Media.TextWrapping.Wrap };
            messages.Children.Add(new Border { Padding = new Thickness(12), CornerRadius = new CornerRadius(10), Background = message.Role == "user" ? Avalonia.Media.Brushes.SteelBlue : Avalonia.Media.Brushes.DimGray, Child = Ui.Column(Ui.Text((message.Role == "user" ? "你" : owner.Skin.Name) + " · " + message.Status, 11), body) });
        }
        Dispatcher.UIThread.Post(() => history.ScrollToEnd());
        if (quick) return;
        queue.Children.Clear(); var items = owner.Queue.Items.ToList();
        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i]; var index = i;
            var edit = Ui.Input(item.Text); edit.AcceptsReturn = true; edit.TextWrapping = Avalonia.Media.TextWrapping.Wrap;
            queue.Children.Add(Ui.Column(edit,
                Ui.Row(Ui.Button("保存", () => Try(() => owner.Queue.Edit(item.Id, edit.Text ?? ""))), Ui.Button("删除", () => Try(() => owner.Queue.Remove(item.Id))), Ui.Button("↑", () => { if (index > 0) Try(() => owner.Queue.MoveBefore(item.Id, items[index - 1].Id)); })),
                Ui.Row(Ui.Button("↓", () => { if (index < items.Count - 1) Try(() => owner.Queue.MoveBefore(item.Id, index + 2 < items.Count ? items[index + 2].Id : null!)); }), Ui.AsyncButton("立即引导", () => owner.Queue.InterruptAsync(edit.Text ?? "", item.Id), owner.Error))));
        }
    }
    private async Task ExportAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions { Title = "导出聊天记录", SuggestedFileName = owner.Skin.Id + "-chat.md", DefaultExtension = "md" });
        if (file == null) return; await using var stream = await file.OpenWriteAsync(); using var writer = new StreamWriter(stream); await writer.WriteAsync(owner.Session.ExportHistory());
    }
    private void ConfirmClear()
    {
        var confirm = Ui.Window("清空当前伙伴聊天记录", Ui.Text("此操作会删除当前伙伴保存的聊天记录。"), 420, 240);
        confirm.Content = new Border { Padding = new Thickness(20), Child = Ui.Column(Ui.Text("确定清空 " + owner.Skin.Name + " 的聊天记录？"), Ui.Button("确认清空", () => { Try(owner.Session.Clear); confirm.Close(); }), Ui.Button("取消", confirm.Close)) }; confirm.Show();
    }
}
