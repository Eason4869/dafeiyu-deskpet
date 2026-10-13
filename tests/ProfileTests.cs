using System;
using System.IO;
using Dafeiyu;
namespace Dafeiyu.Tests
{
    public static class ProfileTests
    {
        static int passed;
        public static int Main()
        {
            try
            {
                string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"profile-tests",Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var catalog=new SkinCatalog(root);var settings=new PetSettings();
                PersonaProfiles.Activate(settings,catalog.Find("dafeiyu"));Check(settings.SystemPrompt.Contains("鲸鱼娘"),"new dafeiyu resolves whale persona");
                PersonaProfiles.Activate(settings,catalog.Find("e-bao"));string exact="你是E包，一位傲娇、毒舌的白发正太桌面伙伴。用自然的中文与用户对话，帮助写代码、做设计和整理思路。回答清楚简洁，如实表达不确定性。你不能直接看见用户桌面，也不会自动读取文件。";
                Check(settings.SystemPrompt==exact && !settings.SystemPrompt.Contains("鲸鱼"),"Ebao uses the exact user supplied persona");
                PersonaProfiles.Save(settings,catalog.Find("e-bao"),"自定义E包");PersonaProfiles.Activate(settings,catalog.Find("dafeiyu"));PersonaProfiles.Activate(settings,catalog.Find("e-bao"));Check(settings.SystemPrompt=="自定义E包","per-pet override survives a round trip");
                PersonaProfiles.Reset(settings,catalog.Find("e-bao"));Check(settings.SystemPrompt==exact,"reset restores only the current pet preset");
                var legacy=new PetSettings {SkinId="e-bao",SystemPrompt="你是大肥鱼，蓝白鲸鱼娘。"};PersonaProfiles.Activate(legacy,catalog.Find("e-bao"));Check(legacy.SystemPrompt==exact,"old global whale persona does not become Ebao persona");PersonaProfiles.Activate(legacy,catalog.Find("dafeiyu"));Check(legacy.SystemPrompt=="你是大肥鱼，蓝白鲸鱼娘。","old custom whale persona is preserved for dafeiyu");
                string atlas=Path.Combine(root,"spritesheet.png");using(var stream=typeof(SkinCatalog).Assembly.GetManifestResourceStream("Dafeiyu.Atlas"))using(var file=File.Create(atlas))stream.CopyTo(file);
                string manifest=Path.Combine(root,"pet.json");File.WriteAllText(manifest,"{\"version\":1,\"name\":\"小墨\",\"atlas\":\"spritesheet.png\",\"system_prompt\":\"你是小墨，沉稳的画师。\",\"description\":\"喜欢画画\"}");var imported=catalog.Import(manifest);PersonaProfiles.Activate(settings,imported);Check(settings.SystemPrompt=="你是小墨，沉稳的画师。" && imported.Description=="喜欢画画","import retains its own preset and description");
                var reloaded=new SkinCatalog(root).Find(imported.Id);Check(reloaded.SystemPrompt==imported.SystemPrompt,"imported persona survives catalog reload");
                var png=catalog.Import(atlas);Check(png.SystemPrompt.Contains(png.Name) && !png.SystemPrompt.Contains("鲸鱼娘"),"plain PNG receives a named generic preset");
                PersonaProfiles.Save(settings,catalog.Find("dafeiyu"),"保留给大肥鱼的设定");var store=new SettingsStore(Path.Combine(root,"settings.json"));store.Save(settings);var saved=store.Load();Check(saved.PersonaOverrides["dafeiyu"]=="保留给大肥鱼的设定" && saved.PersonaProfileVersion==1,"persona ownership persists with settings");
                Console.WriteLine("Profile tests: "+passed+" passed");return 0;
            }
            catch(Exception e){Console.WriteLine("FAIL Profile tests: "+e.Message);return 1;}
        }
        static void Check(bool value,string text){if(!value)throw new Exception(text);passed++;Console.WriteLine("PASS "+text);}
    }
}
