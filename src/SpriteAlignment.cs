using System;
using System.Windows;

namespace Dafeiyu
{
    /// <summary>
    /// Registration of the original built-in artwork, in 480 x 520 source pixels.
    /// Pass the sampled atlas row/column AFTER action-row and stride remapping.
    /// Imported artwork has its own registration and must not use these offsets.
    /// </summary>
    public static class SpriteAlignment
    {
        private static readonly int[] Counts={6,8,8,4,5,8,6,6,6,8,8};
        // Ebao: navy shorts/pelvis, rather than the swinging hands, shoes or head.
        private static readonly int[] EbaoRight={-14,-25,-17,-31,-17,-30,-16,-29};
        private static readonly int[] EbaoLeft={-6,6,-1,9,-7,11,-4,11};
        private static readonly int[] EbaoJump={0,15,15,15,15};
        // Dafeiyu: apron/body axis; trailing hair and whale tail are not anchors.
        private static readonly int[] DafeiyuRight={-50,-41,-40,-40,-44,-25,-34,-45};
        private static readonly int[] DafeiyuLookUpperX={9,4,6,5,5,3,2,1};
        private static readonly int[] DafeiyuLookLowerX={6,4,4,2,13,3,1,2};
        private static readonly int[][] DafeiyuGroundY={
            new[]{0,0,0,0,0,0}, new int[8], new int[8], new int[4], new int[5],
            new[]{3,3,3,3,3,3,4,3}, new[]{2,2,2,3,2,2},
            new[]{1,1,0,1,1,1}, new[]{3,3,4,3,3,3},
            new[]{7,8,9,8,7,8,8,9}, new[]{7,8,8,9,7,8,7,9}
        };
        // Marfu: waist/body axis, excluding long hair, wand and trailing robe.
        private static readonly int[] MarfuRight={-39,-27,-26,-35,-38,-39,-34,-40};

        public static Point Offset(string skinId,int row,int col,bool mirrored)
        {
            if(row<0 || row>=Counts.Length || col<0 || col>=Counts[row]) return new Point();
            double x=0,y=0,center;
            if(String.Equals(skinId,"e-bao",StringComparison.Ordinal))
            {
                center=255;
                if(row==1) x=EbaoRight[col];
                else if(row==2) x=EbaoLeft[col];
                else if(row==4) x=EbaoJump[col];
            }
            else if(String.Equals(skinId,"dafeiyu",StringComparison.Ordinal))
            {
                center=237;
                if(row==1) x=DafeiyuRight[col];
                else if(row==3) x=8;
                else if(row==4) x=9;
                else if(row==9) x=DafeiyuLookUpperX[col];
                else if(row==10) x=DafeiyuLookLowerX[col];
                y=DafeiyuGroundY[row][col];
            }
            else if(String.Equals(skinId,"marfu",StringComparison.Ordinal))
            {
                center=240;
                if(row==1) x=MarfuRight[col];
            }
            else return new Point();

            // RenderTransformOrigin=.5 mirrors about x240. Also compensate the
            // built-in's off-center idle axis, so changing direction stays put.
            if(mirrored) x=2*center-480-x;
            // Jump lift and running stride height are present in the artwork:
            // their Y offsets intentionally remain zero, including airborne feet.
            return new Point(x,y);
        }
    }
}
