using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
static class Native {
 internal static event Action<int,int> MouseDown;
 delegate IntPtr MouseProc(int code,IntPtr wp,IntPtr lp);
 static MouseProc mouseProc;static IntPtr mouseHook;
 [DllImport("user32.dll")] static extern IntPtr SetWindowsHookEx(int id,MouseProc proc,IntPtr module,uint thread);
 [DllImport("user32.dll")] static extern IntPtr CallNextHookEx(IntPtr hook,int code,IntPtr wp,IntPtr lp);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern IntPtr GetModuleHandle(string name);
 internal static void StartMouse(){mouseProc=(code,wp,lp)=>{if(code>=0&&(wp.ToInt32()==513||wp.ToInt32()==516)){var handler=MouseDown;if(handler!=null)handler(Marshal.ReadInt32(lp),Marshal.ReadInt32(lp,4));}return CallNextHookEx(mouseHook,code,wp,lp);};mouseHook=SetWindowsHookEx(14,mouseProc,GetModuleHandle(null),0);}
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] internal static extern IntPtr FindWindow(string cls,string title);
 [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern IntPtr FindWindowEx(IntPtr parent,IntPtr after,string cls,string title);
 delegate bool EnumProc(IntPtr h,IntPtr p);
 [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc,IntPtr p);
 [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr h);
 internal static IntPtr DesktopHost(){IntPtr host=IntPtr.Zero;EnumWindows((h,p)=>{if(FindWindowEx(h,IntPtr.Zero,"SHELLDLL_DefView",null)!=IntPtr.Zero){host=h;return false;}return true;},IntPtr.Zero);return host;}
 [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW")] internal static extern IntPtr SetWindowLongPtr(IntPtr h,int n,IntPtr v);
 [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int w,int ht,uint flags);
 [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr h);
 [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr h);
 [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr h,int cmd);
 [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
 [DllImport("user32.dll")] internal static extern short GetAsyncKeyState(int key);
 [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h,int a,ref int v,int size);
 [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h,ref Margins m);
 [StructLayout(LayoutKind.Sequential)] struct Margins { public int l,r,t,b; }
 [StructLayout(LayoutKind.Sequential)] struct Accent { public int state,flags,color,animation; }
 [StructLayout(LayoutKind.Sequential)] struct Composition { public int attr; public IntPtr data; public int size; }
 [DllImport("user32.dll")] static extern int SetWindowCompositionAttribute(IntPtr h,ref Composition c);
 [DllImport("shell32.dll",CharSet=CharSet.Unicode)] static extern IntPtr SHGetFileInfo(string path,uint attr,ref FileInfo info,uint size,uint flags);
 [StructLayout(LayoutKind.Sequential,CharSet=CharSet.Unicode)] struct FileInfo { public IntPtr icon;public int index;public uint attr;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=260)] public string name;[MarshalAs(UnmanagedType.ByValTStr,SizeConst=80)] public string type; }
 [DllImport("user32.dll")] static extern bool DestroyIcon(IntPtr h);
 [DllImport("shell32.dll")] static extern int SHGetImageList(int size,ref Guid iid,out IntPtr list);
 [DllImport("comctl32.dll")] static extern IntPtr ImageList_GetIcon(IntPtr list,int index,int flags);
 internal static ImageSource Icon(string path) { var f=new FileInfo(); SHGetFileInfo(path,0,ref f,(uint)Marshal.SizeOf(f),0x4000);IntPtr list=IntPtr.Zero;var iid=new Guid("46EB5926-582E-4017-9FDF-E8998DAA0950");try{if(SHGetImageList(4,ref iid,out list)==0)f.icon=ImageList_GetIcon(list,f.index,1);}finally{if(list!=IntPtr.Zero)Marshal.Release(list);}if(f.icon==IntPtr.Zero)SHGetFileInfo(path,0,ref f,(uint)Marshal.SizeOf(f),0x100); if(f.icon==IntPtr.Zero)return null;try { var b=Imaging.CreateBitmapSourceFromHIcon(f.icon,Int32Rect.Empty,System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());b.Freeze();return NormalizeIcon(b);}finally{DestroyIcon(f.icon);} }
 internal static System.Windows.Media.Imaging.BitmapSource NormalizeIcon(System.Windows.Media.Imaging.BitmapSource source){
  var b=new System.Windows.Media.Imaging.FormatConvertedBitmap(source,PixelFormats.Bgra32,null,0);int w=b.PixelWidth,h=b.PixelHeight,stride=w*4;var data=new byte[stride*h];b.CopyPixels(data,stride,0);int left=w,top=h,right=-1,bottom=-1;
  for(int y=0;y<h;y++)for(int x=0;x<w;x++)if(data[y*stride+x*4+3]>8){left=Math.Min(left,x);right=Math.Max(right,x);top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
  if(right<left)return source;var cropped=new System.Windows.Media.Imaging.CroppedBitmap(b,new Int32Rect(left,top,right-left+1,bottom-top+1));cropped.Freeze();return cropped;
 }
 internal static double EdgeScrollStep(double x,double y,double width,double height){if(x<0||x>width||y<0||y>height||height<=0)return 0;double edge=Math.Min(48,height/3);if(y<edge)return -2-16*(edge-y)/edge;if(y>height-edge)return 2+16*(y-height+edge)/edge;return 0;}
 internal static void Glass(IntPtr h,bool dark,int alpha) {
  var source=HwndSource.FromHwnd(h); if(source!=null)source.CompositionTarget.BackgroundColor=Colors.Transparent;
  int corner=2;DwmSetWindowAttribute(h,33,ref corner,4);int d=dark?1:0;DwmSetWindowAttribute(h,20,ref d,4);
  int excluded=1;DwmSetWindowAttribute(h,12,ref excluded,4);
  var m=new Margins{l=0,r=0,t=0,b=0};DwmExtendFrameIntoClientArea(h,ref m);
  var a=new Accent {state=3, flags=0, color=0};
  var p=Marshal.AllocHGlobal(Marshal.SizeOf(a));try{Marshal.StructureToPtr(a,p,false);var c=new Composition{attr=19,data=p,size=Marshal.SizeOf(a)};SetWindowCompositionAttribute(h,ref c);}finally{Marshal.FreeHGlobal(p);}
 }
 internal static void Layer(IntPtr h,bool pinned) { var desktop=DesktopHost(); SetWindowLongPtr(h,-8,pinned?IntPtr.Zero:desktop);SetWindowPos(h,new IntPtr(pinned?-1:1),0,0,0,0,0x53); }
}
