using System;
using System.IO;
using Microsoft.Win32;
namespace Dafeiyu
{
    public sealed class StartupRegistration
    {
        public const string ValueName="DafeiyuDeskPet";
        const string RunKey="Software\\Microsoft\\Windows\\CurrentVersion\\Run";
        readonly string executable,keyPath,valueName;
        public StartupRegistration(string executablePath):this(executablePath,RunKey,ValueName){}
        internal StartupRegistration(string executablePath,string registryPath,string registryValueName)
        {
            executable=Path.GetFullPath(executablePath);
            if(executable.IndexOfAny(new[]{'"','\r','\n'})>=0)throw new ArgumentException("Invalid executable path.");
            keyPath=registryPath;valueName=registryValueName;
        }
        string Command{get{return "\""+executable+"\" --startup";}}
        public bool IsEnabled
        {
            get{using(var key=Registry.CurrentUser.OpenSubKey(keyPath,false))return key!=null && String.Equals(key.GetValue(valueName,null,RegistryValueOptions.DoNotExpandEnvironmentNames) as string,Command,StringComparison.OrdinalIgnoreCase);}
        }
        public void SetEnabled(bool enabled)
        {
            if(enabled){using(var key=Registry.CurrentUser.CreateSubKey(keyPath)){if(key==null)throw new UnauthorizedAccessException();key.SetValue(valueName,Command,RegistryValueKind.String);}}
            else{using(var key=Registry.CurrentUser.OpenSubKey(keyPath,true)){if(key!=null)key.DeleteValue(valueName,false);}}
        }
    }
}
