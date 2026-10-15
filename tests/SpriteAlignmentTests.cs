using System;
using System.Windows;

namespace Dafeiyu.Tests
{
    public static class SpriteAlignmentTests
    {
        private static int passed,failed;
        public static int Main()
        {
            Test("Ebao run pelvis remains on the idle pelvis axis",delegate {
                // Median navy-short pixels in source y375..389 (idle frame 0 = 248).
                CheckAnchors("e-bao",1,new double[]{262,273,265,279,265,278,264,277},248,0);
                CheckAnchors("e-bao",2,new double[]{254,242,249,239,255,237,252,237},248,0);
            });
            Test("Dafeiyu run apron aligns while hair and tail can trail",delegate {
                CheckAnchors("dafeiyu",1,new double[]{287,278,277,277,281,262,271,282},237,0);
            });
            Test("Marfu run torso aligns independently of trailing robe",delegate {
                CheckAnchors("marfu",1,new double[]{279,267,266,275,278,279,274,280},240,0);
            });
            Test("mirroring retains the same per-pet axis",delegate {
                foreach(string id in new[]{"e-bao","dafeiyu","marfu"}) {
                    double center=id=="e-bao" ? 255 : id=="dafeiyu" ? 237 : 240;
                    for(int col=0;col<8;col++) {
                        Point normal=SpriteAlignment.Offset(id,1,col,false);
                        Point mirror=SpriteAlignment.Offset(id,1,col,true);
                        Equal(center,480-(center-normal.X)+mirror.X);
                        Equal(normal.Y,mirror.Y);
                    }
                }
            });
            Test("jump keeps source lift and aligns Ebao landing",delegate {
                foreach(string id in new[]{"e-bao","dafeiyu","marfu"})
                    for(int col=0;col<5;col++) Equal(0,SpriteAlignment.Offset(id,4,col,false).Y);
                Equal(255,240+SpriteAlignment.Offset("e-bao",4,4,false).X);
                Equal(237,228+SpriteAlignment.Offset("dafeiyu",4,4,false).X);
            });
            Test("run retains airborne stride height",delegate {
                foreach(string id in new[]{"e-bao","dafeiyu","marfu"})
                    for(int col=0;col<8;col++) Equal(0,SpriteAlignment.Offset(id,1,col,false).Y);
            });
            Test("Dafeiyu gaze feet stay on idle ground",delegate {
                int[][] feet={new[]{506,505,504,505,506,505,505,504},new[]{506,505,505,504,506,505,506,504}};
                for(int row=9;row<11;row++) for(int col=0;col<8;col++)
                    Equal(513,feet[row-9][col]+SpriteAlignment.Offset("dafeiyu",row,col,false).Y);
            });
            Test("head tilts do not drag Ebao planted feet",delegate {
                for(int col=0;col<6;col++) Equal(0,SpriteAlignment.Offset("e-bao",6,col,false).X);
                Equal(0,SpriteAlignment.Offset("e-bao",8,3,false).X);
            });
            Test("unknown imported skins and invalid cells are unchanged",delegate {
                foreach(string id in new[]{null,"","skin-e-bao","custom","E-BAO"})
                    Equal(new Point(),SpriteAlignment.Offset(id,1,3,true));
                Equal(new Point(),SpriteAlignment.Offset("e-bao",-1,0,false));
                Equal(new Point(),SpriteAlignment.Offset("dafeiyu",11,0,true));
                Equal(new Point(),SpriteAlignment.Offset("marfu",0,-1,false));
                Equal(new Point(),SpriteAlignment.Offset("marfu",3,4,false));
            });
            Console.WriteLine("SpriteAlignment: "+passed+" passed, "+failed+" failed");
            return failed==0 ? 0 : 1;
        }
        private static void CheckAnchors(string id,int row,double[] anchors,double target,double tolerance)
        {
            for(int col=0;col<anchors.Length;col++) {
                double actual=anchors[col]+SpriteAlignment.Offset(id,row,col,false).X;
                if(Math.Abs(actual-target)>tolerance) throw new Exception(id+" row "+row+" col "+col+": anchor "+actual+", expected "+target);
            }
        }
        private static void Equal(object expected,object actual) { if(!expected.Equals(actual)) throw new Exception("Expected "+expected+", got "+actual); }
        private static void Equal(double expected,double actual) { if(Math.Abs(expected-actual)>.0001) throw new Exception("Expected "+expected+", got "+actual); }
        private static void Test(string name,Action test) { try { test();passed++;Console.WriteLine("PASS "+name); } catch(Exception ex) { failed++;Console.WriteLine("FAIL "+name+": "+ex.Message); } }
    }
}
