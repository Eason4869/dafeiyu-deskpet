using System;
using System.Runtime.InteropServices;
using System.Windows;
using Forms = System.Windows.Forms;

namespace Dafeiyu
{
    public static class DesktopInterop
    {
        [StructLayout(LayoutKind.Sequential)] private struct NativePoint { public int X, Y; }
        [StructLayout(LayoutKind.Sequential)] private struct NativeRect { public int Left, Top, Right, Bottom; }
        [DllImport("user32.dll")] private static extern bool GetCursorPos(out NativePoint point);
        [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out NativeRect rect);
        [DllImport("user32.dll")] private static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] private static extern bool SetForegroundWindow(IntPtr handle);
        [DllImport("user32.dll",CharSet=CharSet.Unicode)] private static extern int GetClassName(IntPtr handle,System.Text.StringBuilder name,int count);
        [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr handle, IntPtr after, int x, int y, int width, int height, uint flags);
        [DllImport("user32.dll",EntryPoint="GetWindowLong")] private static extern int GetWindowLong32(IntPtr handle,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLong")] private static extern int SetWindowLong32(IntPtr handle,int index,int value);
        [DllImport("user32.dll",EntryPoint="GetWindowLongPtr")] private static extern IntPtr GetWindowLong64(IntPtr handle,int index);
        [DllImport("user32.dll",EntryPoint="SetWindowLongPtr")] private static extern IntPtr SetWindowLong64(IntPtr handle,int index,IntPtr value);
        public static void MakeToolWindow(IntPtr handle)
        {
            if(IntPtr.Size==8) SetWindowLong64(handle,-20,new IntPtr(GetWindowLong64(handle,-20).ToInt64() | 0x80 | 0x08000000));
            else SetWindowLong32(handle,-20,GetWindowLong32(handle,-20) | 0x80 | 0x08000000);
        }
        public static Point Cursor()
        { NativePoint point; GetCursorPos(out point); return new Point(point.X, point.Y); }
        public static Rect Bounds(IntPtr handle)
        { NativeRect rect; GetWindowRect(handle, out rect); return new Rect(rect.Left, rect.Top, Math.Max(1, rect.Right - rect.Left), Math.Max(1, rect.Bottom - rect.Top)); }
        public static Rect WorkArea(Point point)
        {
            var rect = Forms.Screen.FromPoint(new System.Drawing.Point((int)point.X, (int)point.Y)).WorkingArea;
            return new Rect(rect.Left, rect.Top, rect.Width, rect.Height);
        }
        public static Rect WorkArea(IntPtr handle)
        {
            var rect = Forms.Screen.FromHandle(handle).WorkingArea;
            return new Rect(rect.Left, rect.Top, rect.Width, rect.Height);
        }
        public static void Move(IntPtr handle, Point point)
        { SetWindowPos(handle, IntPtr.Zero, (int)Math.Round(point.X), (int)Math.Round(point.Y), 0, 0, 0x0001 | 0x0004 | 0x0010); }
        public static void ClampWindow(IntPtr handle)
        {
            Rect bounds = Bounds(handle); Rect area = WorkArea(handle);
            Move(handle, PetMath.ClampPosition(bounds.X, bounds.Y, bounds.Width, bounds.Height, area));
        }
        public static void PlaceBeside(IntPtr window, IntPtr pet)
        {
            Rect mine = Bounds(window), companion = Bounds(pet), area = WorkArea(pet);
            Move(window, PetMath.ClampPosition(companion.X - mine.Width - 18, companion.Bottom - mine.Height, mine.Width, mine.Height, area));
        }
        public static bool ForegroundIsFullScreen()
        {
            var foreground=GetForegroundWindow();if(foreground==IntPtr.Zero)return true;
            var name=new System.Text.StringBuilder(128);GetClassName(foreground,name,name.Capacity);
            if(name.ToString()=="Progman" || name.ToString()=="WorkerW")return false;
            Rect bounds=Bounds(foreground);var screen=Forms.Screen.FromHandle(foreground).Bounds;
            return bounds.Left<=screen.Left+1 && bounds.Top<=screen.Top+1 && bounds.Right>=screen.Right-1 && bounds.Bottom>=screen.Bottom-1;
        }
        public static void ForegroundMenu(System.Windows.Controls.ContextMenu menu)
        {
            var source=PresentationSource.FromVisual(menu) as System.Windows.Interop.HwndSource;
            if(source!=null)SetForegroundWindow(source.Handle);
        }
    }
}
