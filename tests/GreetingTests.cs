using System;
using System.IO;

namespace Dafeiyu.Tests
{
    public static class GreetingTests
    {
        static int passed;
        static void Check(bool value,string text){if(!value)throw new Exception(text);passed++;Console.WriteLine("PASS "+text);}
        public static int Main()
        {
            try
            {
                var enabled=typeof(PetSettings).GetProperty("GreetingEnabled");
                Check(enabled!=null,"主动问候可以配置开关");
                var minimum=typeof(PetSettings).GetProperty("GreetingMinMinutes");
                var maximum=typeof(PetSettings).GetProperty("GreetingMaxMinutes");
                var settings=new PetSettings();
                Check((bool)enabled.GetValue(settings,null) && (int)minimum.GetValue(settings,null)==30 && (int)maximum.GetValue(settings,null)==60,"默认启用并等待 30–60 分钟");
                enabled.SetValue(settings,false,null);minimum.SetValue(settings,12,null);maximum.SetValue(settings,23,null);
                string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"greeting-tests",Guid.NewGuid().ToString("N"));
                var store=new SettingsStore(Path.Combine(folder,"settings.json"));store.Save(settings);var saved=store.Load();var copy=saved.Copy();
                Check(!(bool)enabled.GetValue(copy,null) && (int)minimum.GetValue(copy,null)==12 && (int)maximum.GetValue(copy,null)==23,"开关与时间在保存和复制后保留");
                File.WriteAllText(Path.Combine(folder,"old.json"),"{\"SkinId\":\"dafeiyu\"}");var old=new SettingsStore(Path.Combine(folder,"old.json")).Load();
                Check((bool)enabled.GetValue(old,null) && (int)minimum.GetValue(old,null)==30,"旧配置补充默认问候设置");
                minimum.SetValue(settings,-20,null);maximum.SetValue(settings,0,null);settings.Normalize();
                Check((int)minimum.GetValue(settings,null)>=1 && (int)maximum.GetValue(settings,null)>=(int)minimum.GetValue(settings,null),"非法时间范围被规范化");
                var type=typeof(PetSettings).Assembly.GetType("Dafeiyu.GreetingSchedule");Check(type!=null,"提供独立的问候计时");
                var now=new DateTime(2026,10,14,8,0,0,DateTimeKind.Utc);
                Func<double> sample=()=>0;var schedule=Activator.CreateInstance(type,new object[]{now,30,60,sample});
                var take=type.GetMethod("TryTake");Func<DateTime,bool,bool> tick=(time,allowed)=>(bool)take.Invoke(schedule,new object[]{time,allowed});
                bool quiet=true;for(int i=1;i<30;i++)quiet &= !tick(now.AddMinutes(i),true);Check(quiet,"未到最短时间不主动问候");
                Check(tick(now.AddMinutes(30),true),"达到最短时间问候一次");
                Check(!tick(now.AddMinutes(30).AddSeconds(1),true),"相邻 Tick 不重复问候");
                for(int i=31;i<60;i++)tick(now.AddMinutes(i),true);
                Check(!tick(now.AddMinutes(60),false),"隐藏或忙碌状态不问候");
                Check(!tick(now.AddMinutes(60).AddSeconds(1),true),"解除禁止后不补发积累问候");
                Check(!tick(now.AddHours(8),true),"睡眠或锁屏恢复后重新等待完整间隔");
                var interrupted=Activator.CreateInstance(type,new object[]{now,30,60,new Func<double>(()=>0)});
                for(int i=1;i<20;i++)take.Invoke(interrupted,new object[]{now.AddMinutes(i),true});
                take.Invoke(interrupted,new object[]{now.AddMinutes(20),false});take.Invoke(interrupted,new object[]{now.AddMinutes(21),true});
                Check((DateTime)type.GetProperty("NextAt").GetValue(interrupted,null)==now.AddMinutes(51),"对话或全屏在到期前结束，也重新等待完整间隔");
                sample=()=>1;var late=Activator.CreateInstance(type,new object[]{now,30,60,sample});
                var next=(DateTime)type.GetProperty("NextAt").GetValue(late,null);Check(next==now.AddMinutes(60),"最长等待范围正确");
                var greetings=typeof(PetSettings).Assembly.GetType("Dafeiyu.GreetingPhrases");
                string eb=(string)greetings.GetMethod("Choose").Invoke(null,new object[]{"e-bao","E包",new DateTime(2026,10,14,9,0,0),0});
                string df=(string)greetings.GetMethod("Choose").Invoke(null,new object[]{"dafeiyu","大肥鱼",new DateTime(2026,10,14,9,0,0),0});
                string custom=(string)greetings.GetMethod("Choose").Invoke(null,new object[]{"custom","星星",new DateTime(2026,10,14,9,0,0),0});
                Check(eb!=df && custom.Contains("星星"),"预置角色语气不同，导入角色使用自身名称");
                Console.WriteLine("Greeting tests: "+passed+" passed");return 0;
            }
            catch(Exception e){Console.WriteLine("FAIL Greeting: "+e.Message);return 1;}
        }
    }
}
