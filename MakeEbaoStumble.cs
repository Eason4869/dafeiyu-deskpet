using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;

// Authoring a code-driven sway from the accepted original poses preserves the
// character's proportions and hair colors. No regenerated outline or blending.
public static class MakeEbaoStumble
{
    public static int Main(string[] args)
    {
        if(args.Length!=1)throw new ArgumentException("Supply repository root.");
        string root=Path.GetFullPath(args[0]),path=Path.Combine(root,"assets/additional/e-bao.png");
        using(var original=new Bitmap(Path.Combine(root,"assets/e-bao-interactions.png")))
        using(var idle=new Bitmap(Path.Combine(root,"assets/e-bao-spritesheet.png")))
        using(var existing=new Bitmap(path))using(var result=new Bitmap(1920,2080,PixelFormat.Format32bppArgb))
        {
            using(var atlas=Graphics.FromImage(result))
            {
                atlas.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                atlas.DrawImage(existing,new Rectangle(0,1040,1920,1040),new Rectangle(0,1040,1920,1040),GraphicsUnit.Pixel);
                double[] angles={0,-1.5,-3.5,-2,1.5,2.5,.8,0};
                for(int i=0;i<8;i++)
                {
                    var source=i==7?idle:original;var region=i==7?new Rectangle(0,0,480,520):new Rectangle(Math.Min(i,3)*480,520,480,520);
                    using(var frame=source.Clone(region,PixelFormat.Format32bppArgb))using(var pose=new Bitmap(480,520,PixelFormat.Format32bppArgb))
                    {
                        using(var g=Graphics.FromImage(pose))
                        {
                            g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                            g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                            g.TranslateTransform(240,500);g.RotateTransform((float)angles[i]);g.TranslateTransform(-240,-500);
                            g.DrawImageUnscaled(frame,0,0);
                        }
                        int bottom=0;for(int y=0;y<520;y++)for(int x=0;x<480;x++)if(pose.GetPixel(x,y).A>32)bottom=Math.Max(bottom,y);
                        atlas.DrawImageUnscaled(pose,i%4*480,i/4*520+504-bottom);
                    }
                }
            }
            string temp=path+".tmp";result.Save(temp,ImageFormat.Png);
            existing.Dispose();File.Copy(temp,path,true);File.Delete(temp);
        }
        Console.WriteLine("Ebao stumble: eight registered original-art poses, fixed scale, no generated hair edges.");return 0;
    }
}
