using System;
using System.Collections.Generic;
using System.Windows;

namespace Dafeiyu
{
    public static class PetMath
    {
        public static int LookDirection(double dx, double dy)
        {
            if (Math.Abs(dx) + Math.Abs(dy) < 1) return -1;
            double clockwise = Math.Atan2(dx, -dy) * 180 / Math.PI;
            return ((int)Math.Floor((clockwise + 360 + 11.25) / 22.5)) % 16;
        }
        public static Point ClampPosition(double x, double y, double width, double height, Rect area)
        {
            return new Point(Math.Max(area.Left, Math.Min(x, Math.Max(area.Left, area.Right - width))),
                Math.Max(area.Top, Math.Min(y, Math.Max(area.Top, area.Bottom - height))));
        }
    }

    public sealed class GazeController
    {
        // Continuous clockwise direction, in sixteenths of a full turn.
        public double Direction { get; private set; }
        public void Reset(double direction) { Direction=(direction%16+16)%16; }
        public void Advance(double dx,double dy,double seconds)
        {
            if(Double.IsNaN(seconds) || Double.IsInfinity(seconds) || seconds<=0 || dx*dx+dy*dy<256) return;
            double target=(Math.Atan2(dx,-dy)*8/Math.PI+16)%16;
            double distance=(target-Direction+24)%16-8;
            Reset(Direction+distance*(1-Math.Exp(-12*seconds)));
        }
        public int Frame(int multiplier) { return (int)Math.Round(Direction*multiplier)%(16*multiplier); }
    }

    public sealed class AnimationPlayer
    {
        public static readonly string[] States = { "idle", "running-right", "running-left", "waving", "jumping", "failed", "waiting", "running", "review" };
        public static readonly string[] Labels = { "待机", "向右跑", "向左跑", "招呼", "跳跃", "委屈", "等待", "思考", "检查成果" };
        public static readonly int[] Counts = { 6, 8, 8, 4, 5, 8, 6, 6, 6 };
        private static readonly double[] Periods = { .17, .11, .11, .16, .13, .16, .19, .18, .18 };
        private double elapsed;
        private double duration;
        public int FrameMultiplier { get; private set; }
        public string State { get; private set; }
        public int Frame { get; private set; }
        public int Row { get { return Array.IndexOf(States, State); } }
        public AnimationPlayer(int frameMultiplier=1)
        {
            if(frameMultiplier!=1 && frameMultiplier!=8) throw new ArgumentOutOfRangeException("frameMultiplier");
            FrameMultiplier=frameMultiplier; Play("idle", 0);
        }
        public void Play(string state, double seconds)
        {
            if (Array.IndexOf(States, state) < 0) throw new ArgumentException("Unknown animation state.");
            State = state; Frame = 0; elapsed = 0; duration = Math.Max(0, seconds);
        }
        public void Advance(double seconds)
        {
            if (seconds < 0 || Double.IsNaN(seconds) || Double.IsInfinity(seconds)) return;
            elapsed += seconds;
            if (duration > 0 && elapsed >= duration) { Play("idle", 0); return; }
            Frame = (int)((elapsed / Periods[Row] * FrameMultiplier) % (Counts[Row]*FrameMultiplier));
        }
    }

    public sealed class ChatMessage
    {
        public string Role { get; private set; }
        public string Content { get; private set; }
        public ChatMessage(string role, string content) { Role = role; Content = content; }
    }
    public sealed class ConversationLog
    {
        private readonly List<ChatMessage> messages = new List<ChatMessage>();
        public IList<ChatMessage> Messages { get { return messages.AsReadOnly(); } }
        public void Commit(string user, string assistant)
        {
            messages.Add(new ChatMessage("user", user)); messages.Add(new ChatMessage("assistant", assistant));
            while (messages.Count > 20) messages.RemoveRange(0, 2);
        }
        public List<ChatMessage> ForRequest(string persona, string user)
        {
            var result = new List<ChatMessage>();
            if (!String.IsNullOrWhiteSpace(persona)) result.Add(new ChatMessage("system", persona));
            result.AddRange(messages); result.Add(new ChatMessage("user", user)); return result;
        }
        public void Clear() { messages.Clear(); }
    }
}
