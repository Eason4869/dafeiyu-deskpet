using System;
namespace Dafeiyu
{
    internal static class VoiceVisual
    {
        // Perceptual display gain only. Audio recording/playback is untouched.
        public static double Response(double level)
        {
            if(Double.IsNaN(level) || Double.IsInfinity(level) || level<=.0015)return 0;
            return Math.Min(1,Math.Pow((Math.Min(1,level)-.0015)*12,.38));
        }
    }
}
