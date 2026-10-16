using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;

// Authoring utility: extract generated cells, remove disconnected alpha dust,
// register feet and use one scale for the entire character, never blend frames.
public static class PackAdditionalActions
{
    static int[] Cuts(Bitmap source,bool horizontal)
    {
        int length=horizontal?source.Height:source.Width,other=horizontal?source.Width:source.Height;
        var occupancy=new int[length];
        for(int p=0;p<length;p++)for(int q=0;q<other;q++)if((horizontal?source.GetPixel(q,p):source.GetPixel(p,q)).A>=128)occupancy[p]++;
        var cuts=new[]{0,0,0,0,length};
        for(int i=1;i<4;i++)
        {
            int expected=length*i/4,best=expected,score=Int32.MaxValue;
            for(int p=Math.Max(2,expected-length/14);p<Math.Min(length-2,expected+length/14);p++)
            {int value=(occupancy[p-1]+occupancy[p]+occupancy[p+1])*1000+Math.Abs(p-expected);if(value<score){score=value;best=p;}}
            cuts[i]=best;
        }
        return cuts;
    }
    static Rectangle Bounds(Bitmap image)
    {
        int left=image.Width,top=image.Height,right=-1,bottom=-1;
        for(int y=0;y<image.Height;y++)for(int x=0;x<image.Width;x++)if(image.GetPixel(x,y).A>=32){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
        if(right<left)throw new InvalidDataException("Empty sprite.");
        return Rectangle.FromLTRB(left,top,right+1,bottom+1);
    }
    static void Clean(Bitmap image)
    {
        int w=image.Width,h=image.Height;var seen=new bool[w*h];var groups=new List<List<int>>();
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)
        {
            int start=y*w+x;if(seen[start] || image.GetPixel(x,y).A<32)continue;
            var group=new List<int>();var pending=new Queue<int>();pending.Enqueue(start);seen[start]=true;
            while(pending.Count>0)
            {
                int p=pending.Dequeue();group.Add(p);int px=p%w,py=p/w;
                for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
                {int nx=px+dx,ny=py+dy;if(nx<0 || ny<0 || nx>=w || ny>=h)continue;int next=ny*w+nx;if(!seen[next] && image.GetPixel(nx,ny).A>=32){seen[next]=true;pending.Enqueue(next);}}
            }
            groups.Add(group);
        }
        int largest=groups.Max(g=>g.Count);var keep=new bool[w*h];
        foreach(var group in groups)if(group.Count>=Math.Max(40,largest/500))foreach(int p in group)
            for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
            {int x=p%w+dx,y=p/w+dy;if(x>=0 && y>=0 && x<w && y<h)keep[y*w+x]=true;}
        for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(!keep[y*w+x])image.SetPixel(x,y,Color.Transparent);
    }
    public static int Main(string[] args)
    {
        if(args.Length!=3)throw new ArgumentException("source-sheet reference-preview output-sheet");
        using(var source=new Bitmap(args[0]))using(var reference=new Bitmap(args[1]))using(var atlas=new Bitmap(1920,2080,PixelFormat.Format32bppArgb))
        {
            var frames=new List<Bitmap>();var bounds=new List<Rectangle>();
            try
            {
                var rowCuts=Cuts(source,true);
                for(int row=0;row<4;row++)using(var strip=source.Clone(new Rectangle(0,rowCuts[row],source.Width,rowCuts[row+1]-rowCuts[row]),PixelFormat.Format32bppArgb))
                {
                    var columnCuts=Cuts(strip,false);
                    for(int col=0;col<4;col++){var cell=strip.Clone(new Rectangle(columnCuts[col],0,columnCuts[col+1]-columnCuts[col],strip.Height),PixelFormat.Format32bppArgb);Clean(cell);frames.Add(cell);bounds.Add(Bounds(cell));}
                }
                var target=Bounds(reference);double neutral=(bounds[0].Height+bounds[7].Height+bounds[8].Height+bounds[15].Height)/4.0;
                double scale=target.Height/neutral;
                scale=Math.Min(scale,448.0/bounds.Max(b=>b.Width));scale=Math.Min(scale,480.0/bounds.Max(b=>b.Height));
                using(var g=Graphics.FromImage(atlas))
                {
                    g.CompositingMode=System.Drawing.Drawing2D.CompositingMode.SourceCopy;
                    g.InterpolationMode=System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                    g.PixelOffsetMode=System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                    for(int i=0;i<16;i++)
                    {
                        var b=bounds[i];int w=(int)Math.Round(b.Width*scale),h=(int)Math.Round(b.Height*scale);
                        // Register cell center and grounded foot line; no per-frame scaling.
                        int x=i%4*480+(480-w)/2,y=i/4*520+Math.Min(505,target.Bottom)-h;
                        g.DrawImage(frames[i],new Rectangle(x,y,w,h),b,GraphicsUnit.Pixel);
                        Console.WriteLine(i+": source="+b+" rendered="+w+"x"+h+" baseline="+(y%520+h));
                    }
                }
                Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2])));atlas.Save(args[2],ImageFormat.Png);
                Console.WriteLine("Packed 16 original cells with uniform scale "+scale.ToString("F4")+" -> "+args[2]);
            }
            finally{foreach(var frame in frames)frame.Dispose();}
        }
        return 0;
    }
}
