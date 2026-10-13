using System;
using System.IO;
using System.Reflection;
using System.Security.Principal;
using System.Threading;
using System.Windows;

[assembly: AssemblyTitle("大肥鱼桌面伙伴")]
[assembly: AssemblyDescription("高清 Windows 桌面伙伴，外观导入与文字、语音对话")]
[assembly: AssemblyVersion("1.2.0.0")]

namespace Dafeiyu
{
    public static class Program
    {
        [STAThread] public static void Main()
        {
            string suffix = WindowsIdentity.GetCurrent().User.Value;
            bool first;
            using (var mutex = new Mutex(true, "Local\\Dafeiyu.DeskPet." + suffix, out first))
            {
                if (!first)
                {
                    try { using (EventWaitHandle wake = EventWaitHandle.OpenExisting("Local\\Dafeiyu.DeskPet.Wake." + suffix)) wake.Set(); }
                    catch (WaitHandleCannotBeOpenedException) { }
                    return;
                }
                using (var wake = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\Dafeiyu.DeskPet.Wake." + suffix))
                {
                    try
                    {
                        var app = new Application { ShutdownMode = ShutdownMode.OnExplicitShutdown };
                        app.DispatcherUnhandledException += delegate(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs args)
                        {
                            MessageBox.Show("桌宠遇到了一个运行问题。可以退出后重新打开，设置仍保存在 data 文件夹。", "桌面伙伴", MessageBoxButton.OK, MessageBoxImage.Information);
                            args.Handled = true; app.Shutdown(1);
                        };
                        var store = new SettingsStore(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "data", "settings.json"));
                        var pet = new PetWindow(new SpriteAtlas(), store.Load(), store, wake);
                        pet.Closed += delegate { app.Shutdown(); }; app.Run(pet);
                    }
                    catch (Exception)
                    {
                        MessageBox.Show("桌宠无法启动。请将完整文件夹解压到可写入的位置，并确认 Windows 已安装 .NET Framework 4.8。", "大肥鱼", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                    finally { mutex.ReleaseMutex(); }
                }
            }
        }
    }
}
