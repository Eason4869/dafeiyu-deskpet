using System;

namespace Dafeiyu
{
    public static class WanderSchedule
    {
        public const double MinimumSeconds=90, MaximumSeconds=180;
        public static double Next(double now,double sample)
        {return now+MinimumSeconds+(MaximumSeconds-MinimumSeconds)*Math.Max(0,Math.Min(1,Double.IsNaN(sample)?.5:sample));}
    }
}
