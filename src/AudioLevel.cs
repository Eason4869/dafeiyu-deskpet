using System;
namespace Dafeiyu
{
    public interface IAudioLevelSource { double AudioLevel { get; } }
    public static class AudioLevel
    {
        public static double Pcm16(byte[] bytes)
        {
            if(bytes==null || bytes.Length<2)return 0;
            double sum=0;int count=bytes.Length/2;
            for(int i=0;i<count;i++){double value=(short)(bytes[i*2]|bytes[i*2+1]<<8)/32768.0;sum+=value*value;}
            return Math.Min(1,Math.Sqrt(sum/count)*3);
        }
    }
}
