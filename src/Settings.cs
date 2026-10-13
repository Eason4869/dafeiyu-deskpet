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
    public sealed class ConnectionSettings
    {
        [DataMember] public string BaseUrl { get; set; }
        [DataMember] public string Model { get; set; }
        [DataMember] public string EncryptedKey { get; set; }
        [DataMember] public string Protocol { get; set; }
        [DataMember] public bool UseProxy { get; set; }
        [DataMember] public string ProxyAddress { get; set; }
        public ConnectionSettings() { BaseUrl = ""; Model = ""; EncryptedKey = ""; Protocol = "auto"; ProxyAddress=ProxyConfiguration.DefaultAddress; }
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
            return new ConnectionSettings { BaseUrl = BaseUrl, Model = Model, EncryptedKey = EncryptedKey, Protocol = Protocol, UseProxy = UseProxy, ProxyAddress=ProxyAddress };
        }
        public void Normalize() { BaseUrl = (BaseUrl ?? "").Trim(); Model = (Model ?? "").Trim(); EncryptedKey = EncryptedKey ?? ""; Protocol = String.IsNullOrWhiteSpace(Protocol) ? "auto" : Protocol; ProxyAddress=ProxyConfiguration.Normalize(ProxyAddress); }
    }

    [DataContract]
    public sealed class PetSettings
    {
        [DataMember] public double Scale { get; set; }
        [DataMember] public int Volume { get; set; }
        [DataMember] public bool Topmost { get; set; }
        [DataMember] public bool FollowCursor { get; set; }
        [DataMember] public bool Wander { get; set; }
        [DataMember] public bool GreetingEnabled { get; set; }
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
        [DataMember] public string RepositoryUrl { get; set; }
        [DataMember] public bool UpdateUseProxy { get; set; }
        [DataMember] public string UpdateProxyAddress { get; set; }
        [DataMember] public bool AutoUpdate { get; set; }
        [DataMember] public long LastUpdateCheckUtcTicks { get; set; }
        [DataMember] public string VoiceName { get; set; }
        [DataMember] public string SystemPrompt { get; set; }
        [DataMember] public ConnectionSettings Text { get; set; }
        [DataMember] public ConnectionSettings Transcription { get; set; }
        [DataMember] public ConnectionSettings Speech { get; set; }
        public PetSettings() { Defaults(); }
        [OnDeserializing] private void BeforeDeserialize(StreamingContext context) { Defaults(); }
        private void Defaults()
        {
            Scale = 1.25; Volume = 50; Topmost = true; FollowCursor = true; Wander = false;
            GreetingEnabled=true; GreetingMinMinutes=30; GreetingMaxMinutes=60;
            SkinId = "e-bao";
            ReadReplies = true; UseCloudVoice = false; VoiceUsesTextService = false; VoiceName = "";
            Text = new ConnectionSettings(); Transcription = new ConnectionSettings(); Speech = new ConnectionSettings();
            Live = new ConnectionSettings { BaseUrl="wss://generativelanguage.googleapis.com/ws/google.ai.generativelanguage.v1beta.GenerativeService.BidiGenerateContent", Model="gemini-3.8-live", Protocol="gemini-live" };
            VoiceMode="pipeline"; LiveVoice="Kore"; PersonaOverrides=new Dictionary<string,string>();
            RepositoryUrl=ReleaseInfo.RepositoryUrl;
            UpdateProxyAddress=ProxyConfiguration.DefaultAddress;
            AutoUpdate=true;
            SystemPrompt = "你是{pet_name}，一位温和、俏皮的桌面伙伴。用自然的中文与用户对话，帮助写代码、做设计和整理思路。回答清楚简洁，如实表达不确定性。你不能直接看见用户桌面，也不会自动读取文件。";
        }
        public void Normalize()
        {
            if (Double.IsNaN(Scale) || Double.IsInfinity(Scale)) Scale = 1.25;
            Scale = Math.Max(.75, Math.Min(2.25, Scale)); Volume = Math.Max(0, Math.Min(100, Volume));
            GreetingMinMinutes=Math.Max(1,Math.Min(1440,GreetingMinMinutes));
            GreetingMaxMinutes=Math.Max(GreetingMinMinutes,Math.Min(1440,GreetingMaxMinutes));
            if (X.HasValue && (Double.IsNaN(X.Value) || Double.IsInfinity(X.Value))) X = null;
            if (Y.HasValue && (Double.IsNaN(Y.Value) || Double.IsInfinity(Y.Value))) Y = null;
            Text = Text ?? new ConnectionSettings(); Transcription = Transcription ?? new ConnectionSettings(); Speech = Speech ?? new ConnectionSettings();
            Text.Normalize(); Transcription.Normalize(); Speech.Normalize();
            Live=Live ?? new ConnectionSettings(); Live.Normalize(); PersonaOverrides=PersonaOverrides ?? new Dictionary<string,string>();
            VoiceMode=VoiceMode=="live" ? "live" : "pipeline"; LiveVoice=String.IsNullOrWhiteSpace(LiveVoice) ? "Kore" : LiveVoice.Trim();
            RepositoryUrl=ReleaseInfo.RepositoryUrl;
            UpdateProxyAddress=ProxyConfiguration.Normalize(UpdateProxyAddress);
            if(LastUpdateCheckUtcTicks<0 || LastUpdateCheckUtcTicks>DateTime.UtcNow.AddDays(1).Ticks)LastUpdateCheckUtcTicks=0;
            // Older MiMo configurations reused the text endpoint only for ASR.
            if(PersonaProfileVersion==0 && Speech.Model.StartsWith("mimo-",StringComparison.OrdinalIgnoreCase) && String.IsNullOrWhiteSpace(Speech.BaseUrl) && String.IsNullOrEmpty(Speech.EncryptedKey)) SpeechUsesTextService=true;
            VoiceName = (VoiceName ?? "").Trim(); SystemPrompt = SystemPrompt ?? "";
            SkinId = String.IsNullOrWhiteSpace(SkinId) ? "e-bao" : SkinId;
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
            return new PetSettings { Scale=Scale,Volume=Volume,Topmost=Topmost,FollowCursor=FollowCursor,Wander=Wander,SkinId=SkinId,X=X,Y=Y,
                GreetingEnabled=GreetingEnabled,GreetingMinMinutes=GreetingMinMinutes,GreetingMaxMinutes=GreetingMaxMinutes,
                ReadReplies=ReadReplies,UseCloudVoice=UseCloudVoice,VoiceUsesTextService=VoiceUsesTextService,VoiceName=VoiceName,SystemPrompt=SystemPrompt,
                SpeechUsesTextService=SpeechUsesTextService,VoiceMode=VoiceMode,LiveVoice=LiveVoice,Live=Live.Copy(),PersonaProfileVersion=PersonaProfileVersion,
                PersonaOverrides=new Dictionary<string,string>(PersonaOverrides),RepositoryUrl=RepositoryUrl,UpdateUseProxy=UpdateUseProxy,UpdateProxyAddress=UpdateProxyAddress,AutoUpdate=AutoUpdate,LastUpdateCheckUtcTicks=LastUpdateCheckUtcTicks,Text=Text.Copy(),Transcription=Transcription.Copy(),Speech=Speech.Copy() };
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
