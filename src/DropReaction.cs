using System;

namespace Dafeiyu
{
    // Only a completed native drag may roll. Cooldown survives interruptions.
    public sealed class DropReaction
    {
        public const double Probability=.4, CooldownSeconds=10;
        readonly Func<double> sample;
        double nextAllowed=Double.NegativeInfinity;
        public DropReaction(Func<double> random){sample=random;}
        public bool TryStumble(bool moved,bool allowed,bool hasArtwork,double now)
        {
            if(!moved || !allowed || !hasArtwork || Double.IsNaN(now) || Double.IsInfinity(now) || now<nextAllowed)return false;
            double roll=sample();
            if(Double.IsNaN(roll) || roll<0 || roll>=Probability)return false;
            nextAllowed=now+CooldownSeconds;return true;
        }
    }
}
