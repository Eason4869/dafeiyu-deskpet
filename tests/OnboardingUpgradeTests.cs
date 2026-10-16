using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Dafeiyu.Tests
{
    public static class OnboardingUpgradeTests
    {
        static int passed;
        public static int Main()
        {
            passed=0;
            try { Run();Console.WriteLine("Onboarding upgrade tests: "+passed+" passed");return 0; }
            catch(Exception error){Console.WriteLine("FAIL onboarding upgrade: "+error+"; HRESULT=0x"+error.HResult.ToString("X8"));return 1;}
        }
        static void Run()
        {
            Check(typeof(PetSettings).GetProperty("LastOnboardingSeries")!=null,"settings expose persisted onboarding series");
            var fresh=new PetSettings();var result=Prepare(fresh,"0.5.5");
            Check(result.ShowGuide && result.Changed && Series(fresh)=="0.5" && fresh.OnboardingStep==0,"fresh installation records current series before its only automatic guide");
            Check(!Prepare(fresh,"0.5.5").ShowGuide,"same process cannot automatically open a second first-run guide");
            foreach(string state in new[]{"new","deferred","completed","skipped"}) {
                fresh.OnboardingState=state;fresh.OnboardingStep=7;
                Check(!Prepare(fresh,"0.5.11").ShowGuide && fresh.OnboardingStep==7,"same-series restart preserves guide state after "+state);
            }
            string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"onboarding-series-tests",Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(folder);var path=Path.Combine(folder,"settings.json");
            File.WriteAllText(path,"{\"OnboardingState\":\"completed\",\"OnboardingStep\":8,\"OnboardingVersion\":1}");
            var store=new SettingsStore(path);var old=store.Load();
            Check(Series(old)=="0.5","legacy config without field migrates to baseline 0.5");
            result=Prepare(old,"0.5.11");
            Check(!result.ShowGuide && !result.Changed && old.OnboardingStep==8,"0.5.3 to 0.5.11 never repeats onboarding");
            result=Prepare(old,"0.6.0");
            Check(result.ShowGuide && result.Changed && Series(old)=="0.6" && old.OnboardingStep==0 && old.OnboardingState=="new","legacy config first entering 0.6 resets guide from step zero");
            store.Save(old);var persisted=store.Load();
            Check(Series(persisted)=="0.6" && !Prepare(persisted,"0.6.1").ShowGuide,"series is persisted before any guide completion or close");
            Check(Series(persisted.Copy())=="0.6","settings copy preserves last onboarding series");
            if(Environment.OSVersion.Platform==PlatformID.Win32NT)ReplaceSharingChecks(store,path);
            else Console.WriteLine("SKIP Windows delete-sharing fixture: rename semantics differ on this platform; all 35 onboarding policy checks still run.");
            foreach(var pair in new[]{new[]{"0.8","1.0.0"},new[]{"0.9","0.10.0"},new[]{"2.8","10.0.0"},new[]{"0.5","8.7.4"}}) {
                var settings=Recorded(pair[0]);result=Prepare(settings,pair[1]);
                Check(result.ShowGuide && result.Changed && settings.OnboardingStep==0,"numeric series advance triggers across "+pair[0]+" to "+pair[1]);
            }
            var downgrade=Recorded("1.3");
            Check(!Prepare(downgrade,"0.8.3").ShowGuide && Series(downgrade)=="1.3","downgrading keeps the highest triggered series");
            Check(!Prepare(downgrade,"1.3.9").ShowGuide && Series(downgrade)=="1.3","returning to a previously triggered series stays silent");
            Check(Prepare(downgrade,"1.4.0").ShowGuide && Series(downgrade)=="1.4","a newer series still triggers after downgrade and re-upgrade");
            foreach(string invalid in new[]{null,"","bad","-1.2",new string('9',10000)}) {
                var damaged=Recorded(invalid);result=Prepare(damaged,"0.5.5");
                Check(!result.ShowGuide && result.Changed && Series(damaged)=="0.5","damaged recorded series recovers to migration baseline");
            }
            foreach(string invalid in new[]{null,"","bad","1","1.-2","1.2.3.4.5",new string('9',10000)+".0","2147483648.0"}) {
                var settings=Recorded("0.5");result=Prepare(settings,invalid);
                Check(!result.ShowGuide && !result.Changed && Series(settings)=="0.5","invalid running version cannot reset guide or consume the series");
            }
            var canonical=Recorded("00.05");result=Prepare(canonical,"0.5.99");
            Check(!result.ShowGuide && result.Changed && Series(canonical)=="0.5","equivalent numeric series canonicalizes without retriggering");
            var maximum=Recorded("2147483647.2147483647");result=Prepare(maximum,"2147483647.2147483647.1");
            Check(!result.ShowGuide && !result.Changed,"maximum supported numeric series does not overflow");
            var manual=Recorded("0.6");manual.OnboardingState="deferred";manual.OnboardingStep=4;
            Check(!Prepare(manual,"0.6.2").ShowGuide && manual.OnboardingState=="deferred" && manual.OnboardingStep==4,"manual guide state remains available without automatic reopening");
        }
        static PetSettings Recorded(string series){return new PetSettings{OnboardingState="completed",OnboardingStep=9,LastOnboardingSeries=series};}
        static void ReplaceSharingChecks(SettingsStore store,string path)
        {
            var updated=store.Load();updated.OnboardingStep=6;var started=new ManualResetEventSlim();Task saving;
            using(var reader=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                saving=Task.Run(delegate{started.Set();store.Save(updated);});started.Wait();Thread.Sleep(80);
                if(saving.IsFaulted)Console.WriteLine("Observed replace failure HRESULT=0x"+saving.Exception.InnerException.HResult.ToString("X8"));
                Check(!saving.IsCompleted,"a short delete-sharing lock is retried before returning a save failure");
            }
            saving.GetAwaiter().GetResult();started.Dispose();
            Check(store.Load().OnboardingStep==6 && !File.Exists(path+".tmp"),"settings commit succeeds once the transient reader releases its lock");
            byte[] original=File.ReadAllBytes(path);bool rejected=false;
            using(var reader=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read)) {
                try{updated.OnboardingStep=7;store.Save(updated);}catch(IOException){rejected=true;}
            }
            Check(rejected,"a persistent delete-sharing lock still reports a save error");
            Check(Convert.ToBase64String(File.ReadAllBytes(path))==Convert.ToBase64String(original) && !File.Exists(path+".tmp"),"failed settings replacement preserves the original file and removes its temporary file");
        }
        static string Series(PetSettings settings){return settings.LastOnboardingSeries;}
        static OnboardingUpgradeResult Prepare(PetSettings settings,string version){return OnboardingUpgrade.Prepare(settings,version);}
        static void Check(bool condition,string message){if(!condition)throw new Exception(message);passed++;Console.WriteLine("PASS "+message);}
    }
}
