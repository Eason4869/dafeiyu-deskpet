using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Dafeiyu;

namespace DeskPet.Desktop;

internal sealed class AboutWindow : Window
{
    internal static bool MatchesArchitecture(string name, string architecture)
    {
        name = name.ToLowerInvariant();
        var matches = architecture == "x64" ? name.Contains("x64") || name.Contains("x86_64") : name.Contains(architecture);
        return matches && (name.EndsWith(".dmg") || name.EndsWith(".zip")) && (name.Contains("mac") || name.Contains("osx"));
    }
    private readonly DesktopController owner;
    private readonly TextBlock notice = Ui.Text("");
    private readonly TextBox repository;
    private readonly CancellationTokenSource cancel = new();
    private Uri? releasePage;
    private readonly Button download;
    public AboutWindow(DesktopController controller)
    {
        owner = controller; Title = "DeskPet · 关于与更新"; Width = 570; Height = 490;
        var version = typeof(App).Assembly.GetName().Version;
        repository = Ui.Input(owner.Settings.RepositoryUrl);
        download = Ui.AsyncButton("打开最新版本下载页", async () => { if (releasePage != null) await Launcher.LaunchUriAsync(releasePage); }, owner.Error); download.IsEnabled = false;
        Content = new Border { Padding = new Thickness(24), Child = Ui.Column(Ui.Text("DeskPet " + version?.ToString(3), 24), Ui.Text("macOS 客户端 · Avalonia / .NET\n桌面伙伴、文字聊天、语音与实时对话"), Ui.Field("更新仓库（HTTPS GitHub 仓库地址）", repository), Ui.AsyncButton("检查更新", CheckAsync, owner.Error), download, notice, Ui.Text("下载后使用新应用替换 Applications 中的旧版本。设置和历史保存在用户数据目录，替换应用不会清除。此界面不静默安装或绕过 Gatekeeper。"), Ui.Button("关闭", Close)) };
        Closed += (_, _) => { cancel.Cancel(); cancel.Dispose(); };
    }
    private async Task CheckAsync()
    {
        var token = cancel.Token;
        var repo = GitHubUpdates.Repository(repository.Text ?? "");
        owner.Settings.RepositoryUrl = repository.Text!.Trim(); owner.Save();
        notice.Text = "正在查询最新正式版本…"; download.IsEnabled = false;
        using var client = owner.Settings.UpdateUseProxy ? ProxyConfiguration.CreateHttpClient(owner.Settings.UpdateProxyAddress, true) : new HttpClient(new HttpClientHandler { UseProxy = false });
        client.Timeout = TimeSpan.FromSeconds(30); client.DefaultRequestHeaders.UserAgent.ParseAdd("DeskPet/" + typeof(App).Assembly.GetName().Version?.ToString(3));
        using var response = await client.GetAsync("https://api.github.com/repos/" + repo + "/releases/latest", token);
        if (!response.IsSuccessStatusCode) throw new ApiException("检查更新失败（HTTP " + (int)response.StatusCode + "）。请检查仓库、代理或稍后重试。");
        var data = await response.Content.ReadAsByteArrayAsync(token);
        if (data.Length > 1024 * 1024) throw new ApiException("版本信息过大。");
        using var json = JsonDocument.Parse(data); var root = json.RootElement;
        var tag = root.GetProperty("tag_name").GetString() ?? "";
        if (!Uri.TryCreate(root.GetProperty("html_url").GetString(), UriKind.Absolute, out var page) || page.Scheme != "https" || page.Host != "github.com") throw new ApiException("更新页面地址无效。");
        token.ThrowIfCancellationRequested(); releasePage = page; download.IsEnabled = true;
        var next = Version.TryParse(tag.TrimStart('v', 'V'), out var v) ? v : null;
        var current = typeof(App).Assembly.GetName().Version!;
        var newer = next != null && new Version(next.Major, next.Minor, Math.Max(0, next.Build)) > new Version(current.Major, current.Minor, current.Build);
        var arch = System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture == System.Runtime.InteropServices.Architecture.Arm64 ? "arm64" : "x64";
        var assets = root.GetProperty("assets").EnumerateArray().Where(a => MatchesArchitecture(a.GetProperty("name").GetString() ?? "", arch)).Select(a => a.GetProperty("name").GetString()).ToList();
        notice.Text = (newer ? "发现新版本 " : "最新发布版本 ") + tag + "。\n" + (assets.Count > 0 ? "本机架构附件：" + string.Join("、", assets) : "该版本没有发现适合本机架构的 macOS 附件。请勿下载 Windows 安装包。");
    }
}
