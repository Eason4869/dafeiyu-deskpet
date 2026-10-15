using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
namespace Dafeiyu.Tests
{
    public static class FeedbackSmoke
    {
        [STAThread] public static int Main(){try{
            string dir=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"feedback-qa");Directory.CreateDirectory(dir);
            var wave=new DictationWaveform();for(int i=0;i<60;i++)wave.Push(.015);
            var pixels=Capture(wave,240,34,Path.Combine(dir,"dictation-soft.png"));
            int top=34,bottom=0;for(int y=0;y<34;y++)for(int x=0;x<240;x++)if(pixels[(y*240+x)*4+3]>100){top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
            Check(bottom-top>=16,"quiet dictation reaches at least half the available waveform height");
            var orbit=new VoiceOrbit();for(int i=0;i<20;i++)orbit.Level=.02;
            pixels=Capture(orbit,200,200,Path.Combine(dir,"live-soft.png"));
            double radius=0;for(int y=0;y<200;y++)for(int x=0;x<200;x++){int p=(y*200+x)*4;if(pixels[p+3]>100 && pixels[p]>pixels[p+2]+25)radius=Math.Max(radius,Math.Sqrt((x-100)*(x-100)+(y-100)*(y-100)));}
            Check(radius>=90,"ordinary soft speech makes the live orbit expand visibly");
            for(int i=0;i<70;i++)orbit.Level=0;Check(orbit.Level<.0001,"live orbit decays to silence");Capture(orbit,200,200,Path.Combine(dir,"live-silent.png"));
            Console.WriteLine("Feedback smoke passed");return 0;
        }catch(Exception e){Console.WriteLine(e);return 1;}}
        static byte[] Capture(FrameworkElement e,int w,int h,string file){e.Measure(new Size(w,h));e.Arrange(new Rect(0,0,w,h));e.UpdateLayout();var b=new RenderTargetBitmap(w,h,96,96,PixelFormats.Pbgra32);b.Render(e);var p=new byte[w*h*4];b.CopyPixels(p,w*4,0);var png=new PngBitmapEncoder();png.Frames.Add(BitmapFrame.Create(b));using(var s=File.Create(file))png.Save(s);return p;}
        static void Check(bool ok,string message){if(!ok)throw new Exception(message);Console.WriteLine("PASS "+message);}
    }
}
