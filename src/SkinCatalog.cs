using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Media.Imaging;

namespace Dafeiyu
{
    [DataContract] public sealed class SkinManifest
    {
        [DataMember(Name="version")] public int Version { get; set; }
        [DataMember(Name="name")] public string Name { get; set; }
        [DataMember(Name="atlas")] public string Atlas { get; set; }
        [DataMember(Name="system_prompt",EmitDefaultValue=false)] public string SystemPrompt { get; set; }
        [DataMember(Name="description",EmitDefaultValue=false)] public string Description { get; set; }
        [DataMember(Name="format",EmitDefaultValue=false)] public string Format { get; set; }
        [DataMember(Name="cell_width",EmitDefaultValue=false)] public int CellWidth { get; set; }
        [DataMember(Name="cell_height",EmitDefaultValue=false)] public int CellHeight { get; set; }
        [DataMember(Name="preview",EmitDefaultValue=false)] public string Preview { get; set; }
        [DataMember(Name="special_action",EmitDefaultValue=false)] public string SpecialAction { get; set; }
        [DataMember(Name="animations",EmitDefaultValue=false)] public Dictionary<string,IllustratedAnimation> Animations { get; set; }
        [DataMember(Name="gaze",EmitDefaultValue=false)] public IllustratedGaze Gaze { get; set; }
    }
    public sealed class SkinDefinition
    {
        private static readonly int[] MarfuRightStride={0,2,3,2,4,2,6,2};
        public SkinManifest Manifest { get; private set; }
        public bool Illustrated { get { return Manifest!=null && Manifest.Version==2; } }
        public string SpecialAction { get {
            if(Illustrated && !String.IsNullOrWhiteSpace(Manifest.SpecialAction))return Manifest.SpecialAction;
            if(Embedded){switch(Id){case "e-bao":return "傲娇扭头";case "dafeiyu":return "摇尾欢呼";case "marfu":return "挥杖流光";}}
            return "伙伴招牌";
        } }
        internal SkinDefinition(string id,string folder,SkinManifest manifest) : this(id,SkinCatalog.CleanName(manifest.Name),folder,manifest.Preview,false,manifest.SystemPrompt,manifest.Description) { Manifest=manifest; }
        public string Id { get; private set; }
        public string Name { get; private set; }
        public string Source { get; private set; }
        public string Preview { get; private set; }
        public bool Embedded { get; private set; }
        internal bool Bundled { get; set; }
        public string SystemPrompt { get; private set; }
        public string Description { get; private set; }
        public SkinDefinition(string id,string name,string source,string preview,bool embedded,string prompt=null,string description=null)
        {
            Id=id; Name=name; Source=source; Preview=preview; Embedded=embedded;
            SystemPrompt=String.IsNullOrWhiteSpace(prompt) ? (id=="dafeiyu" ? PersonaProfiles.Dafeiyu : id=="e-bao" ? PersonaProfiles.Ebao : PersonaProfiles.Generic(name)) : prompt.Trim();
            Description=description ?? (id=="e-bao" ? "傲娇毒舌 · 白发正太" : id=="dafeiyu" ? "温和俏皮 · 蓝白鲸鱼娘" : "你的专属桌面伙伴");
        }
        public override string ToString() { return Name; }
        public SpriteAtlas Load()
        {
            return Illustrated ? new SpriteAtlas(Source,Manifest) : new SpriteAtlas(Source,Embedded);
        }
        public int ActionFrame(int row,int phase)
        {
            // Emphasize Marfu's planted/tucked/extended poses instead of holding
            // several near-identical planted frames; every selection is real artwork.
            if(!Embedded || Id!="marfu" || phase<0 || phase>=8) return phase;
            return row==1 || row==2 ? MarfuRightStride[phase] : phase;
        }
        public int ActionRow(int row) { return MirrorsAction(row) ? 1 : row; }
        public bool MirrorsAction(int row) { return Embedded && (Id=="marfu" || Id=="dafeiyu") && row==2; }
        public AnimationPlayer CreateAnimation(int frameMultiplier=1)
        {
            double period=Embedded && (Id=="marfu" || Id=="e-bao" || Id=="dafeiyu") ? .22 : .11;
            double[] durations=null;
            if(Illustrated) durations=AnimationPlayer.States.Select(n=>Manifest.Animations[n].Loop ? 0 : Manifest.Animations[n].Frames/Manifest.Animations[n].Fps).ToArray();
            if(AdditionalInteractions.Has(this,"jumping")){if(durations==null)durations=new double[AnimationPlayer.States.Length];durations[4]=1;}
            return new AnimationPlayer(frameMultiplier,period,durations);
        }
    }
    public sealed class SkinCatalog
    {
        private static readonly Dictionary<string,BitmapSource> avatars=new Dictionary<string,BitmapSource>();
        private readonly string directory;
        private readonly List<SkinDefinition> skins=new List<SkinDefinition>();
        public IList<SkinDefinition> Skins { get { return skins.AsReadOnly(); } }
        public SkinCatalog(string dataDirectory)
        {
            directory=Path.Combine(dataDirectory,"skins");
            skins.Add(new SkinDefinition("e-bao","E包","Dafeiyu.EbaoAtlas","Dafeiyu.EbaoPreview",true));
            skins.Add(new SkinDefinition("dafeiyu","大肥鱼","Dafeiyu.Atlas","Dafeiyu.Preview",true));
            skins.Add(new SkinDefinition("marfu","马尔芙","Dafeiyu.MarfuAtlas","Dafeiyu.MarfuPreview",true,
                PersonaProfiles.Marfu,
                "聪明俏皮 · 金发绿眼魔法少女"));
            try {
                var bundled=BundledSkinCache.LoadXiaobei(dataDirectory);
                if(bundled!=null)skins.Add(bundled);
            } catch(Exception e) {
                if(!(e is IOException || e is UnauthorizedAccessException || e is SerializationException || e is ArgumentException || e is InvalidOperationException))throw;
                System.Diagnostics.Trace.WriteLine("Bundled Xiaobei could not be loaded: "+e.GetType().Name);
            }
            if(!Directory.Exists(directory)) return;
            foreach(string folder in Directory.GetDirectories(directory))
            {
                try
                {
                    var manifest=ReadManifest(Path.Combine(folder,"pet.json"));
                    if(manifest.Version==2) { IllustratedPack.Validate(folder,manifest,false);skins.Add(new SkinDefinition(Path.GetFileName(folder),folder,manifest));continue; }
                    string atlas=AtlasPath(folder,manifest.Atlas); ValidatePng(atlas);
                    skins.Add(new SkinDefinition(Path.GetFileName(folder),CleanName(manifest.Name),atlas,null,false,manifest.SystemPrompt,manifest.Description));
                }
                catch(Exception e)
                { if(!(e is IOException || e is UnauthorizedAccessException || e is SerializationException || e is ArgumentException || e is InvalidOperationException)) throw; }
            }
        }
        public SkinDefinition Find(string id) { return skins.FirstOrDefault(s=>s.Id==id) ?? skins[0]; }
        public SkinDefinition Import(string path)
        {
            string atlas,name,prompt=null,description=null;
            if(String.Equals(Path.GetExtension(path),".json",StringComparison.OrdinalIgnoreCase))
            { var m=ReadManifest(path); if(m.Version==2)return ImportIllustrated(path,m); atlas=AtlasPath(Path.GetDirectoryName(Path.GetFullPath(path)),m.Atlas); name=CleanName(m.Name); prompt=m.SystemPrompt; description=m.Description; }
            else
            { if(!String.Equals(Path.GetExtension(path),".png",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("请选择透明 PNG 精灵图或 pet.json 外观文件。"); atlas=path; name=CleanName(Path.GetFileNameWithoutExtension(path)); }
            ValidatePng(atlas);
            // Decode and validate every required frame before adding a saved design.
            try { new SpriteAtlas(atlas,false); }
            catch(Exception e)
            {
                if(!(e is FileFormatException || e is NotSupportedException || e is ArgumentException)) throw;
                throw new InvalidOperationException("PNG 图像无法解码，请重新导出完整的透明精灵图。",e);
            }
            string id="skin-"+Guid.NewGuid().ToString("N");
            string destination=Path.Combine(directory,id);
            Directory.CreateDirectory(destination);
            try
            {
                File.Copy(atlas,Path.Combine(destination,"spritesheet.png"),false);
                var manifest=new SkinManifest { Version=1,Name=name,Atlas="spritesheet.png",SystemPrompt=String.IsNullOrWhiteSpace(prompt) ? PersonaProfiles.Generic(name) : prompt,Description=description };
                using(var stream=File.Create(Path.Combine(destination,"pet.json"))) new DataContractJsonSerializer(typeof(SkinManifest)).WriteObject(stream,manifest);
                var skin=new SkinDefinition(id,name,Path.Combine(destination,"spritesheet.png"),null,false,manifest.SystemPrompt,description);
                skins.Add(skin); return skin;
            }
            catch
            {
                // This directory is a new GUID child created by this import alone.
                foreach(string file in Directory.GetFiles(destination)) File.Delete(file);
                Directory.Delete(destination); throw;
            }
        }
        private SkinDefinition ImportIllustrated(string path,SkinManifest manifest)
        {
            string source=Path.GetDirectoryName(Path.GetFullPath(path));
            var files=IllustratedPack.Validate(source,manifest,true);
            string id="skin-"+Guid.NewGuid().ToString("N"),destination=Path.Combine(directory,id);
            Directory.CreateDirectory(destination);
            try {
                foreach(string relative in files) {
                    string target=Path.Combine(destination,relative.Replace('/',Path.DirectorySeparatorChar));
                    Directory.CreateDirectory(Path.GetDirectoryName(target));
                    File.Copy(IllustratedPack.AssetPath(source,relative),target,false);
                }
                // Validate the copied bytes before publishing the catalog entry.
                IllustratedPack.Validate(destination,manifest,true);
                manifest.Name=CleanName(manifest.Name);
                using(var stream=File.Create(Path.Combine(destination,"pet.json"))) Serializer().WriteObject(stream,manifest);
                var skin=new SkinDefinition(id,destination,manifest);skins.Add(skin);return skin;
            } catch { Directory.Delete(destination,true);throw; }
        }
        internal static DataContractJsonSerializer Serializer()
        { return new DataContractJsonSerializer(typeof(SkinManifest),new DataContractJsonSerializerSettings {UseSimpleDictionaryFormat=true}); }
        private static SkinManifest ReadManifest(string path)
        {
            using(var stream=File.OpenRead(path))return ReadManifest(stream);
        }
        internal static SkinManifest ReadManifest(Stream stream)
        {
            if(stream.Length>65536) throw new InvalidOperationException("外观说明文件过大。");
            var result=(SkinManifest)Serializer().ReadObject(stream);
            if(result==null || (result.Version!=1 && result.Version!=2)) throw new InvalidOperationException("外观版本应为 1 或 2。");
            if((result.SystemPrompt ?? "").Length>8000 || (result.Description ?? "").Length>1000) throw new InvalidOperationException("人格最多 8000 字，简介最多 1000 字。");
            return result;
        }
        private static string AtlasPath(string folder,string filename)
        {
            if(String.IsNullOrWhiteSpace(filename) || filename!=Path.GetFileName(filename) || filename.IndexOfAny(new char[]{'/', '\\', ':'})>=0 || !String.Equals(Path.GetExtension(filename),".png",StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("atlas 必须是与 pet.json 同文件夹的 PNG 文件名。");
            return Path.Combine(folder,filename);
        }
        internal static string CleanName(string name)
        {
            string result=new string((name??"").Where(c=>!Char.IsControl(c)).ToArray()).Trim();
            return result.Length==0 ? "新伙伴" : result.Substring(0,Math.Min(32,result.Length));
        }
        public static void ValidatePng(string path)
        {
            using(var stream=File.OpenRead(path))
            {
                if(stream.Length>25*1024*1024 || stream.Length<33) throw new InvalidOperationException("PNG 文件大小应小于 25 MB。");
                byte[] header=new byte[33]; if(stream.Read(header,0,header.Length)!=header.Length) throw new InvalidOperationException("PNG 文件不完整。");
                byte[] signature={137,80,78,71,13,10,26,10};
                if(!header.Take(8).SequenceEqual(signature) || Encoding.ASCII.GetString(header,12,4)!="IHDR") throw new InvalidOperationException("文件不是有效的 PNG 精灵图。");
                int width=BigEndian(header,16),height=BigEndian(header,20);
                SpriteAtlas.ValidateDimensions(width,height);
            }
        }
        private static int BigEndian(byte[] b,int i) { return (b[i]<<24)|(b[i+1]<<16)|(b[i+2]<<8)|b[i+3]; }
        public static BitmapSource GetAvatar(SkinDefinition skin)
        {
            // Imported designs keep their own artwork, even when their ID matches a preset.
            if(!skin.Embedded && !skin.Bundled)return GetPreview(skin);
            lock(avatars)
            {
                BitmapSource cached;if(avatars.TryGetValue(skin.Id,out cached))return cached;
                using(var stream=Assembly.GetExecutingAssembly().GetManifestResourceStream("Dafeiyu.Avatar."+skin.Id))
                {
                    if(stream==null)return GetPreview(skin);
                    var image=new BitmapImage();image.BeginInit();image.CacheOption=BitmapCacheOption.OnLoad;image.DecodePixelWidth=256;image.StreamSource=stream;image.EndInit();image.Freeze();avatars.Add(skin.Id,image);return image;
                }
            }
        }
        public static DataTemplate AvatarTemplate()
        {
            var row=new FrameworkElementFactory(typeof(System.Windows.Controls.StackPanel));row.SetValue(System.Windows.Controls.StackPanel.OrientationProperty,System.Windows.Controls.Orientation.Horizontal);
            var image=new FrameworkElementFactory(typeof(System.Windows.Controls.Image));image.SetValue(FrameworkElement.WidthProperty,28.0);image.SetValue(FrameworkElement.HeightProperty,28.0);image.SetValue(UIElement.ClipProperty,new System.Windows.Media.RectangleGeometry(new Rect(0,0,28,28),5.6,5.6));image.SetValue(FrameworkElement.MarginProperty,new Thickness(0,0,9,0));image.SetBinding(System.Windows.Controls.Image.SourceProperty,new System.Windows.Data.Binding{Converter=new AvatarConverter()});row.AppendChild(image);
            var name=new FrameworkElementFactory(typeof(System.Windows.Controls.TextBlock));name.SetBinding(System.Windows.Controls.TextBlock.TextProperty,new System.Windows.Data.Binding("Name"));name.SetValue(FrameworkElement.VerticalAlignmentProperty,VerticalAlignment.Center);row.AppendChild(name);return new DataTemplate{VisualTree=row};
        }
        sealed class AvatarConverter:System.Windows.Data.IValueConverter
        {
            public object Convert(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture){return value is SkinDefinition?GetAvatar((SkinDefinition)value):null;}
            public object ConvertBack(object value,Type targetType,object parameter,System.Globalization.CultureInfo culture){throw new NotSupportedException();}
        }
        public static BitmapSource GetPreview(SkinDefinition skin)
        {
            if(skin.Illustrated) return IllustratedPack.ReadImage(IllustratedPack.AssetPath(skin.Source,skin.Manifest.Preview),480,520);
            using(Stream stream=skin.Embedded ? Assembly.GetExecutingAssembly().GetManifestResourceStream(skin.Preview) : File.OpenRead(skin.Source))
            {
                if(stream==null) throw new InvalidOperationException("找不到外观预览。");
                var image=new BitmapImage(); image.BeginInit(); image.CacheOption=BitmapCacheOption.OnLoad;
                image.StreamSource=stream; image.DecodePixelWidth=skin.Embedded ? 480 : 3840; image.EndInit(); image.Freeze();
                if(skin.Embedded) return image;
                var crop=new CroppedBitmap(image,new Int32Rect(0,0,image.PixelWidth/8,image.PixelHeight/11)); crop.Freeze(); return crop;
            }
        }
    }
}
