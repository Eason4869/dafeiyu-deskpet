using System.Diagnostics;
using System.Reflection;
using DeskPet.Platform.Mac;

internal static class SpeechProcessTests
{
    internal static async Task Run()
    {
        var start = SayProcess.StartInfo();
        Assert(start.FileName == "/usr/bin/say" && !start.UseShellExecute && start.RedirectStandardInput && start.ArgumentList.SequenceEqual(["--"]), "system speech arguments contain no private text");
        Assert(SayProcess.SplitPhrases("你好。Hello! 第二句？\n末句").SequenceEqual(["你好。", "Hello!", "第二句？", "末句"]), "system speech follows sentence boundaries");
        var pidFile = Path.Combine(Path.GetTempPath(), "deskpet-say-test-" + Guid.NewGuid().ToString("N"));
        var runner = new SayProcess(() =>
        {
            var info = new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = false, RedirectStandardInput = true, RedirectStandardError = true, CreateNoWindow = true };
            if (string.Equals(Path.GetFileNameWithoutExtension(Environment.ProcessPath), "dotnet", StringComparison.OrdinalIgnoreCase)) info.ArgumentList.Add(Assembly.GetExecutingAssembly().Location);
            info.ArgumentList.Add("--speech-process-child"); info.ArgumentList.Add(pidFile); return info;
        });
        using var cancellation = new CancellationTokenSource();
        try
        {
            var speech = runner.SpeakAsync("私密测试文本 [[volm 1]]", 25, cancellation.Token);
            Assert(SpinWait.SpinUntil(() => File.Exists(pidFile), 5000), "speech process started");
            var pid = int.Parse(await File.ReadAllTextAsync(pidFile));
            cancellation.Cancel();
            try { await speech; throw new Exception("Speech cancellation was ignored"); } catch (OperationCanceledException) { }
            bool alive;
            try { using var process = Process.GetProcessById(pid); alive = !process.HasExited; } catch (ArgumentException) { alive = false; }
            Assert(!alive, "cancelled speech process is reaped before completion");
        }
        finally { File.Delete(pidFile); }
        Console.WriteLine("4 system speech checks passed");
    }
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); Console.WriteLine("PASS " + message); }
}
