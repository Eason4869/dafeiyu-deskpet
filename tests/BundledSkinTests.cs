using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;

namespace Dafeiyu.Tests
{
    public static class BundledSkinTests
    {
        static int passed;
        static void Check(bool value,string message){if(!value)throw new Exception(message);Console.WriteLine("PASS "+message);passed++;}
        static byte[] Hash(string path){using(var stream=File.OpenRead(path))using(var sha=SHA256.Create())return sha.ComputeHash(stream);}
        [STAThread] public static int Main()
        {
            string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"bundled-skin-tests-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try {
                string data=Path.Combine(root,"data");
                var catalog=new SkinCatalog(data);var skin=catalog.Find("xiaobei");
                Check(skin.Id=="xiaobei" && skin.Illustrated && skin.Name=="小贝","fresh install offers bundled Xiaobei without import");
                Check(catalog.Skins.Count(s=>s.Id=="xiaobei")==1 && catalog.Skins.Count(s=>s.Embedded)==3,"preset is unique and preserves original three partners");
                Check(skin.SystemPrompt.Contains("男性") && skin.SystemPrompt.Length>120 && skin.SpecialAction!="专属动作","bundled preset retains complete male persona and named action");
                var settings=new PetSettings();PersonaProfiles.Activate(settings,skin);
                Check(settings.SkinId=="xiaobei" && settings.SystemPrompt==skin.SystemPrompt,"selecting preset activates Xiaobei persona");
                PersonaProfiles.Save(settings,skin,"用户保留的小贝人格");PersonaProfiles.Activate(settings,catalog.Find("e-bao"));PersonaProfiles.Activate(settings,skin);
                Check(settings.SystemPrompt=="用户保留的小贝人格","bundled persona respects the user's per-companion override");
                PersonaProfiles.Reset(settings,skin);Check(settings.SystemPrompt==skin.SystemPrompt,"reset restores the complete bundled persona");
                Check(skin.Source.StartsWith(Path.GetFullPath(Path.Combine(data,"bundled-skins"))+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase),"preset assets use separate bundled cache");
                Check(!Directory.Exists(Path.Combine(data,"skins")),"fresh bundled loading does not create user imported skins directory");
                Check(skin.Manifest.Animations.Count==17 && skin.Manifest.Animations.Values.All(a=>a.Frames==8 && a.Fps>=1 && a.Fps<=8),"bundled Xiaobei has eight real poses per action at low frame rate");
                var motion=new SmoothMotion(skin);
                for(int action=0;action<IllustratedPack.Actions.Length;action++) {
                    string name=IllustratedPack.Actions[action];
                    double expected=name=="running-right" || name=="running-left" ? 1.76 : name=="jumping" || name=="landing" || name=="poked" ? 1 : name=="sleeping" ? 3.2 : name=="special" ? 2 : 1.6;
                    var clip=motion.Get(action<9?action:action+2);
                    Check(clip.Count==8 && Math.Abs(clip.Duration-expected)<1e-8,"bundled runtime honors selected eight-frame duration: "+name);
                }
                Check(skin.Load().HasGaze && SkinCatalog.GetPreview(skin).PixelWidth==480,"bundled gaze and preview decode");
                string json=Path.Combine(skin.Source,"pet.json");DateTime written=File.GetLastWriteTimeUtc(json);
                var again=new SkinCatalog(data).Find("xiaobei");
                Check(again.Source==skin.Source && File.GetLastWriteTimeUtc(json)==written,"reopening reuses verified immutable cache");
                var imported=catalog.Import(json);string importJson=Path.Combine(imported.Source,"pet.json");byte[] original=Hash(importJson);
                Check(imported.Id.StartsWith("skin-") && imported.Id!="xiaobei","user import with same name has independent identity");
                var reopened=new SkinCatalog(data);
                Check(reopened.Find(imported.Id).Id==imported.Id && reopened.Find("xiaobei").Id=="xiaobei","preset and same-named user import both survive reload");
                Check(Hash(importJson).SequenceEqual(original),"loading preset never rewrites user imported manifest");
                string cachedPreview=Path.Combine(skin.Source,skin.Manifest.Preview);byte[] preview=Hash(cachedPreview);
                File.WriteAllText(cachedPreview,"damaged local cache");
                var repaired=new SkinCatalog(data).Find("xiaobei");
                Check(repaired.Id=="xiaobei" && repaired.Source!=skin.Source && Hash(Path.Combine(repaired.Source,repaired.Manifest.Preview)).SequenceEqual(preview),"damaged cache is replaced from trusted embedded bytes in a new folder");
                Check(Hash(importJson).SequenceEqual(original) && reopened.Find(imported.Id).Load().HasGaze,"cache recovery leaves imported appearance usable");
                Check(new SkinCatalog(data).Find("xiaobei").Source==repaired.Source,"recovered cache is reused without accumulating copies");
                File.WriteAllText(Path.Combine(repaired.Source,"pet.json"),"{\"version\":2,\"name\":\"tampered\"}");
                Check(new SkinCatalog(data).Find("xiaobei").Name=="小贝","modified cached persona cannot override embedded preset");
                Check(!Directory.GetDirectories(Path.Combine(data,"bundled-skins"),".staging-*").Any(),"successful extraction leaves no staging folders");
                Check(new SkinCatalog(data).Find("not-installed").Id=="e-bao","unknown selection preserves existing default fallback");
                Console.WriteLine("PASS "+passed+" bundled skin checks");return 0;
            } catch(Exception e){Console.Error.WriteLine(e);return 1;}
            finally {Directory.Delete(root,true);}
        }
    }
}
