using System;

namespace Dafeiyu
{
    // One physical trajectory for both artwork versions and both drop outcomes.
    public sealed class DropMotion
    {
        public const double Seconds=.3, LogicalDistance=24, LogicalHeight=208;
        double start,target,elapsed;
        public bool Active {get;private set;}
        public double Position {get;private set;}
        public void Start(double y,double height,double screenBottom)
        {
            Active=false;elapsed=0;Position=y;
            if(!Finite(y) || !Finite(height) || !Finite(screenBottom) || height<=0)return;
            start=y;target=Math.Max(y,Math.Min(y+height*LogicalDistance/LogicalHeight,screenBottom-height));
            Active=target>start;
        }
        public void Advance(double seconds)
        {
            if(!Active || !Finite(seconds) || seconds<=0)return;
            elapsed=Math.Min(Seconds,elapsed+seconds);
            double t=elapsed/Seconds;Position=start+(target-start)*t*t;
            if(elapsed>=Seconds-1e-9){Position=target;Active=false;}
        }
        public void Cancel(){Active=false;}
        static bool Finite(double value){return !Double.IsNaN(value) && !Double.IsInfinity(value);}
    }
}
