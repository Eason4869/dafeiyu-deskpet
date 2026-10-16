using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
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

}
