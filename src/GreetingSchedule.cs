using System;

namespace Dafeiyu
{
    public sealed class GreetingSchedule
    {
        readonly int minimum,maximum;
        readonly Func<double> sample;
        DateTime previous;
        bool previouslyAllowed=true;
        public DateTime NextAt {get;private set;}
        public GreetingSchedule(DateTime now,int minMinutes,int maxMinutes,Func<double> randomSample)
        {
            minimum=Math.Max(1,Math.Min(1440,minMinutes));
            maximum=Math.Max(minimum,Math.Min(1440,maxMinutes));
            sample=randomSample ?? new Random().NextDouble;
            Reset(now);
        }
        public void Reset(DateTime now)
        {
            double value=sample();if(Double.IsNaN(value) || Double.IsInfinity(value))value=.5;
            NextAt=now.AddMinutes(minimum+(maximum-minimum)*Math.Max(0,Math.Min(1,value)));
            previous=now;
        }
        public bool TryTake(DateTime now,bool allowed)
        {
            bool resumed=allowed && !previouslyAllowed;previouslyAllowed=allowed;
            if(resumed){Reset(now);return false;}
            // A suspended PC or a hidden/paused timer never accumulates greetings.
            if(now<previous || now-previous>TimeSpan.FromMinutes(2)){Reset(now);return false;}
            previous=now;if(now<NextAt)return false;
            Reset(now);return allowed;
        }
    }

    public static class GreetingPhrases
    {
        public static string Choose(string skinId,string name,DateTime localTime,int index)
        {
            string greeting=localTime.Hour<6?"夜深了":localTime.Hour<11?"早上好":localTime.Hour<14?"中午好":localTime.Hour<18?"下午好":"晚上好";
            string[] lines;
            if(skinId=="e-bao")lines=new[]{greeting+"。本少爷路过打个招呼，才不是特地来看你的。","喂，忙了一阵了吧？喝口水。别让我再提醒你。","今天也在努力呢。哼，勉强夸你一下。","本少爷在这里。有想法就来聊两句，别自己闷着。"};
            else if(skinId=="dafeiyu")lines=new[]{greeting+"呀！大肥鱼游过来，跟你打个招呼。","忙了一会儿啦，喝点水、伸个懒腰吧。","送你一个小小的问候，愿今天顺顺利利。","我在这里陪着你，有想法随时来聊呀。"};
            else lines=new[]{greeting+"！我是"+(name??"你的伙伴")+"，来和你打个招呼。","工作顺利吗？记得偶尔喝水、休息一下。","送你一个小小的问候，愿今天顺顺利利。","我在这里，有想法随时来聊聊。"};
            return lines[(int)((uint)index%(uint)lines.Length)];
        }
    }
}
