using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;

namespace Dafeiyu
{
    [DataContract]
    public sealed class PersonaVoiceProfile
    {
        [DataMember] public Dictionary<string,string> Speech { get; set; }
        [DataMember] public Dictionary<string,string> Live { get; set; }
        public PersonaVoiceProfile() { Speech=new Dictionary<string,string>();Live=new Dictionary<string,string>(); }
        public void Normalize() { Speech=Clean(Speech);Live=Clean(Live); }
        static Dictionary<string,string> Clean(Dictionary<string,string> source)
        {
            var result=new Dictionary<string,string>();if(source==null)return result;
            foreach(var pair in source) {
                if(result.Count>=8)break;
                if(String.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length>128 || pair.Value==null || pair.Value.Length>512)continue;
                result[pair.Key]=pair.Value.Trim();
            }
            return result;
        }
        public PersonaVoiceProfile Copy() { return new PersonaVoiceProfile{Speech=Clean(Speech),Live=Clean(Live)}; }
    }
    [DataContract]
    public sealed class ConnectionSettings
    {
        [DataMember] public string BaseUrl { get; set; }
        [DataMember] public string Model { get; set; }
        [DataMember] public string EncryptedKey { get; set; }
        [DataMember] public string Protocol { get; set; }
        [DataMember] public bool StreamReplies { get; set; }
        [OnDeserializing] private void BeforeDeserialize(StreamingContext context) { StreamReplies=true; }
        [DataMember] public bool UseProxy { get; set; }
        [DataMember] public string ProxyAddress { get; set; }
        public ConnectionSettings() { StreamReplies=true; BaseUrl = ""; Model = ""; EncryptedKey = ""; Protocol = "auto"; ProxyAddress=ProxyConfiguration.DefaultAddress; }
        public string ApiKey
        {
            get
            {
                if (String.IsNullOrEmpty(EncryptedKey)) return "";
                try { return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(EncryptedKey), null, DataProtectionScope.CurrentUser)); }
                catch (CryptographicException) { return ""; }
                catch (FormatException) { return ""; }
            }
            set
            {
                string key = (value ?? "").Trim();
                EncryptedKey = key.Length == 0 ? "" : Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(key), null, DataProtectionScope.CurrentUser));
            }
        }
        public ConnectionSettings Copy()
        {
            return new ConnectionSettings { BaseUrl = BaseUrl, Model = Model, EncryptedKey = EncryptedKey, Protocol = Protocol, StreamReplies=StreamReplies, UseProxy = UseProxy, ProxyAddress=ProxyAddress };
        }
        public void Normalize() { BaseUrl = (BaseUrl ?? "").Trim(); Model = (Model ?? "").Trim(); EncryptedKey = EncryptedKey ?? ""; Protocol = String.IsNullOrWhiteSpace(Protocol) ? "auto" : Protocol; ProxyAddress=ProxyConfiguration.Normalize(ProxyAddress); }
    }

    [DataContract]
    public sealed class TextProviderSettings
    {
        [DataMember] public string Id { get; set; }
        [DataMember] public string Name { get; set; }
        [DataMember] public ConnectionSettings Connection { get; set; }
        [DataMember] public List<string> Models { get; set; }
        [DataMember] public Dictionary<string,string> ModelDisplayNames { get; set; }
        public TextProviderSettings() { Id=Guid.NewGuid().ToString("N");Name="自定义服务";Connection=new ConnectionSettings();Models=new List<string>();ModelDisplayNames=new Dictionary<string,string>(); }
        public void Normalize()
        {
            if(String.IsNullOrWhiteSpace(Id) || Id.Length>128)Id=Guid.NewGuid().ToString("N");
            Name=String.IsNullOrWhiteSpace(Name)?"自定义服务":Name.Trim();if(Name.Length>80)Name=Name.Substring(0,80);
            Connection=Connection ?? new ConnectionSettings();Connection.Normalize();
            var clean=new List<string>();
            if(Models!=null)foreach(string model in Models) { string value=(model ?? "").Trim();if(value.Length>0 && value.Length<=256 && !clean.Contains(value) && clean.Count<100)clean.Add(value); }
            string selected=Connection.Model;
            if(selected.Length>0 && selected.Length<=256 && !clean.Contains(selected)) { if(clean.Count>=100)clean.RemoveAt(clean.Count-1);clean.Add(selected); }
            if(!clean.Contains(selected))Connection.Model=clean.Count>0?clean[0]:"";
            Models=clean;
            var labels=new Dictionary<string,string>(StringComparer.Ordinal);
            if(ModelDisplayNames!=null)foreach(var pair in ModelDisplayNames){string label=(pair.Value ?? "").Trim();if(clean.Contains(pair.Key) && label.Length>0){if(label.Length>80)label=label.Substring(0,80);labels[pair.Key]=label;}}
            ModelDisplayNames=labels;
        }
        public string DisplayName(string model) {string label;return ModelDisplayNames!=null && model!=null && ModelDisplayNames.TryGetValue(model,out label) && !String.IsNullOrWhiteSpace(label)?label:model ?? "";}
        public TextProviderSettings Copy() { return new TextProviderSettings {Id=Id,Name=Name,ModelDisplayNames=ModelDisplayNames==null?new Dictionary<string,string>():new Dictionary<string,string>(ModelDisplayNames),Connection=(Connection ?? new ConnectionSettings()).Copy(),Models=Models==null?new List<string>():new List<string>(Models)}; }
        public override string ToString() {return Name;}
    }

    [DataContract]
    public sealed class PetSettings
    {
        public const int OnboardingLastStep=15;
        [DataMember] public string OnboardingState { get; set; }
        [DataMember] public int OnboardingStep { get; set; }
        [DataMember] public int OnboardingVersion { get; set; }
        [DataMember] public bool SaveChatHistory { get; set; }
        [DataMember] public bool QuietMode { get; set; }
        [DataMember] public bool HideOnFullscreen { get; set; }
        [DataMember] public bool ClickThrough { get; set; }
        [DataMember] public bool LockPosition { get; set; }
        [DataMember] public Dictionary<string,string> Hotkeys { get; set; }
        [DataMember] public double Scale { get; set; }
        [DataMember] public int Volume { get; set; }
        [DataMember] public bool Topmost { get; set; }
        [DataMember] public bool FollowCursor { get; set; }
        [DataMember] public bool Wander { get; set; }
        [DataMember] public bool GreetingEnabled { get; set; }
        [DataMember] public bool AiGreetingEnabled { get; set; }
        [DataMember] public int GreetingMinMinutes { get; set; }
        [DataMember] public int GreetingMaxMinutes { get; set; }
        [DataMember] public string SkinId { get; set; }
        [DataMember] public double? X { get; set; }
        [DataMember] public double? Y { get; set; }
        [DataMember] public bool ReadReplies { get; set; }
        [DataMember] public bool UseCloudVoice { get; set; }
        [DataMember] public bool VoiceUsesTextService { get; set; }
        [DataMember] public bool SpeechUsesTextService { get; set; }
        [DataMember] public string VoiceMode { get; set; }
        [DataMember] public string LiveVoice { get; set; }
        [DataMember] public ConnectionSettings Live { get; set; }
        [DataMember] public Dictionary<string,string> PersonaOverrides { get; set; }
        [DataMember] public int PersonaProfileVersion { get; set; }
        [DataMember] public int VoiceProfileVersion { get; set; }
        [DataMember] public Dictionary<string,PersonaVoiceProfile> VoiceProfiles { get; set; }
        [DataMember] public string RepositoryUrl { get; set; }
        [DataMember] public bool UpdateUseProxy { get; set; }
        [DataMember] public string UpdateProxyAddress { get; set; }
        public const string DefaultUpdateMirror="https://gh-proxy.org";
        [DataMember] public bool UpdateUseMirror { get; set; }
        [DataMember] public string UpdateMirrorAddress { get; set; }
        [DataMember] public bool AutoUpdate { get; set; }
        [DataMember] public long LastUpdateCheckUtcTicks { get; set; }
        [DataMember] public string VoiceName { get; set; }
        [DataMember] public string SystemPrompt { get; set; }
        [DataMember] public ConnectionSettings Text { get; set; }
        [DataMember] public List<TextProviderSettings> TextProviders { get; set; }
        [DataMember] public string ActiveTextProviderId { get; set; }
        [DataMember] public ConnectionSettings Transcription { get; set; }
        [DataMember] public ConnectionSettings Speech { get; set; }
        public PetSettings() { Defaults(); }
        [OnDeserializing] private void BeforeDeserialize(StreamingContext context) { Defaults();OnboardingVersion=0;OnboardingState="legacy";LiveVoice="Kore"; }
        private void Defaults()
        {
            OnboardingState="new";OnboardingStep=0;OnboardingVersion=1;
            SaveChatHistory=true; QuietMode=false; HideOnFullscreen=true; ClickThrough=false; LockPosition=false;
            Hotkeys=new Dictionary<string,string> { {"quick","Ctrl+Alt+Space"},{"hide","Ctrl+Alt+H"},{"quiet","Ctrl+Alt+D"},{"passthrough","Ctrl+Alt+P"} };
            Scale = 1.25; Volume = 50; Topmost = true; FollowCursor = true; Wander = false;
            GreetingEnabled=true; AiGreetingEnabled=false; GreetingMinMinutes=30; GreetingMaxMinutes=60;
            SkinId = "e-bao";
            ReadReplies = true; UseCloudVoice = false; VoiceUsesTextService = false; VoiceName = "";
            TextProviders=new List<TextProviderSettings>();ActiveTextProviderId="";
            Text = new ConnectionSettings(); Transcription = new ConnectionSettings(); Speech = new ConnectionSettings();
            Live = new ConnectionSettings { BaseUrl="wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent", Model="gemini-3.8-live", Protocol="gemini-live" };
            VoiceMode="pipeline"; LiveVoice="Puck"; PersonaOverrides=new Dictionary<string,string>();
            VoiceProfiles=new Dictionary<string,PersonaVoiceProfile>();
            RepositoryUrl=ReleaseInfo.RepositoryUrl;
            UpdateProxyAddress=ProxyConfiguration.DefaultAddress;
            UpdateMirrorAddress=DefaultUpdateMirror;
            AutoUpdate=true;
            SystemPrompt = "你是{pet_name}，一位温和、俏皮的桌面伙伴。用自然的中文与用户对话，帮助写代码、做设计和整理思路。回答清楚简洁，如实表达不确定性。你不能直接看见用户桌面，也不会自动读取文件。";
        }
        public void Normalize()
        {
            if(OnboardingState!="new" && OnboardingState!="deferred" && OnboardingState!="skipped" && OnboardingState!="completed")OnboardingState="completed";
            // Version 1 inserts the double-click lesson before the old service steps.
            if(OnboardingVersion<1){OnboardingStep=Math.Max(0,Math.Min(12,OnboardingStep));if(OnboardingStep>=7)OnboardingStep++;OnboardingVersion=1;}
            OnboardingStep=Math.Max(0,Math.Min(OnboardingLastStep,OnboardingStep));
            Hotkeys=Hotkeys ?? new Dictionary<string,string>();
            foreach(var pair in new Dictionary<string,string> { {"quick","Ctrl+Alt+Space"},{"hide","Ctrl+Alt+H"},{"quiet","Ctrl+Alt+D"},{"passthrough","Ctrl+Alt+P"} })
                if(!Hotkeys.ContainsKey(pair.Key))Hotkeys[pair.Key]=pair.Value; else Hotkeys[pair.Key]=(Hotkeys[pair.Key] ?? "").Trim();
            if (Double.IsNaN(Scale) || Double.IsInfinity(Scale)) Scale = 1.25;
            Scale = Math.Max(.75, Math.Min(2.25, Scale)); Volume = Math.Max(0, Math.Min(100, Volume));
            GreetingMinMinutes=Math.Max(1,Math.Min(1440,GreetingMinMinutes));
            GreetingMaxMinutes=Math.Max(GreetingMinMinutes,Math.Min(1440,GreetingMaxMinutes));
            if (X.HasValue && (Double.IsNaN(X.Value) || Double.IsInfinity(X.Value))) X = null;
            if (Y.HasValue && (Double.IsNaN(Y.Value) || Double.IsInfinity(Y.Value))) Y = null;
            Text = Text ?? new ConnectionSettings(); Transcription = Transcription ?? new ConnectionSettings(); Speech = Speech ?? new ConnectionSettings();
            Text.Normalize(); Transcription.Normalize(); Speech.Normalize();EnsureTextProviders();
            Live=Live ?? new ConnectionSettings(); Live.Normalize(); PersonaOverrides=PersonaOverrides ?? new Dictionary<string,string>();
            VoiceMode=VoiceMode=="live" ? "live" : "pipeline"; LiveVoice=String.IsNullOrWhiteSpace(LiveVoice) ? "Kore" : LiveVoice.Trim();
            RepositoryUrl=ReleaseInfo.RepositoryUrl;
            UpdateProxyAddress=ProxyConfiguration.Normalize(UpdateProxyAddress);
            UpdateMirrorAddress=String.IsNullOrWhiteSpace(UpdateMirrorAddress)?DefaultUpdateMirror:UpdateMirrorAddress.Trim().TrimEnd('/');
            if(UpdateUseMirror)UpdateUseProxy=false;
            if(LastUpdateCheckUtcTicks<0 || LastUpdateCheckUtcTicks>DateTime.UtcNow.AddDays(1).Ticks)LastUpdateCheckUtcTicks=0;
            // Older MiMo configurations reused the text endpoint only for ASR.
            if(PersonaProfileVersion==0 && Speech.Model.StartsWith("mimo-",StringComparison.OrdinalIgnoreCase) && String.IsNullOrWhiteSpace(Speech.BaseUrl) && String.IsNullOrEmpty(Speech.EncryptedKey)) SpeechUsesTextService=true;
            VoiceName = (VoiceName ?? "").Trim(); SystemPrompt = SystemPrompt ?? "";
            SkinId = String.IsNullOrWhiteSpace(SkinId) ? "e-bao" : SkinId;
            var profiles=new Dictionary<string,PersonaVoiceProfile>();
            if(VoiceProfiles!=null)foreach(var pair in VoiceProfiles) {
                if(profiles.Count>=32)break;
                if(String.IsNullOrWhiteSpace(pair.Key) || pair.Key.Length>256 || pair.Value==null)continue;
                pair.Value.Normalize();profiles[pair.Key]=pair.Value;
            }
            VoiceProfiles=profiles;VoiceProfileVersion=Math.Max(0,VoiceProfileVersion);
        }
        public void EnsureTextProviders()
        {
            Text=Text ?? new ConnectionSettings();Text.Normalize();
            var clean=new List<TextProviderSettings>();var ids=new HashSet<string>(StringComparer.Ordinal);
            if(TextProviders!=null)foreach(var provider in TextProviders) {
                if(provider==null || clean.Count>=32)continue;
                provider.Normalize();if(!ids.Add(provider.Id)){provider.Id=Guid.NewGuid().ToString("N");ids.Add(provider.Id);}clean.Add(provider);
            }
            if(clean.Count==0) {var initial=new TextProviderSettings{Name="默认服务",Connection=Text.Copy()};initial.Normalize();clean.Add(initial);}
            TextProviders=clean;
            if(!clean.Exists(p=>p.Id==ActiveTextProviderId))ActiveTextProviderId=clean[0].Id;
        }
        public bool SelectTextModel(string providerId,string model)
        {
            EnsureTextProviders();
            var provider=TextProviders.Find(p=>p.Id==providerId);
            if(provider==null || !provider.Models.Contains(model ?? ""))return false;
            provider.Connection.Model=model;Text=provider.Connection.Copy();ActiveTextProviderId=provider.Id;return true;
        }
        public ConnectionSettings TranscriptionConnection()
        {
            var copy = Transcription.Copy();
            if (VoiceUsesTextService) { copy.BaseUrl = Text.BaseUrl; copy.EncryptedKey = Text.EncryptedKey; copy.UseProxy=Text.UseProxy; copy.ProxyAddress=Text.ProxyAddress; }
            return copy;
        }
        public ConnectionSettings SpeechConnection()
        {
            var copy=Speech.Copy();
            if(SpeechUsesTextService) { copy.BaseUrl=Text.BaseUrl; copy.EncryptedKey=Text.EncryptedKey; copy.UseProxy=Text.UseProxy; copy.ProxyAddress=Text.ProxyAddress; }
            return copy;
        }
        public PetSettings Copy()
        {
            var voiceProfiles=new Dictionary<string,PersonaVoiceProfile>();
            if(VoiceProfiles!=null)foreach(var pair in VoiceProfiles)if(pair.Value!=null)voiceProfiles[pair.Key]=pair.Value.Copy();
            var providers=new List<TextProviderSettings>();if(TextProviders!=null)foreach(var provider in TextProviders)if(provider!=null)providers.Add(provider.Copy());
            return new PetSettings { TextProviders=providers,ActiveTextProviderId=ActiveTextProviderId,OnboardingState=OnboardingState,OnboardingStep=OnboardingStep,OnboardingVersion=OnboardingVersion,SaveChatHistory=SaveChatHistory,QuietMode=QuietMode,HideOnFullscreen=HideOnFullscreen,ClickThrough=ClickThrough,LockPosition=LockPosition,Hotkeys=new Dictionary<string,string>(Hotkeys),Scale=Scale,Volume=Volume,Topmost=Topmost,FollowCursor=FollowCursor,Wander=Wander,SkinId=SkinId,X=X,Y=Y,
                GreetingEnabled=GreetingEnabled,AiGreetingEnabled=AiGreetingEnabled,GreetingMinMinutes=GreetingMinMinutes,GreetingMaxMinutes=GreetingMaxMinutes,
                ReadReplies=ReadReplies,UseCloudVoice=UseCloudVoice,VoiceUsesTextService=VoiceUsesTextService,VoiceName=VoiceName,SystemPrompt=SystemPrompt,
                SpeechUsesTextService=SpeechUsesTextService,VoiceMode=VoiceMode,LiveVoice=LiveVoice,Live=Live.Copy(),PersonaProfileVersion=PersonaProfileVersion,VoiceProfileVersion=VoiceProfileVersion,VoiceProfiles=voiceProfiles,
                PersonaOverrides=new Dictionary<string,string>(PersonaOverrides),RepositoryUrl=RepositoryUrl,UpdateUseProxy=UpdateUseProxy,UpdateProxyAddress=UpdateProxyAddress,UpdateUseMirror=UpdateUseMirror,UpdateMirrorAddress=UpdateMirrorAddress,AutoUpdate=AutoUpdate,LastUpdateCheckUtcTicks=LastUpdateCheckUtcTicks,Text=Text.Copy(),Transcription=Transcription.Copy(),Speech=Speech.Copy() };
        }
    }

    public sealed class SettingsStore
    {
        private readonly string path;
        public string Warning { get; private set; }
        public string DirectoryPath { get { return Path.GetDirectoryName(path); } }
        public SettingsStore(string filePath) { path = Path.GetFullPath(filePath); }
        public PetSettings Load()
        {
            Warning = "";
            if (!File.Exists(path)) return new PetSettings();
            try
            {
                using (var stream = File.OpenRead(path))
                {
                    if (stream.Length > 1024 * 1024) throw new SerializationException("Settings too large.");
                    var settings = (PetSettings)new DataContractJsonSerializer(typeof(PetSettings)).ReadObject(stream);
                    if (settings == null) throw new SerializationException("Empty settings.");
                    settings.Normalize();
                    if ((!String.IsNullOrEmpty(settings.Text.EncryptedKey) && settings.Text.ApiKey.Length == 0) ||
                        (!String.IsNullOrEmpty(settings.Transcription.EncryptedKey) && settings.Transcription.ApiKey.Length == 0) ||
                        (!String.IsNullOrEmpty(settings.Speech.EncryptedKey) && settings.Speech.ApiKey.Length == 0) ||
                        (!String.IsNullOrEmpty(settings.Live.EncryptedKey) && settings.Live.ApiKey.Length == 0))
                        Warning = "已保存的密钥无法在当前 Windows 用户下解密，请重新填写。";
                    foreach(var provider in settings.TextProviders)if(!String.IsNullOrEmpty(provider.Connection.EncryptedKey) && provider.Connection.ApiKey.Length==0)Warning="已保存的密钥无法在当前 Windows 用户下解密，请重新填写。";
                    return settings;
                }
            }
            catch (Exception e)
            {
                if (!(e is SerializationException || e is IOException || e is UnauthorizedAccessException || e is ArgumentException)) throw;
                Warning = "设置文件无法读取，已恢复默认设置。";
                try { File.Copy(path, path + ".invalid-" + DateTime.Now.ToString("yyyyMMddHHmmssfff"), false); }
                catch (IOException) { Warning += " 原文件未改动。"; }
                catch (UnauthorizedAccessException) { Warning += " 原文件未改动。"; }
                return new PetSettings();
            }
        }
        public void Save(PetSettings settings)
        {
            settings.Normalize(); Directory.CreateDirectory(DirectoryPath);
            string temporary = path + ".tmp";
            try
            {
                using (var stream = new FileStream(temporary, FileMode.Create, FileAccess.Write, FileShare.None))
                { new DataContractJsonSerializer(typeof(PetSettings)).WriteObject(stream, settings); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, null);
                else File.Move(temporary, path);
                Warning = "";
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
