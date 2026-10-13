using System;
using System.IO;
using Microsoft.Win32;
namespace Dafeiyu.Tests
{
    public static class StartupTests
    {
        static int passed;
        public static int Main()
        {
            string root="Software\\DafeiyuDeskPet.Tests\\Startup\\"+Guid.NewGuid().ToString("N");
            try
            {
                string executable=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"中文 path","桌宠.exe");
                var registration=new StartupRegistration(executable,root,"TestPet");
                Check(!registration.IsEnabled,"autostart starts disabled without creating a key");
                using(var untouched=Registry.CurrentUser.OpenSubKey(root))Check(untouched==null,"query does not create a registry key");
                registration.SetEnabled(true);Check(registration.IsEnabled,"enabling autostart survives reloading");
                using(var key=Registry.CurrentUser.OpenSubKey(root,true))
                {Check((string)key.GetValue("TestPet")=="\""+executable+"\" --startup","Unicode paths with spaces are quoted");key.SetValue("OtherApp","preserve this value");}
                registration.SetEnabled(false);Check(!registration.IsEnabled,"disabling removes the pet entry");
                using(var key=Registry.CurrentUser.OpenSubKey(root))Check((string)key.GetValue("OtherApp")=="preserve this value","disabling preserves other application values");
                registration.SetEnabled(false);Check(!registration.IsEnabled,"disabling twice is safe");
                Console.WriteLine("Startup tests: "+passed+" passed");return 0;
            }
            catch(Exception e){Console.WriteLine("FAIL startup: "+e.Message);return 1;}
            finally{Registry.CurrentUser.DeleteSubKey(root,false);}
        }
        static void Check(bool value,string label){if(!value)throw new Exception(label);passed++;Console.WriteLine("PASS "+label);}
    }
}
