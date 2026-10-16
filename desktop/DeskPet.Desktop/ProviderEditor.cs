using Avalonia;
using Avalonia.Controls;
using Dafeiyu;

namespace DeskPet.Desktop;

internal sealed class ProviderEditor : StackPanel, IDisposable
{
    private readonly DesktopController owner;
    private readonly string kind;
    private readonly PetSettings view;
    private readonly ComboBox providers = new() { MinWidth = 220 };
    private readonly TextBox name = Ui.Input(), address = Ui.Input(), key = Ui.Input(), model = Ui.Input(), alias = Ui.Input(), proxy = Ui.Input();
    private readonly ComboBox protocol = new() { ItemsSource = new[] { "auto", "openai", "anthropic", "mimo", "gemini-live" } };
    private readonly ComboBox models = new() { MinWidth = 220 };
    private readonly CheckBox useProxy = new() { Content = "此厂商使用独立代理" }, stream = new() { Content = "流式回复" };
    private readonly StackPanel found = Ui.Column();
    private readonly TextBlock notice = Ui.Text("");
    private CancellationTokenSource? fetch;
    private TextProviderSettings? selected;
    private bool loading;
    private int revision;
    public ProviderEditor(DesktopController controller, string service)
    {
        owner = controller; kind = service; Spacing = 12;
        view = owner.Settings.Copy().ProviderView(kind); view.EnsureTextProviders();
        key.PasswordChar = '●';
        var presets = new ComboBox { ItemsSource = ServicePresets.For(kind), PlaceholderText = "选择服务预设" };
        presets.SelectionChanged += (_, _) =>
        {
            if (presets.SelectedItem is not ServicePreset preset) return;
            CancelFetch();
            if (!ServicePresets.SameOrigin(address.Text ?? "", preset.Url)) key.Text = "";
            name.Text = preset.Name; address.Text = preset.Url; model.Text = preset.Model; protocol.SelectedItem = preset.Protocol;
        };
        providers.SelectionChanged += (_, _) =>
        {
            if (loading) return; Capture(); CancelFetch(); selected = providers.SelectedItem as TextProviderSettings; LoadFields();
        };
        models.SelectionChanged += (_, _) =>
        {
            if (loading || selected == null || models.SelectedItem is not string id) return;
            model.Text = id; alias.Text = selected.ModelDisplayNames.GetValueOrDefault(id, "");
        };
        Children.Add(Ui.Row(providers, Ui.Button("新增厂商", AddProvider), Ui.Button("删除", RemoveProvider)));
        Children.Add(Ui.Text("每个厂商独立保存地址、密钥、代理和模型；点击保存并启用后生效。"));
        Children.Add(presets); Children.Add(Ui.Field("厂商名称", name)); Children.Add(Ui.Field("服务地址", address)); Children.Add(Ui.Field("API Key（保存在系统密钥保护下）", key));
        Children.Add(Ui.Field("协议", protocol)); Children.Add(Ui.Row(useProxy, stream)); Children.Add(Ui.Field("代理地址", proxy));
        Children.Add(Ui.Field("已添加模型", models)); Children.Add(Ui.Field("模型 ID（可手填）", model)); Children.Add(Ui.Field("模型显示名称（可留空）", alias));
        Children.Add(Ui.Row(Ui.Button("添加 / 更新模型", AddModel), Ui.Button("删除模型", RemoveModel)));
        Children.Add(Ui.Row(Ui.AsyncButton("获取模型列表", FetchAsync, owner.Error), Ui.Button("取消获取", CancelFetch)));
        Children.Add(new ScrollViewer { Content = found, MaxHeight = 220 }); Children.Add(notice);
        Children.Add(Ui.Button("保存并启用此厂商 / 模型", Save));
        ReloadProviders(view.ActiveTextProviderId);
    }
    private void ReloadProviders(string id)
    {
        loading = true; providers.ItemsSource = null; providers.ItemsSource = view.TextProviders; selected = view.TextProviders.FirstOrDefault(p => p.Id == id) ?? view.TextProviders[0]; providers.SelectedItem = selected; loading = false; LoadFields();
    }
    private void LoadFields()
    {
        if (selected == null) return; loading = true;
        name.Text = selected.Name; address.Text = selected.Connection.BaseUrl; key.Text = selected.Connection.ApiKey;
        model.Text = selected.Connection.Model; alias.Text = selected.ModelDisplayNames.GetValueOrDefault(selected.Connection.Model, "");
        protocol.SelectedItem = selected.Connection.Protocol; useProxy.IsChecked = selected.Connection.UseProxy; proxy.Text = selected.Connection.ProxyAddress; stream.IsChecked = selected.Connection.StreamReplies;
        models.ItemsSource = null; models.ItemsSource = selected.Models; models.SelectedItem = selected.Connection.Model; loading = false;
    }
    private void Capture()
    {
        if (selected == null || loading) return;
        selected.Name = name.Text?.Trim() ?? "自定义服务"; selected.Connection.BaseUrl = address.Text?.Trim() ?? "";
        selected.Connection.ApiKey = key.Text ?? ""; selected.Connection.Model = model.Text?.Trim() ?? "";
        selected.Connection.Protocol = protocol.SelectedItem as string ?? "auto"; selected.Connection.UseProxy = useProxy.IsChecked == true;
        selected.Connection.ProxyAddress = proxy.Text?.Trim() ?? ""; selected.Connection.StreamReplies = stream.IsChecked == true;
        var id = selected.Connection.Model;
        if (id.Length > 0) { if (!selected.Models.Contains(id)) selected.Models.Add(id); if (!string.IsNullOrWhiteSpace(alias.Text)) selected.ModelDisplayNames[id] = alias.Text.Trim(); else selected.ModelDisplayNames.Remove(id); }
        selected.Normalize();
    }
    private void AddProvider()
    {
        try { Capture(); CancelFetch(); if (view.TextProviders.Count >= 32) throw new InvalidOperationException("最多添加 32 个厂商。"); var item = new TextProviderSettings(); view.TextProviders.Add(item); ReloadProviders(item.Id); } catch (Exception e) { owner.Error(e); }
    }
    private void RemoveProvider()
    {
        if (selected == null) return;
        if (view.TextProviders.Count == 1) { owner.Error(new InvalidOperationException("至少保留一个厂商。")); return; }
        CancelFetch(); view.TextProviders.Remove(selected); ReloadProviders(view.TextProviders[0].Id);
    }
    private void AddModel()
    {
        try { Capture(); LoadFields(); notice.Text = "已加入草稿；点击保存并启用后生效。"; } catch (Exception e) { owner.Error(e); }
    }
    private void RemoveModel()
    {
        if (selected == null || models.SelectedItem is not string id) return;
        selected.Models.Remove(id); selected.ModelDisplayNames.Remove(id); selected.Connection.Model = selected.Models.FirstOrDefault() ?? ""; LoadFields();
    }
    private void CancelFetch() { revision++; fetch?.Cancel(); found.Children.Clear(); notice.Text = ""; }
    private async Task FetchAsync()
    {
        Capture(); CancelFetch(); if (selected == null) return;
        var providerId = selected.Id; var tokenRevision = revision; var snapshot = selected.Connection.Copy();
        var cancellation = new CancellationTokenSource(); fetch = cancellation;
        notice.Text = "正在获取模型列表…";
        try
        {
            var result = await owner.Api.ListModelsAsync(snapshot, cancellation.Token);
            if (cancellation.IsCancellationRequested || tokenRevision != revision || selected.Id != providerId) return;
            var checks = new List<CheckBox>();
            foreach (var id in result.Take(500)) { var check = new CheckBox { Content = id, Tag = id, IsChecked = selected.Models.Contains(id) }; checks.Add(check); found.Children.Add(check); }
            found.Children.Add(Ui.Button("添加所选模型", () =>
            {
                if (tokenRevision != revision || selected?.Id != providerId) return;
                foreach (var check in checks.Where(c => c.IsChecked == true)) if (selected.Models.Count < TextProviderSettings.MaximumModels && !selected.Models.Contains((string)check.Tag!)) selected.Models.Add((string)check.Tag!);
                LoadFields(); notice.Text = "已加入草稿；点击保存并启用后生效。";
            }));
            notice.Text = $"找到 {result.Count} 个模型，可多选加入。";
        }
        catch (OperationCanceledException) { if (tokenRevision == revision) notice.Text = "已取消。"; }
        finally { if (ReferenceEquals(fetch, cancellation)) fetch = null; cancellation.Dispose(); }
    }
    private void Save()
    {
        try
        {
            if (owner.Session.IsBusy || owner.Live.IsActive) throw new InvalidOperationException("请先停止对话再修改正在使用的服务。");
            Capture(); if (selected == null) return;
            view.ActiveTextProviderId = selected.Id; view.Text = selected.Connection.Copy();
            owner.Settings.ApplyProviders(kind, view.TextProviders.Select(p => p.Copy()).ToList(), selected.Id, selected.Connection.Copy());
            owner.Save(); notice.Text = "已保存并启用。";
        }
        catch (Exception e) { owner.Error(e); }
    }
    public void Dispose() => CancelFetch();
}
