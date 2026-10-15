using System;

namespace Dafeiyu.Tests
{
    public static class InteractionTests
    {
        private static int passed;

        [STAThread] public static int Main()
        {
            try
            {
                LoopsAndInterruption();
                CompletionAndReplay();
                FeedingSequence();
                ClockInputs();
                OptionalArtwork();
                Console.WriteLine("Interaction tests: " + passed + " passed");
                return 0;
            }
            catch (Exception error)
            {
                Console.WriteLine("FAIL interaction: " + error.Message);
                return 1;
            }
        }

        private static void LoopsAndInterruption()
        {
            var animation = new InteractionAnimation();
            Check(!animation.Active && !animation.IsSleeping, "initial state leaves ordinary animation in control");
            animation.Start("lifted");
            Check(animation.Active && animation.Row == 0 && animation.Frame == 0, "drag starts lifted artwork");
            animation.Advance(.31);
            Check(animation.Frame > 0, "lifted advances poses");
            animation.Advance(10000);
            Check(animation.Active && animation.Name == "lifted" && animation.Frame >= 0 && animation.Frame < 4, "long drag keeps lifted loop bounded");
            animation.Start("landing");
            Check(animation.Name == "landing" && animation.Frame == 0 && animation.Row == 1, "release interrupts lifted with landing");
            animation.Advance(10);
            Check(!animation.Active, "landing returns control to ordinary animation");
            animation.Start("sleeping");
            animation.Advance(10000);
            Check(animation.Active && animation.IsSleeping && animation.Row == 6, "sleep remains asleep beyond ordinary action duration");
            animation.Start("poked");
            Check(!animation.IsSleeping && animation.Name == "poked", "direct interaction wakes sleeping pet");
            animation.Cancel();
            animation.Advance(10);
            Check(!animation.Active && !animation.IsSleeping && animation.Name == "", "cancel clears interaction without later resuming");
        }

        private static void CompletionAndReplay()
        {
            string[] actions = { "landing", "petting", "poked", "happy", "special" };
            int[] rows = { 1, 2, 3, 5, 7 };
            for (int i = 0; i < actions.Length; i++)
            {
                var animation = new InteractionAnimation();
                animation.Start(actions[i]);
                Check(animation.Row == rows[i], actions[i] + " selects its artwork row");
                animation.Advance(.3);
                animation.Start(actions[i]);
                Check(animation.Frame == 0 && animation.Active, actions[i] + " can be replayed from first pose");
                bool[] visited = new bool[4];
                for (int tick = 0; tick < 500 && animation.Active; tick++)
                {
                    visited[animation.Frame] = true;
                    animation.Advance(.01);
                }
                Check(!animation.Active, actions[i] + " finishes without an idle override");
                Check(visited[0] && visited[1] && visited[2] && visited[3], actions[i] + " shows all four poses");
            }
        }

        private static void FeedingSequence()
        {
            var animation = new InteractionAnimation();
            animation.Start("feeding");
            Check(animation.Row == 4, "feeding selects catch/eat artwork");
            for (int i = 0; i < 500 && animation.Name == "feeding"; i++) animation.Advance(.01);
            Check(animation.Active && animation.Name == "happy" && animation.Row == 5, "finishing a meal triggers happiness");
            animation.Advance(10);
            Check(!animation.Active, "feeding happiness finishes once");
            animation.Start("feeding");
            animation.Advance(100);
            Check(!animation.Active, "long frame consumes feeding and happiness without replay");
            animation.Start("feeding");
            animation.Cancel();
            animation.Advance(100);
            Check(!animation.Active, "cancelled meal cannot trigger delayed happiness");
            var coarse = new InteractionAnimation();
            var fine = new InteractionAnimation();
            coarse.Start("feeding"); fine.Start("feeding");
            coarse.Advance(1.75);
            for (int i = 0; i < 175; i++) fine.Advance(.01);
            Check(coarse.Active == fine.Active && coarse.Name == fine.Name && coarse.Frame == fine.Frame, "feeding transition preserves elapsed time across frame rates");
            coarse.Start("feeding"); fine.Start("feeding");
            coarse.Advance(2.8);
            for (int i = 0; i < 28; i++) fine.Advance(.1);
            Check(!coarse.Active && !fine.Active, "exact meal-plus-happiness duration completes at different frame rates");
        }

        private static void ClockInputs()
        {
            var animation = new InteractionAnimation();
            animation.Start("petting");
            animation.Advance(-1); animation.Advance(Double.NaN); animation.Advance(Double.PositiveInfinity); animation.Advance(0);
            Check(animation.Active && animation.Frame == 0, "invalid time inputs cannot corrupt playback");
            animation.Start("sleeping");
            animation.Advance(Double.MaxValue);
            Check(animation.Active && animation.Frame >= 0 && animation.Frame < 4, "huge finite elapsed time does not overflow loop");
            animation.Start("lifted");
            for (int i = 0; i < 10; i++) animation.Advance(.1);
            Check(animation.Frame == 0, "accumulated decimal clock values wrap at the loop boundary");
            animation.Start("unknown");
            Check(!animation.Active, "unknown action safely releases animation");
            animation.Start(null);
            Check(!animation.Active, "null action is safe");
        }

        private static void OptionalArtwork()
        {
            Check(InteractionAtlas.LoadForSkin((string)null) == null && InteractionAtlas.LoadForSkin("skin-custom") == null, "unknown and imported ids retain base artwork");
            Check(InteractionAtlas.LoadForSkin((SkinDefinition)null) == null, "no selected skin is safe");
            var imported = new SkinDefinition("e-bao", "Imported", "unused.png", null, false);
            Check(InteractionAtlas.LoadForSkin(imported) == null, "imported skins never borrow built-in artwork even with matching id");
            foreach (string id in new[] { "e-bao", "dafeiyu", "marfu" })
            {
                var atlas = InteractionAtlas.LoadForSkin(id);
                if (atlas == null) { Console.WriteLine("Optional interaction atlas absent: " + id); continue; }
                Check(atlas.FrameWidth == 480 && atlas.FrameHeight == 520, id + " interaction frame dimensions");
                for (int row = 0; row < 8; row++) for (int col = 0; col < 4; col++)
                {
                    var frame = atlas.GetFrame(row, col);
                    Check(frame.IsFrozen && frame.PixelWidth == 480 && frame.PixelHeight == 520, id + " immutable detached frame " + row + "/" + col);
                    bool visible = false, transparent = false;
                    for (int y = 0; y < 520; y += 4) for (int x = 0; x < 480; x += 4)
                    {
                        byte alpha = atlas.AlphaAt(row, col, x, y);
                        visible |= alpha > 16; transparent |= alpha == 0;
                    }
                    Check(visible && transparent, id + " frame contains character and transparent margin " + row + "/" + col);
                }
                Check(atlas.AlphaAt(0, 0, -1, 0) == 0 && atlas.AlphaAt(0, 0, 480, 0) == 0, "outside frame is click-through");
            }
        }

        private static void Check(bool condition, string name)
        {
            if (!condition) throw new Exception(name);
            passed++;
        }
    }
}
