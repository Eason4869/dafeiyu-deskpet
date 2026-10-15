using System;

namespace Dafeiyu
{
    public sealed class GreetingSchedule
    {
        readonly int minimum,maximum;
        readonly Func<double> sample;
        DateTime previous;
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
            // A suspended PC or a hidden/paused timer never accumulates greetings.
            if(now<previous || now-previous>TimeSpan.FromMinutes(2)){Reset(now);return false;}
            previous=now;if(now<NextAt)return false;
            // Short walks, clicks and open panels defer one due greeting; they
            // must not restart a 30-minute wait on every return to idle.
            if(!allowed){NextAt=now.AddSeconds(5);return false;}
            Reset(now);return true;
        }
        public void Defer(DateTime now){previous=now;NextAt=now.AddSeconds(5);}
    }

    public static class GreetingPhrases
    {
        public const int Count=12;
        public static string Choose(string skinId,string name,DateTime localTime,int index)
        {
            string greeting=localTime.Hour<6?"夜深了":localTime.Hour<11?"早上好":localTime.Hour<14?"中午好":localTime.Hour<18?"下午好":"晚上好";
            string[] lines;
            if(skinId=="e-bao")lines=new[]{greeting+"。本少爷路过打个招呼，才不是特地来看你的。","喂，忙了一阵了吧？喝口水。别让我再提醒你。","今天也在努力呢。哼，勉强夸你一下。","本少爷在这里。有想法就来聊两句，别自己闷着。","想歇一会儿就歇。本少爷又没催你。","有难题可以拿来商量，别一个人跟自己较劲。","偶尔发会儿呆也行。反正本少爷今天有空。","今天的小进展也算数，可别一转头就忘了。","喂，肩膀放松点。只是顺口提醒你而已。","要是想说点什么，本少爷勉为其难听听。","不急，按你的节奏来。本少爷就在旁边。","给你留了个聊天的位置，来不来随你。"};
            else if(skinId=="dafeiyu")lines=new[]{greeting+"呀！大肥鱼游过来，跟你打个招呼。","忙了一会儿啦，喝点水、伸个懒腰吧。","送你一个小小的问候，愿今天顺顺利利。","我在这里陪着你，有想法随时来聊呀。","今天的小小进展，也值得被好好记住呢。","可以慢慢来，我陪你把事情一件件做好。","如果脑袋有点挤，就让思绪游一小会儿吧。","想聊正事还是闲话？我都愿意听呀。","送你一朵看不见的小浪花，祝你心情轻一点。","不想说话也没关系，安静待着也很好。","记得给自己一点空隙，好好呼吸一下。","我绕了一小圈，又游回你身边啦。"};
            else if(skinId=="marfu")lines=new[]{greeting+"！马尔芙带着一点小魔法来报到。","给今天加一颗幸运星，希望你顺顺利利。","灵感暂时躲起来了吗？我们可以慢慢找。","我的魔法笔记还有空位，要不要聊个新点子？","休息也是恢复魔力的一部分，别忘了呀。","今天的一小步，也能点亮一小颗星星。","不急着把所有事做完，先照顾眼前这一件。","如果遇到打结的思路，我们一起解开它。","悄悄送来一份好心情，希望没有打断你。","想说的话可以放在这里，我会认真听。","魔法暂停一下，肩膀也放松一下吧。","我就在旁边守着这点小小的光。"};
            else lines=new[]{greeting+"！我是"+(name??"你的伙伴")+"，来和你打个招呼。","工作顺利吗？记得偶尔喝水、休息一下。","送你一个小小的问候，愿今天顺顺利利。","我在这里，有想法随时来聊聊。","按你的节奏来，一点点进展也很好。","想歇一会儿的话，我陪你安静待着。","有什么新点子吗？可以随时说给我听。","别忘了放松肩膀，给自己一点空隙。","不管今天快一点还是慢一点，都照顾好自己。","如果事情有点复杂，我们可以一件件梳理。","我来打声招呼，你继续忙也没关系。","有想分享的小事，我都愿意听。"};
            return lines[(int)((uint)index%(uint)lines.Length)];
        }
    }
}
