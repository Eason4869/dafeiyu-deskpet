using System;

namespace Dafeiyu
{
    public sealed class IdleSleepSchedule
    {
        public const double SystemIdleSeconds=300,PetIdleSeconds=900,RetrySeconds=60,Probability=.7;
        readonly Func<double> random;
        double lastInteraction,nextAttempt;
        public IdleSleepSchedule(double now,Func<double> sample){random=sample;Touch(now);}
        public void Touch(double now){lastInteraction=now;nextAttempt=now+RetrySeconds;}
        public bool TrySleep(double now,double systemIdle,bool allowed,bool enabled)
        {
            if(!enabled || !allowed || Double.IsNaN(now) || Double.IsInfinity(now) || now<nextAttempt)return false;
            double elapsed=now-lastInteraction;
            bool away=!Double.IsNaN(systemIdle) && !Double.IsInfinity(systemIdle) && Math.Min(elapsed,systemIdle)>=SystemIdleSeconds;
            if(!away && elapsed<PetIdleSeconds)return false;
            nextAttempt=now+RetrySeconds;double roll=random();return roll>=0 && roll<Probability;
        }
    }
}
