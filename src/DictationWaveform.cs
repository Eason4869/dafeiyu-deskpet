using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;
namespace Dafeiyu
{
    internal sealed class DictationWaveform : FrameworkElement
    {
        readonly Queue<double> samples=new Queue<double>();
        public DictationWaveform(){Height=34;MinWidth=20;IsHitTestVisible=false;}
        public void Push(double level){samples.Enqueue(Math.Max(0,Math.Min(1,level)));while(samples.Count>100)samples.Dequeue();InvalidateVisual();}
        public void Clear(){samples.Clear();InvalidateVisual();}
        protected override void OnRender(DrawingContext drawing)
        {
            var values=samples.ToArray();int count=Math.Max(1,(int)(ActualWidth/6));
            for(int i=0;i<count;i++)
            {
                int index=values.Length-count+i;double value=index>=0?values[index]:0;
                double height=Math.Max(3,Math.Min(ActualHeight-4,3+VoiceVisual.Response(value)*29));double x=ActualWidth-(count-i)*6+2;
                var ink=Ui.Brush(index<0?"#D4D6DA":"#456DE6");drawing.DrawRoundedRectangle(ink,null,new Rect(x,(ActualHeight-height)/2,3,height),1.5,1.5);
            }
        }
    }
}
