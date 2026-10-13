using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Dafeiyu
{
    public sealed class SpriteAtlas
    {
        private readonly BitmapSource[][] frames = new BitmapSource[11][];
        private readonly byte[][][] alpha = new byte[11][][];
        public int CellWidth { get; private set; }
        public int CellHeight { get; private set; }
        public bool HasGaze { get; private set; }
        public int FrameMultiplier { get; private set; }
        public SpriteAtlas() : this("Dafeiyu.Atlas", true) { }
        public SpriteAtlas(string source, bool embedded, bool motion=false)
        {
            if(motion && !embedded) throw new ArgumentException("Motion atlases must be embedded resources.");
            if (!embedded) SkinCatalog.ValidatePng(source);
            BitmapSource image;
            using (Stream stream = embedded ? Assembly.GetExecutingAssembly().GetManifestResourceStream(source) : File.OpenRead(source))
            {
                if (stream == null) throw new InvalidOperationException("找不到内嵌桌宠素材。");
                image = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            }
            if(motion) {
                if(image.PixelWidth!=12288 || image.PixelHeight!=2288) throw new InvalidOperationException("内嵌动画图尺寸不正确。");
            } else ValidateDimensions(image.PixelWidth, image.PixelHeight);
            FrameMultiplier=motion ? 8 : 1;
            CellWidth = image.PixelWidth / (8*FrameMultiplier); CellHeight = image.PixelHeight / 11; HasGaze = true;
            image.Freeze();
            for (int row = 0; row < 11; row++)
            {
                int count = (row < 9 ? AnimationPlayer.Counts[row] : 8)*FrameMultiplier;
                frames[row] = new BitmapSource[count]; alpha[row] = new byte[count][];
                for (int col = 0; col < count; col++)
                {
                    var cropped = new CroppedBitmap(image, new Int32Rect(col * CellWidth, row * CellHeight, CellWidth, CellHeight));
                    cropped.Freeze();
                    var bgra = new FormatConvertedBitmap(cropped, PixelFormats.Bgra32, null, 0); bgra.Freeze();
                    byte[] pixels = new byte[CellWidth * CellHeight * 4]; bgra.CopyPixels(pixels, CellWidth * 4, 0);
                    // Detach each small frame so WPF does not retain/upload the
                    // full 12k-wide atlas for every animated Image.Source.
                    frames[row][col]=BitmapSource.Create(CellWidth,CellHeight,96,96,PixelFormats.Bgra32,null,pixels,CellWidth*4);
                    frames[row][col].Freeze();
                    alpha[row][col] = new byte[CellWidth * CellHeight];
                    for (int i = 0; i < alpha[row][col].Length; i++) alpha[row][col][i] = pixels[i * 4 + 3];
                    bool visible=false,transparent=false;
                    for(int i=0;i<alpha[row][col].Length;i++) { if(alpha[row][col][i]>16) visible=true; if(alpha[row][col][i]==0) transparent=true; }
                    if(!visible || !transparent) throw new InvalidOperationException("每个动作帧应有人物和透明背景。");
                }
            }
        }
        public static void ValidateDimensions(int width, int height)
        {
            if (width % 8 != 0 || height % 11 != 0 || width / 8 < 64 || height / 11 < 64 || width / 8 > 768 || height / 11 > 832 || (long)width*height > 55000000)
                throw new InvalidOperationException("外观需为 8 列 × 11 行透明 PNG 精灵图，单帧最大 768 × 832。");
        }
        public BitmapSource GetFrame(int row, int col) { return frames[row][col]; }
        public byte AlphaAt(int row, int col, int x, int y)
        { return x < 0 || y < 0 || x >= CellWidth || y >= CellHeight ? (byte)0 : alpha[row][col][y * CellWidth + x]; }
    }
}
