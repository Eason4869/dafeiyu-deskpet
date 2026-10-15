using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization.Json;
using System.Text;
namespace Dafeiyu.Tests
{
 public static class ProviderTests
 {
  static int passed;
  public static int Main()
  {
   string folder=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"deskpet-providers-"+Guid.NewGuid().ToString("N"));
   try {
    Check(typeof(TextProviderSettings).GetProperty("ModelDisplayNames")!=null,"models support independent custom display names");var legacy=new PetSettings();legacy.Text=new ConnectionSettings{BaseUrl="https://first.example/v1",Model="first-a",ApiKey="first-secret",Protocol="anthropic",UseProxy=true,ProxyAddress="127.0.0.1:7891",StreamReplies=false};
    foreach(var example in new[]{new[]{"https://api.deepseek.com/v1","DeepSeek"},new[]{"https://api.xiaomimimo.com/v1","MiMo"},new[]{"https://api.minimax.io/v1","MiniMax"}})
    {var migrated=new PetSettings{Text=new ConnectionSettings{BaseUrl=example[0]}};migrated.EnsureTextProviders();Check(migrated.TextProviders[0].Name==example[1],"legacy provider defaults to its manufacturer: "+example[1]);}
    legacy.EnsureTextProviders();var first=legacy.TextProviders[0];
    Check(legacy.TextProviders.Count==1 && first.Connection.ApiKey=="first-secret" && first.Models.Contains("first-a"),"legacy connection migrates without losing secret or model");
    Check(first.Connection.Protocol=="anthropic" && first.Connection.UseProxy && !first.Connection.StreamReplies,"migration retains protocol proxy and streaming");
    string id=first.Id;legacy.EnsureTextProviders();Check(legacy.TextProviders.Count==1 && legacy.ActiveTextProviderId==id,"migration is idempotent");
    var second=new TextProviderSettings{Name="Second",BrandId="deepseek",Connection=new ConnectionSettings{BaseUrl="https://second.example/v1",ApiKey="second-secret",Model="second-a"},Models=new List<string>{"second-a","second-b"}};legacy.TextProviders.Add(second);
    first.ModelDisplayNames["first-a"]="日常聊天";second.ModelDisplayNames["second-b"]="深度思考";var snapshot=legacy.Copy();Check(legacy.SelectTextModel(second.Id,"second-b"),"select second provider model");
    Check(legacy.Text.ApiKey=="second-secret" && legacy.Text.Model=="second-b" && legacy.Text.BaseUrl==second.Connection.BaseUrl,"selection uses one provider's complete credentials");
    Check(first.Connection.ApiKey=="first-secret" && snapshot.Text.Model=="first-a" && snapshot.TextProviders[1].Connection.Model=="second-a","existing request and providers retain independent copies");
    Check(!legacy.SelectTextModel(first.Id,"second-b") && legacy.Text.ApiKey=="second-secret","reject model absent from provider without changing current request");
    Directory.CreateDirectory(folder);string path=Path.Combine(folder,"settings.json");var store=new SettingsStore(path);store.Save(legacy);string json=File.ReadAllText(path);
    Check(!json.Contains("first-secret") && !json.Contains("second-secret"),"all provider keys encrypted on disk");
    var loaded=store.Load();Check(loaded.TextProviders.Count==2 && loaded.ActiveTextProviderId==second.Id && loaded.Text.Model=="second-b","provider list and active model survive reload");
    Check(loaded.TextProviders[0].DisplayName("first-a")=="日常聊天" && loaded.TextProviders[1].DisplayName("second-b")=="深度思考" && loaded.TextProviders[1].DisplayName("second-a")=="second-a","Chinese display names persist with raw ID fallback");
    Check(loaded.TextProviders[1].BrandId=="deepseek" && snapshot.TextProviders[1].BrandId=="deepseek" && loaded.TextProviders[1].Name=="Second","chosen manufacturer survives custom gateways, copy and reload without renaming custom services");
    legacy.TextProviders[0].ModelDisplayNames["first-a"]="已修改";Check(snapshot.TextProviders[0].DisplayName("first-a")=="日常聊天","alias maps are deep copied");
    loaded.TextProviders[0].ModelDisplayNames["missing"]="陈旧别名";loaded.Normalize();Check(!loaded.TextProviders[0].ModelDisplayNames.ContainsKey("missing"),"normalization drops orphaned aliases");
    Check(loaded.TextProviders[0].Connection.ApiKey=="first-secret" && loaded.TextProviders[1].Connection.ApiKey=="second-secret","independent secrets survive reload");
    loaded.TextProviders[1].Models.Add(" third ");loaded.TextProviders[1].Models.Add("third");loaded.TextProviders.Add(null);loaded.Normalize();Check(loaded.TextProviders.Count==2 && loaded.TextProviders[1].Models.FindAll(m=>m=="third").Count==1,"normalization removes null and duplicate model entries");
    loaded.Text.Model="external-current";loaded.EnsureTextProviders();Check(loaded.Text.Model=="external-current","normalization never replaces active Text snapshot");
    var oldAudio=new PetSettings{PersonaProfileVersion=1,VoiceUsesTextService=true,SpeechUsesTextService=true,Text=new ConnectionSettings{BaseUrl="https://api.xiaomimimo.com/v1",ApiKey="old-shared-secret",UseProxy=true,ProxyAddress="127.0.0.1:17890"},Transcription=new ConnectionSettings{Model="mimo-v2.5-asr",Protocol="mimo"},Speech=new ConnectionSettings{Model="mimo-v2.5-tts",Protocol="mimo"},VoiceName="Mia"};
    using(var stream=File.Create(path))new DataContractJsonSerializer(typeof(PetSettings)).WriteObject(stream,oldAudio);
    var migratedAudio=store.Load();Check(!migratedAudio.VoiceUsesTextService && !migratedAudio.SpeechUsesTextService,"legacy reuse switches are cleared after loading");
    Check(migratedAudio.Transcription.ApiKey=="old-shared-secret" && migratedAudio.Speech.ApiKey=="old-shared-secret","legacy effective keys migrate into independent ASR and TTS settings");
    Check(migratedAudio.Speech.UseProxy && ProxyConfiguration.Endpoint(migratedAudio.Speech.ProxyAddress).Port==17890,"migration retains the effective voice proxy");Check(migratedAudio.VoiceName=="Mia","migration retains the selected voice");
    foreach(string kind in new[]{"asr","speech","live"})migratedAudio.ProviderView(kind);
    var audioCopy=migratedAudio.Copy();audioCopy.AudioProviders["asr"].Providers[0].Connection.Model="copy-only";Check(migratedAudio.AudioProviders["asr"].Providers[0].Connection.Model=="mimo-v2.5-asr","audio provider configurations are deep copied");
    migratedAudio.Text.ApiKey="replacement-key";migratedAudio.Text.BaseUrl="https://new.example/v1";store.Save(migratedAudio);var independent=store.Load();
    Check(independent.TranscriptionConnection().ApiKey=="old-shared-secret" && independent.SpeechConnection().ApiKey=="old-shared-secret" && independent.SpeechConnection().BaseUrl=="https://api.xiaomimimo.com/v1" && !File.ReadAllText(path).Contains("old-shared-secret") && !File.ReadAllText(path).Contains("SpeechUsesTextService"),"later text-service changes do not alter independent audio credentials and retired reuse switches are not saved");
    Console.WriteLine("ProviderTests: "+passed+" passed");return 0;
   }catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}
   finally {if(Directory.Exists(folder))Directory.Delete(folder,true);}
  }
  static void Check(bool ok,string name){if(!ok)throw new Exception(name);passed++;Console.WriteLine("PASS "+name);}
 }
}
