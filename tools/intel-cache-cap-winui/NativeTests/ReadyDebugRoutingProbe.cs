using System;
using System.Text;
using System.Runtime.InteropServices;
class Probe {
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr LoadLibraryEx(string path,IntPtr reserved,uint flags);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern uint GetModuleFileName(IntPtr module,StringBuilder path,int size);
 static int Main(){string path=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),"dbghelp.dll");bool okay=true;foreach(uint flags in new uint[]{0,0x800}){IntPtr h=LoadLibraryEx(path,IntPtr.Zero,flags);int error=h==IntPtr.Zero?Marshal.GetLastWin32Error():0;var actual=new StringBuilder(4096);if(h!=IntPtr.Zero)GetModuleFileName(h,actual,actual.Capacity);Console.WriteLine("flags="+flags+" error="+error+" loaded="+actual);okay &= h!=IntPtr.Zero;}return okay?0:1;}
}
