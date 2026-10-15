using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dafeiyu.Tests
{
    public static class ImportedSkinTests
    {
        static int passed;
        static void Check(bool value,string name){if(!value)throw new Exception(name);Console.WriteLine("PASS "+name);passed++;}
        static void Reject(SkinCatalog catalog,string path,string name){int before=catalog.Skins.Count;try{catalog.Import(path);}catch(Exception e){if(!(e is InvalidOperationException || e is IOException || e is System.Runtime.Serialization.SerializationException))throw;Check(before==catalog.Skins.Count,name);return;}throw new Exception("Accepted invalid pack: "+name);}
        static void Write(string path,SkinManifest manifest){using(var stream=File.Create(path))SkinCatalog.Serializer().WriteObject(stream,manifest);}
        static void Image(string path,int columns,int count)
        {
            int width=columns*480,height=((count+columns-1)/columns)*520;byte[] pixels=new byte[width*height*4];
            for(int i=0;i<count;i++)for(int y=100;y<420;y++)for(int x=100;x<380;x++) {
                int p=((i/columns*520+y)*width+i%columns*480+x)*4;
                pixels[p]=(byte)(10+i*9);pixels[p+1]=(byte)(230-i*7);pixels[p+2]=(byte)(30+i*11);pixels[p+3]=255;
            }
            var image=BitmapSource.Create(width,height,96,96,PixelFormats.Bgra32,null,pixels,width*4);
            var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(image));using(var output=File.Create(path))encoder.Save(output);
        }
        static byte[] Pixels(BitmapSource image){var bgra=new FormatConvertedBitmap(image,PixelFormats.Bgra32,null,0);var result=new byte[image.PixelWidth*image.PixelHeight*4];bgra.CopyPixels(result,image.PixelWidth*4,0);return result;}
        [STAThread] public static int Main(string[] args)
        {
            string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"imported-tests-"+Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(root);
            try {
                string source=Path.Combine(root,"source"),data=Path.Combine(root,"data");Directory.CreateDirectory(source);Directory.CreateDirectory(Path.Combine(source,"animations"));
                Image(Path.Combine(source,"animations","action.png"),8,8);Image(Path.Combine(source,"gaze.png"),4,16);Image(Path.Combine(source,"preview.png"),1,1);
                var manifest=new SkinManifest{Version=2,Name="小贝",Format="illustrated-atlases-v1",CellWidth=480,CellHeight=520,Preview="preview.png",SystemPrompt="你是小贝，男性桌面伙伴。",Description="温柔的伙伴",SpecialAction="太极光环",Animations=new System.Collections.Generic.Dictionary<string,IllustratedAnimation>(),Gaze=new IllustratedGaze{File="gaze.png",Columns=4,Frames=16,Interpolation="none",Order="clockwise-from-up"}};
                foreach(string name in IllustratedPack.Actions)manifest.Animations[name]=new IllustratedAnimation{File="animations/action.png",Columns=8,Frames=8,Fps=24,Loop=name=="idle" || name=="lifted" || name=="sleeping"};
                manifest.Animations["idle"].Fps=5;manifest.Animations["jumping"].Fps=4;manifest.Animations["feeding"].Fps=8;manifest.Animations["happy"].Fps=2;
                string json=Path.Combine(source,"pet.json");Write(json,manifest);var catalog=new SkinCatalog(data);var skin=catalog.Import(json);
                Check(skin.Illustrated && skin.Name=="小贝" && skin.SpecialAction=="太极光环" && skin.SystemPrompt==manifest.SystemPrompt,"v2 imports persona and named special action");
                foreach(var builtin in catalog.Skins.Where(s=>s.Embedded)) {
                    var exactAtlas=builtin.Load();BitmapSource originalAtlas;
                    using(var input=typeof(SkinCatalog).Assembly.GetManifestResourceStream(builtin.Source))originalAtlas=BitmapDecoder.Create(input,BitmapCreateOptions.PreservePixelFormat,BitmapCacheOption.OnLoad).Frames[0];
                    for(int i=0;i<16;i++)Check(Pixels(exactAtlas.GetFrame(9+i/8,i%8)).SequenceEqual(Pixels(new CroppedBitmap(originalAtlas,new Int32Rect(i%8*480,(9+i/8)*520,480,520)))),builtin.Id+" stationary gaze retains original "+i);
                }
                var reloaded=new SkinCatalog(data).Find(skin.Id);Check(reloaded.Illustrated && reloaded.Manifest.Animations["idle"].Fps==5,"v2 dictionary and timing survive reload");
                var motion=new SmoothMotion(reloaded);var idle=motion.Get(0);Check(idle.Count==8 && idle.Duration==1.6 && idle.Width==480 && idle.Height==520,"v2 clips retain source dimensions and manifest timing");
                Check(idle.Sample(.199)==0 && idle.Sample(.2)==1 && idle.Sample(1.6)==0,"real frames advance at manifest FPS and wrap without blending");
                for(int a=0;a<IllustratedPack.Actions.Length;a++)Check(motion.Get(a<9?a:a+2).Count==8,"all v2 action slots load: "+IllustratedPack.Actions[a]);
                var jump=motion.Get(4);Check(!jump.Loop && jump.Duration==2 && jump.Sample(9)==7,"one shot clamps to final source frame");
                var player=skin.CreateAnimation();player.Play("jumping",.1);player.Advance(1.99);Check(player.State=="jumping","manifest one shot overrides legacy request duration");player.Advance(.02);Check(player.State=="idle","manifest one shot ends at its actual duration");
                var interaction=new InteractionAnimation();interaction.Configure(skin);interaction.Start("feeding");interaction.Advance(.99);Check(interaction.Name=="feeding","feeding uses imported duration");interaction.Advance(.02);Check(interaction.Name=="happy" && interaction.Elapsed<.011,"feeding transitions with correct remainder");interaction.Advance(3.98);Check(interaction.Name=="happy","happy has independent manifest duration");interaction.Advance(.02);Check(!interaction.Active,"interaction finishes by manifest duration");
                var atlas=skin.Load();var original=IllustratedPack.ReadImage(Path.Combine(source,"gaze.png"),1920,2080);
                for(int i=0;i<16;i++)Check(Pixels(atlas.GetFrame(9+i/8,i%8)).SequenceEqual(Pixels(new CroppedBitmap(original,new Int32Rect(i%4*480,i/4*520,480,520)))),"gaze is exact original illustration "+i);
                Check(atlas.AlphaAt(9,0,100,100)==255 && atlas.AlphaAt(9,0,0,0)==0 && idle.AlphaAt(0,100,100)==255 && idle.AlphaAt(0,479,519)==0,"gaze and action hit masks preserve original alpha");
                Check(SkinCatalog.GetPreview(skin).PixelWidth==480,"preview loaded from copied asset");
                int folders=Directory.GetDirectories(Path.Combine(data,"skins")).Length;
                foreach(string bad in new[]{"../preview.png","/preview.png","C:/preview.png","https://example.com/x.png","animations/../preview.png","animations\\action.png"}) {
                    manifest.Animations["idle"].File=bad;Write(json,manifest);Reject(catalog,json,"reject unsafe path "+bad);
                }
                manifest.Animations["idle"].File="animations/action.png";
                manifest.Animations["idle"].Frames=129;Write(json,manifest);Reject(catalog,json,"reject excessive frames before decoding");manifest.Animations["idle"].Frames=8;
                manifest.Animations["idle"].Fps=0;Write(json,manifest);Reject(catalog,json,"reject invalid timing");manifest.Animations["idle"].Fps=5;
                manifest.Animations["idle"].Loop=false;Write(json,manifest);Reject(catalog,json,"reject nonlooping idle baseline");manifest.Animations["idle"].Loop=true;
                manifest.Gaze.Interpolation="blend";Write(json,manifest);Reject(catalog,json,"reject interpolated gaze");manifest.Gaze.Interpolation="none";
                manifest.Animations["idle"].File="preview.png";Write(json,manifest);Reject(catalog,json,"reject atlas dimensions before decode");manifest.Animations["idle"].File="animations/action.png";

                var blank=new WriteableBitmap(3840,520,96,96,PixelFormats.Bgra32,null);var encoder=new PngBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(blank));using(var output=File.Create(Path.Combine(source,"blank.png")))encoder.Save(output);
                manifest.Animations["idle"].File="blank.png";Write(json,manifest);Reject(catalog,json,"reject empty actual frame");manifest.Animations["idle"].File="animations/action.png";
                string oversized=Path.Combine(source,"oversized.png");File.Copy(Path.Combine(source,"animations","action.png"),oversized);
                using(var output=File.OpenWrite(oversized))output.SetLength(25L*1024*1024+1);
                manifest.Animations["idle"].File="oversized.png";Write(json,manifest);Reject(catalog,json,"reject oversized PNG before decode");
                string damaged=Path.Combine(source,"damaged.png");File.Copy(Path.Combine(source,"animations","action.png"),damaged);
                using(var output=File.OpenWrite(damaged))output.SetLength(33);
                manifest.Animations["idle"].File="damaged.png";Write(json,manifest);Reject(catalog,json,"reject corrupt PNG with valid dimensions");manifest.Animations["idle"].File="animations/action.png";
                Check(Directory.GetDirectories(Path.Combine(data,"skins")).Length==folders,"failed imports leave no destination folders");
                File.Move(Path.Combine(source,"animations","action.png"),Path.Combine(source,"animations","source-preserved.png"));
                Check(new SmoothMotion(new SkinCatalog(data).Find(skin.Id)).Get(0).Count==8,"import owns copied assets independently of source");
                Check(File.Exists(Path.Combine(source,"animations","source-preserved.png")),"source files are preserved");
                string oldAtlas=Path.Combine(source,"legacy.png");using(var input=typeof(SkinCatalog).Assembly.GetManifestResourceStream("Dafeiyu.Atlas"))using(var output=File.Create(oldAtlas))input.CopyTo(output);
                var old=new SkinManifest{Version=1,Name="旧格式",Atlas="legacy.png"};Write(json,old);var oldSkin=catalog.Import(json);Check(!oldSkin.Illustrated && oldSkin.Load().CellWidth==480,"v1 JSON remains compatible");
                Check(!catalog.Import(oldAtlas).Illustrated,"standalone v1 PNG remains compatible");
                string copiedAction=Path.Combine(skin.Source,"animations","action.png");using(var damagedCopy=File.OpenWrite(copiedAction))damagedCopy.SetLength(33);
                Check(new SkinCatalog(data).Find(skin.Id).Id!=skin.Id,"catalog rejects damaged installed action before runtime");
                if(args.Length>0) {
                    var actual=catalog.Import(args[0]);Check(actual.Illustrated,"supplied real pack imports");
                    var actualMotion=new SmoothMotion(actual);
                    for(int a=0;a<IllustratedPack.Actions.Length;a++) {
                        var spec=actual.Manifest.Animations[IllustratedPack.Actions[a]];var clip=actualMotion.Get(a<9?a:a+2);
                        Check(clip.Count==spec.Frames && Math.Abs(clip.Duration-spec.Frames/spec.Fps)<1e-9,"real pack action "+IllustratedPack.Actions[a]);
                    }
                    Check(actual.Load().HasGaze,"real pack gaze loads");
                }
                Console.WriteLine("PASS "+passed+" imported skin checks");return 0;
            } catch(Exception e){Console.Error.WriteLine(e);return 1;}
            finally {Directory.Delete(root,true);}
        }
    }
}
