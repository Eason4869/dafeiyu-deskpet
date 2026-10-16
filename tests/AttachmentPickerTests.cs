using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Automation;

namespace Dafeiyu.Tests
{
    public static class AttachmentPickerTests
    {
        private static int assertions;
        private static void Check(bool condition, string message) { assertions++; if (!condition) throw new Exception(message); }
        [STAThread] public static int Main()
        {
            string scratch = Path.Combine(Directory.GetCurrentDirectory(), "work", "attachment-picker-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(scratch);
            File.WriteAllText(Path.Combine(scratch, "one.txt"), "picker test");
            File.WriteAllBytes(Path.Combine(scratch, "two.png"), new byte[] { 137, 80, 78, 71 });
            try
            {
                string[] paths = DriveDialog(scratch, delegate(AutomationElement dialog)
                {
                    Invoke(dialog, "添加当前文件夹");
                });
                Check(paths.Length == 1 && paths[0].Equals(scratch, StringComparison.OrdinalIgnoreCase), "custom folder button returns explicit current directory");
                paths = DriveDialog(scratch, delegate(AutomationElement dialog)
                {
                    var edits = dialog.FindAll(TreeScope.Descendants, new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Edit)).Cast<AutomationElement>();
                    var filename = edits.FirstOrDefault(e => e.Current.Name.Contains("文件名") || e.Current.Name.IndexOf("File name", StringComparison.OrdinalIgnoreCase) >= 0 || e.Current.AutomationId == "1148");
                    if (filename == null) throw new Exception("Native file-name field not found.");
                    ((ValuePattern)filename.GetCurrentPattern(ValuePattern.Pattern)).SetValue("\"one.txt\" \"two.png\"");
                    Invoke(dialog, "添加所选文件");
                });
                Check(paths.Length == 2 && paths.Contains(Path.Combine(scratch, "one.txt")) && paths.Contains(Path.Combine(scratch, "two.png")), "single native dialog multi-selects file and image");
                paths = DriveDialog(scratch, delegate(AutomationElement dialog) { ((WindowPattern)dialog.GetCurrentPattern(WindowPattern.Pattern)).Close(); });
                Check(paths.Length == 0, "cancel returns empty array");
                Exception threadFailure = null;
                var mta = new Thread(delegate() { try { AttachmentPicker.Show(IntPtr.Zero); } catch (Exception ex) { threadFailure = ex; } });
                mta.SetApartmentState(ApartmentState.MTA); mta.Start(); mta.Join();
                Check(threadFailure is InvalidOperationException, "non-STA use fails clearly");
                Console.WriteLine("AttachmentPickerTests: " + assertions + " assertions passed.");
                return 0;
            }
            catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
            finally { if (Directory.Exists(scratch)) Directory.Delete(scratch, true); }
        }
        private static string[] DriveDialog(string directory, Action<AutomationElement> action)
        {
            Exception failure = null;
            var worker = new Thread(delegate()
            {
                AutomationElement dialog = null;
                try
                {
                    var clock = Stopwatch.StartNew();
                    while (clock.ElapsedMilliseconds < 8000)
                    {
                        dialog = AutomationElement.RootElement.FindAll(TreeScope.Children,
                            new PropertyCondition(AutomationElement.ProcessIdProperty, Process.GetCurrentProcess().Id))
                            .Cast<AutomationElement>().FirstOrDefault(e => e.Current.Name == "添加文件、图片或文件夹");
                        if (dialog != null) break;
                        Thread.Sleep(40);
                    }
                    if (dialog == null) throw new Exception("Native picker did not open on the interactive desktop.");
                    Thread.Sleep(150); action(dialog);
                }
                catch (Exception ex)
                {
                    failure = ex;
                    if (dialog != null) PostMessage(new IntPtr(dialog.Current.NativeWindowHandle), 0x10, IntPtr.Zero, IntPtr.Zero);
                }
            });
            worker.IsBackground = true; worker.SetApartmentState(ApartmentState.MTA); worker.Start();
            string[] result = AttachmentPicker.Show(IntPtr.Zero, directory);
            worker.Join(10000);
            if (failure != null) throw new Exception("Dialog automation failed.", failure);
            return result;
        }
        private static void Invoke(AutomationElement dialog, string label)
        {
            var button = dialog.FindFirst(TreeScope.Descendants, new AndCondition(
                new PropertyCondition(AutomationElement.ControlTypeProperty, ControlType.Button),
                new PropertyCondition(AutomationElement.NameProperty, label)));
            if (button == null) throw new Exception("Native button missing: " + label);
            ((InvokePattern)button.GetCurrentPattern(InvokePattern.Pattern)).Invoke();
        }
        [DllImport("user32.dll")] private static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);
    }
}
