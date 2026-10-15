using System;

namespace Dafeiyu
{
    // A separate clock keeps optional interaction artwork independent of the
    // imported-skin animation contract. All time values are in seconds.
    public sealed class InteractionAnimation
    {
        private static readonly string[] Names = { "lifted", "landing", "petting", "poked", "feeding", "happy", "sleeping", "special" };
        // Each action has four equally held poses; lifted and sleeping repeat.
        private static readonly double[] Durations = { 1.0, .64, 1.6, .8, 1.6, 1.2, 3.2, 2.0 };
        private const double ClockTolerance = 1e-9;
        private double elapsed;
        private double[] durations=Durations;
        private bool[] loops={true,false,false,false,false,false,true,false};
        public void Configure(SkinDefinition skin)
        {
            durations=(double[])Durations.Clone();loops=new[]{true,false,false,false,false,false,true,false};
            if(skin==null)return;
            for(int i=0;i<Names.Length;i++) {
                if(skin.Illustrated){var spec=skin.Manifest.Animations[Names[i]];durations[i]=spec.Frames/spec.Fps;loops[i]=spec.Loop;}
            }
        }
        public double Elapsed {get{return elapsed;}}
        private int row = -1;

        public bool Active { get { return row >= 0; } }
        public string Name { get { return Active ? Names[row] : ""; } }
        public int Row { get { return row; } }
        public int Frame { get { return Active ? Math.Min(3, (int)((elapsed + ClockTolerance) / (durations[row] / 4))) : 0; } }
        public bool IsSleeping { get { return row == 6; } }

        public void Start(string name)
        {
            row = Array.IndexOf(Names, name);
            elapsed = 0;
        }

        public void Cancel()
        {
            row = -1;
            elapsed = 0;
        }

        public void Advance(double dt)
        {
            if (!Active || dt <= 0 || Double.IsNaN(dt) || Double.IsInfinity(dt)) return;
            if (loops[row])
            {
                // Modulo first prevents elapsed + dt from overflowing after a
                // long suspension. No iteration depends on elapsed wall time.
                elapsed = (elapsed + dt % durations[row]) % durations[row];
                if (durations[row] - elapsed <= ClockTolerance) elapsed = 0;
                return;
            }

            double remaining = durations[row] - elapsed;
            if (dt < remaining - ClockTolerance)
            {
                elapsed += dt;
                return;
            }
            if (row == 4)
            {
                row = 5;
                elapsed = 0;
                Advance(Math.Max(0, dt - remaining));
            }
            else Cancel();
        }
    }
}
