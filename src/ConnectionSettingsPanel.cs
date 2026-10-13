using System;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
namespace Dafeiyu
{
    public sealed class ConnectionSettingsPanel : Grid
    {
        readonly PetWindow pet;readonly ConnectionEditor text,recognition,synthesis,live;readonly TextBox voice,liveVoice;
        readonly CheckBox reuse,reuseSpeech,cloud;readonly TextBlock notice;readonly ContentControl page=new ContentControl();readonly StackPanel[] sections;
        public ConnectionSettingsPanel(PetWindow companion)
        {
            pet=companion;Margin=new Thickness(24,22,24,20);RowDefinitions.Add(new RowDefinition());RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});Children.Add(page);
            text=new ConnectionEditor("对话模型","TextService","text",pet.Settings.Text);recognition=new ConnectionEditor("语音识别","Transcription","asr",pet.Settings.Transcription);synthesis=new ConnectionEditor("声音与朗读","Speech","speech",pet.Settings.Speech);live=new ConnectionEditor("Live 实时语音","LiveService","live",pet.Settings.Live);
            reuse=Ui.Toggle("复用文字服务的地址、密钥和代理",pet.Settings.VoiceUsesTextService,v=>recognition.SetAddressEnabled(!v));reuse.Name="ReuseTextForAsr";recognition.Children.Insert(2,reuse);recognition.SetAddressEnabled(!pet.Settings.VoiceUsesTextService);
            cloud=Ui.Toggle("使用云端 TTS",pet.Settings.UseCloudVoice,v=>SetCloud(v));cloud.Name="UseCloudVoice";synthesis.Children.Insert(2,cloud);
            reuseSpeech=Ui.Toggle("复用文字服务的地址、密钥和代理",pet.Settings.SpeechUsesTextService,v=>synthesis.SetAddressEnabled(!v && cloud.IsChecked==true));reuseSpeech.Name="ReuseTextForSpeech";synthesis.Children.Insert(3,reuseSpeech);
            voice=Ui.Field(pet.Settings.VoiceName);voice.Name="SpeechVoice";Ui.Label(synthesis,"音色名称",voice);synthesis.Children.Add(Ui.Text("MiMo：苏打、白桦、冰糖、茉莉、mimo_default。\n关闭云端 TTS 时使用 Windows 系统语音。",11,Ui.Muted));SetCloud(pet.Settings.UseCloudVoice);
            var sound=new StackPanel{Margin=new Thickness(0,16,0,0)};
            sound.Children.Add(Ui.Toggle("自动朗读文字回复",pet.Settings.ReadReplies,v=>{pet.Settings.ReadReplies=v;if(!v)pet.StopReplySpeech();pet.SaveSettings();}));
            var label=Ui.Text("音量 · "+pet.Settings.Volume+"%",12,Ui.Blue);sound.Children.Add(label);
            var volume=new Slider{Name="VoiceVolume",Minimum=0,Maximum=100,TickFrequency=5,IsSnapToTickEnabled=true,Value=pet.Settings.Volume,Margin=new Thickness(0,8,0,12)};
            volume.ValueChanged+=delegate {int value=(int)Math.Round(volume.Value);label.Text="音量 · "+value+"%";pet.Settings.Volume=value;pet.UpdateVoiceVolume(value);pet.SaveSettings();};sound.Children.Add(volume);synthesis.Children.Add(sound);
            liveVoice=Ui.Field(pet.Settings.LiveVoice);liveVoice.Name="LiveVoice";Ui.Label(live,"Live 音色",liveVoice);live.Children.Add(Ui.Text("例如 Kore。模型与音色以服务账号可用范围为准。\n在聊天窗口切换为 Live 后点击开始，连接成功才会打开麦克风；再次点击结束。",12,Ui.Muted));
            var modes=new ComboBox{Name="VoiceMode",ItemsSource=new[]{"ASR + TTS · 分步语音","Live · 实时语音"},SelectedIndex=pet.Settings.VoiceMode=="live" ? 1 : 0,MinHeight=38,Padding=new Thickness(8)};
            bool refreshingMode=false;
            modes.SelectionChanged+=delegate {if(refreshingMode)return;if(pet.DialogueBusy){refreshingMode=true;modes.SelectedIndex=pet.Settings.VoiceMode=="live"?1:0;refreshingMode=false;if(notice!=null)notice.Text="请先结束当前对话，再切换语音模式。";return;}pet.Settings.VoiceMode=modes.SelectedIndex==1 ? "live" : "pipeline";pet.SaveSettings();pet.RefreshChatAppearance();};Ui.Label(live,"聊天窗口默认语音模式",modes);
            sections=new[]{text,recognition,synthesis,live};ShowSection(0);
            var footer=new StackPanel{Margin=new Thickness(0,14,0,0)};var row=new WrapPanel();var save=Ui.Button("保存服务设置",true);save.Name="SaveConnections";save.Click+=delegate {Save();};row.Children.Add(save);
            var preview=Ui.Button("试听朗读");preview.Name="PreviewSpeech";preview.Click+=async delegate {if(!Save())return;preview.IsEnabled=false;try{if(pet.LiveActive)throw new InvalidOperationException("请先结束 Live 再试听。");await pet.Conversation.PreviewSpeechAsync(pet.Settings,pet.Skin.Name);notice.Text=pet.Conversation.LastNotice;}catch(Exception e){ShowError(e);}finally{preview.IsEnabled=true;}};row.Children.Add(preview);
            var stop=Ui.Button("停止朗读");stop.Click+=delegate {pet.StopReplySpeech();};row.Children.Add(stop);footer.Children.Add(row);
            notice=Ui.Text("连接设置需点击保存。密钥由当前 Windows 用户加密存储。",11,Ui.Muted);notice.Name="ConnectionNotice";notice.Margin=new Thickness(0,10,0,0);footer.Children.Add(notice);Grid.SetRow(footer,1);Children.Add(footer);
        }
        public void ShowSection(int index){var previous=page.Content as ScrollViewer;if(previous!=null)previous.Content=null;page.Content=Ui.Scroll(sections[index]);}
        void SetCloud(bool value){synthesis.SetFieldsEnabled(value);if(reuseSpeech!=null)synthesis.SetAddressEnabled(value && reuseSpeech.IsChecked!=true);if(voice!=null)voice.IsEnabled=value;if(reuseSpeech!=null)reuseSpeech.IsEnabled=value;}
        public bool Save()
        {
            try
            {
                var nextText=text.Read();var nextAsr=recognition.Read();var nextSpeech=synthesis.Read();var nextLive=live.Read();
                ValidateAddress(nextText,ApiClient.ChatPath(nextText));if(reuse.IsChecked!=true)ValidateAddress(nextAsr,ApiClient.AsrPath(nextAsr));if(reuseSpeech.IsChecked!=true)ValidateAddress(nextSpeech,ApiClient.SpeechPath(nextSpeech));
                var probe=nextLive.Copy();if(!String.IsNullOrWhiteSpace(probe.BaseUrl)){if(String.IsNullOrWhiteSpace(probe.Model))probe.Model="validation";if(String.IsNullOrEmpty(probe.ApiKey))probe.ApiKey="validation";GeminiLiveSocket.Endpoint(probe);}
                pet.Settings.Text=nextText;pet.Settings.Transcription=nextAsr;pet.Settings.Speech=nextSpeech;pet.Settings.Live=nextLive;pet.Settings.VoiceUsesTextService=reuse.IsChecked==true;pet.Settings.SpeechUsesTextService=reuseSpeech.IsChecked==true;pet.Settings.UseCloudVoice=cloud.IsChecked==true;pet.Settings.VoiceName=voice.Text.Trim();pet.Settings.LiveVoice=liveVoice.Text.Trim();pet.SaveSettings();pet.RefreshChatAppearance();notice.Text=String.IsNullOrEmpty(pet.Notice)?"已保存。打开对话开始使用。":pet.Notice;return String.IsNullOrEmpty(pet.Notice);
            }
            catch(Exception e){ShowError(e);return false;}
        }
        static void ValidateAddress(ConnectionSettings settings,string path)
        {if(String.IsNullOrWhiteSpace(settings.BaseUrl))return;var probe=settings.Copy();probe.Model="validation";ApiClient.Endpoint(probe,path);if(settings.ApiKey.IndexOfAny(new[]{'\r','\n'})>=0)throw new ApiException("密钥中有换行，请重新粘贴。");}
        void ShowError(Exception e){notice.Text=e is OperationCanceledException?"朗读已停止。":e is ApiException || e is InvalidOperationException?e.Message:e is CryptographicException?"当前 Windows 用户无法加密密钥。":"设置或朗读未完成，请检查配置与输出设备。";}
        sealed class ConnectionEditor : StackPanel
        {
            readonly TextBox address,model;readonly PasswordBox key;readonly ComboBox protocol;readonly CheckBox proxy;readonly string kind;readonly TextBlock hint;
            public ConnectionEditor(string title,string prefix,string serviceKind,ConnectionSettings settings)
            {
                kind=serviceKind;var heading=Ui.Text(title,25);heading.FontWeight=FontWeights.SemiBold;Children.Add(heading);Children.Add(Ui.Text(kind=="text" ? "选择常用服务，或接入自己的兼容接口。" : kind=="live" ? "边说边听，支持字幕与语音打断。" : kind=="asr" ? "把录音转成文字，再交给当前对话模型。" : "给伙伴选一副声音。",12,Ui.Muted));
                address=Ui.Field(settings.BaseUrl);address.Name=prefix+"Url";model=Ui.Field(settings.Model);model.Name=prefix+"Model";
                key=new PasswordBox{Name=prefix+"Key",Password=settings.ApiKey,MinHeight=38,Padding=new Thickness(10,7,10,7),BorderBrush=Ui.Line,Background=Brushes.White};
                protocol=new ComboBox{Name=prefix+"Protocol",ItemsSource=kind=="text" ? new[]{"OpenAI Chat Completions","Anthropic Messages"} : kind=="live" ? new[]{"Gemini Live WebSocket"} : new[]{"自动识别（MiMo 模型使用 MiMo 协议）","OpenAI Audio","MiMo Chat Audio"},MinHeight=38,Padding=new Thickness(8)};
                protocol.SelectedIndex=kind=="text" ? (settings.Protocol=="anthropic"?1:0) : kind=="live"?0:settings.Protocol=="mimo"?2:settings.Protocol=="openai"?1:0;
                proxy=Ui.Toggle("通过本机代理 127.0.0.1:7890 连接",settings.UseProxy,v=>{});proxy.Name=prefix+"Proxy";
                var presets=new ComboBox{Name=prefix+"Preset",ItemsSource=ServicePresets.For(kind),SelectedIndex=0,MinHeight=38,Padding=new Thickness(8)};Ui.Label(this,"服务预设",presets);
                hint=Ui.Text("可修改地址和模型。切换到其他服务域名时会清空密钥。",11,Ui.Muted);hint.Margin=new Thickness(0,7,0,3);Children.Add(hint);
                string keyOrigin=address.Text;
                key.PasswordChanged+=delegate {keyOrigin=address.Text;};
                address.TextChanged+=delegate {if(key.Password.Length>0 && !ServicePresets.SameOrigin(keyOrigin,address.Text)){key.Clear();hint.Text="服务地址已改变，请填写该服务的 API 密钥。";}};
                presets.SelectionChanged+=delegate {var preset=presets.SelectedItem as ServicePreset;if(preset==null || preset.Url.Length==0)return;if(!ServicePresets.SameOrigin(address.Text,preset.Url))key.Clear();address.Text=preset.Url;model.Text=preset.Model;protocol.SelectedIndex=kind=="text"?(preset.Protocol=="anthropic"?1:0):kind=="live"?0:preset.Protocol=="mimo"?2:1;hint.Text=preset.Note.Length==0 ? "已填入示例模型，请确认账号可用。" : preset.Note;};
                Ui.Label(this,"接口协议",protocol);Ui.Label(this,kind=="live"?"完整 WSS 地址":"服务地址（保留 /v1 等前缀）",address);Ui.Label(this,"模型名称",model);Ui.Label(this,"API 密钥",key);Children.Add(proxy);
            }
            public ConnectionSettings Read(){return new ConnectionSettings{BaseUrl=address.Text.Trim(),Model=model.Text.Trim(),ApiKey=key.Password.Trim(),Protocol=kind=="text"?(protocol.SelectedIndex==1?"anthropic":"openai"):kind=="live"?"gemini-live":protocol.SelectedIndex==2?"mimo":protocol.SelectedIndex==1?"openai":"auto",UseProxy=proxy.IsChecked==true};}
            public void SetAddressEnabled(bool value){address.IsEnabled=value;key.IsEnabled=value;proxy.IsEnabled=value;}
            public void SetFieldsEnabled(bool value){SetAddressEnabled(value);model.IsEnabled=value;protocol.IsEnabled=value;}
        }
    }
}
