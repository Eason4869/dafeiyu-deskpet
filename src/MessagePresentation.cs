using System;
using System.Collections.Generic;
using System.Windows.Threading;

namespace Dafeiyu
{
    public static class MessagePresentation
    {
        public static string Status(string value)
        {return value=="pending"?"正在生成…":value=="cancelled"?"已停止":value=="interrupted"?"已中断":value=="error"?"回复未完成":"";}
        public static string QuickText(ChatMessage message)
        {
            if(message==null)return "写下一句话，我在这里。";
            string text=String.IsNullOrEmpty(message.Content) && message.Status=="pending"?"正在等待回复…":message.Content;
            return message.Status=="complete"?text:text+"\n\n〔"+Status(message.Status)+"〕";
        }
    }
    // Network callbacks replace snapshots; only the latest snapshot per message reaches WPF.
    public sealed class MessageUpdatePump : IDisposable
    {
        readonly ConversationSession session;
        readonly Action<ChatMessage> display;
        readonly Dictionary<string,ChatMessage> pending=new Dictionary<string,ChatMessage>();
        readonly DispatcherTimer timer;
        readonly object gate=new object();
        bool disposed;
        public MessageUpdatePump(ConversationSession source,Action<ChatMessage> update)
        {
            session=source;display=update;timer=new DispatcherTimer(DispatcherPriority.Background){Interval=TimeSpan.FromMilliseconds(50)};
            session.MessageUpdated+=Receive;timer.Tick+=Tick;timer.Start();
        }
        void Receive(ChatMessage message){lock(gate){if(!disposed)pending[message.Id]=message;}}
        void Tick(object sender,EventArgs args)
        {
            ChatMessage[] updates;lock(gate){if(disposed)return;updates=new ChatMessage[pending.Count];pending.Values.CopyTo(updates,0);pending.Clear();}
            foreach(var message in updates)display(message);
        }
        public void Dispose(){lock(gate){disposed=true;pending.Clear();}session.MessageUpdated-=Receive;timer.Stop();timer.Tick-=Tick;}
    }
}
