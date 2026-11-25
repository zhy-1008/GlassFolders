using System;
using System.IO;
using System.Linq;
using System.Diagnostics;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Interop;
using System.Windows.Shell;
using System.Windows.Threading;
using Microsoft.Win32;
using Forms=System.Windows.Forms;
public class GlassApp:Application {
 internal static GlassApp CurrentApp;
 internal bool ModalOpen; internal System.Drawing.Icon AppIcon;
 internal List<FolderWindow> Widgets=new List<FolderWindow>();
 Forms.NotifyIcon tray;System.Threading.Mutex mutex;DispatcherTimer recovery;
 [STAThread] public static void Main(string[] args){
  if(args.Contains("--self-test")){SelfTest.Run();return;}
  RenderOptions.ProcessRenderMode=RenderMode.SoftwareOnly; Native.StartMouse(); var app=new GlassApp();CurrentApp=app;app.ShutdownMode=ShutdownMode.OnExplicitShutdown;
  bool first;app.mutex=new System.Threading.Mutex(true,args.Contains("--demo")?"Local\\GlassFolders.Preview":"Local\\GlassFolders.Desktop",out first);if(!first){MessageBox.Show("桌面文件夹已在运行，请从系统托盘管理组件。");return;}
  if(args.Contains("--demo"))Store.Root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"demo-settings");
  Store.Load(); app.DispatcherUnhandledException+=(s,e)=>{Store.Log(e.Exception);MessageBox.Show(e.Exception.Message,"桌面文件夹");e.Handled=true;};
  app.Startup+=(s,e)=>app.Start(args);app.Run();
 }
 void Start(string[] args){using(var stream=System.Reflection.Assembly.GetExecutingAssembly().GetManifestResourceStream("AppIcon"))using(var icon=new System.Drawing.Icon(stream)){AppIcon=(System.Drawing.Icon)icon.Clone();}
  tray=new Forms.NotifyIcon{Text="桌面文件夹 · Glass Folders",Icon=AppIcon,Visible=true};
  var menu=new Forms.ContextMenuStrip(); menu.Items.Add("新建文件夹组件…",null,(s,e)=>Dispatcher.Invoke(new Action(AddFolder)));menu.Items.Add("找回所有组件",null,(s,e)=>Dispatcher.Invoke(new Action(()=>{foreach(var w in Widgets){w.Recover();w.Activate();}})));
  var startup=new Forms.ToolStripMenuItem("开机自动启动"){Checked=StartupEnabled(),CheckOnClick=true};startup.Click+=(s,e)=>SetStartup(startup.Checked);menu.Items.Add(startup);
  menu.Items.Add("退出",null,(s,e)=>Dispatcher.Invoke(new Action(()=>{Store.Save();tray.Visible=false;tray.Dispose();Shutdown();})));tray.ContextMenuStrip=menu;tray.DoubleClick+=(s,e)=>Dispatcher.Invoke(new Action(AddFolder));
  if(args.Contains("--demo")&&Store.Data.Folders.Count==0){Store.Data.Folders.Add(new FolderState{Path=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"demo-files"),X=120,Y=100});}
  foreach(var f in Store.Data.Folders.ToArray())Open(f);
  if(Widgets.Count==0)AddFolder();
  recovery=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};recovery.Tick+=(s,e)=>{foreach(var w in Widgets.ToArray()){if(!Native.IsWindow(new WindowInteropHelper(w).Handle)){Widgets.Remove(w);Open(w.Config);}}};recovery.Start();
  Microsoft.Win32.SystemEvents.DisplaySettingsChanged+=(s,e)=>Dispatcher.BeginInvoke(new Action(()=>{foreach(var w in Widgets)w.Recover();}));
 }
 internal void Modal(Action action){ModalOpen=true;var windows=Widgets.ToArray();foreach(var w in windows)w.ModalLayer(true);try{action();}finally{ModalOpen=false;foreach(var w in windows)if(Widgets.Contains(w))w.ModalLayer(false);}}
 internal void AddFolder(){Modal(()=>{using(var owner=new Forms.Form{ShowInTaskbar=false,TopMost=true,Opacity=0,Width=1,Height=1,StartPosition=Forms.FormStartPosition.CenterScreen}){owner.Show();using(var dialog=new Forms.FolderBrowserDialog{Description="选择要显示的文件夹",ShowNewFolderButton=false})if(dialog.ShowDialog(owner)==Forms.DialogResult.OK){var f=new FolderState{Path=dialog.SelectedPath,X=100+Widgets.Count*30,Y=100+Widgets.Count*30};Store.Data.Folders.Add(f);Store.Save();Open(f);}}});}
 void Open(FolderState f){var w=new FolderWindow(f);Widgets.Add(w);w.Show();}
 internal void Remove(FolderWindow w){Modal(()=>{if(MessageBox.Show(w,"移除此组件？实际文件不会删除。","桌面文件夹",MessageBoxButton.YesNo)!=MessageBoxResult.Yes)return;Widgets.Remove(w);Store.Data.Folders.Remove(w.Config);w.Close();Store.Save();});}
 internal static bool StartupEnabled(){using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))return k!=null&&k.GetValue("GlassFolders")!=null;}
 internal static void SetStartup(bool on){try{using(var k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(on)k.SetValue("GlassFolders","\""+Process.GetCurrentProcess().MainModule.FileName+"\"");else k.DeleteValue("GlassFolders",false);}}catch(Exception e){MessageBox.Show(e.Message,"无法修改开机启动");}}
}
class FolderWindow:Window {
 internal FolderState Config;string current;bool expanded,animating,ready,dark,dragging,dialogOpen;IntPtr hwnd;IntPtr desktop;
 Border border;TextBlock title,status;Button expand,back;TextBox search;DockPanel root;Grid tiles;ScrollViewer scroll;StackPanel tools;
 DispatcherTimer refreshTimer,saveTimer,desktopTimer,dragScrollTimer;FileSystemWatcher watcher;DateTime lastRefresh;Dictionary<string,ImageSource> icons=new Dictionary<string,ImageSource>(StringComparer.OrdinalIgnoreCase);List<string> all=new List<string>();ContextMenu menu;Point dragStart;string dragPath;Rect compact;
 internal FolderWindow(FolderState cfg){
  Icon=Imaging.CreateBitmapSourceFromHIcon(GlassApp.CurrentApp.AppIcon.Handle,Int32Rect.Empty,System.Windows.Media.Imaging.BitmapSizeOptions.FromEmptyOptions());Config=cfg;current=cfg.Path;if(cfg.Orders==null)cfg.Orders=new Dictionary<string,List<string>>();cfg.Rows=Math.Max(1,Math.Min(8,cfg.Rows));cfg.Columns=Math.Max(1,Math.Min(10,cfg.Columns));
  TextOptions.SetTextRenderingMode(this,TextRenderingMode.Grayscale);TextOptions.SetTextFormattingMode(this,TextFormattingMode.Display); Title="桌面文件夹 · "+Name(current);Width=Math.Max(300,cfg.Width);Height=Math.Max(220,cfg.Height);Left=cfg.X;Top=cfg.Y;MinWidth=380;MinHeight=240;WindowStyle=WindowStyle.None;AllowsTransparency=true;ResizeMode=ResizeMode.CanResize;ShowInTaskbar=false;Background=Brushes.Transparent;FontFamily=new FontFamily("Segoe UI Variable Text, Microsoft YaHei UI");FontSize=12;
  WindowChrome.SetWindowChrome(this,new WindowChrome{CaptionHeight=0,ResizeBorderThickness=new Thickness(7),CornerRadius=new CornerRadius(18),GlassFrameThickness=new Thickness(0),UseAeroCaptionButtons=false});
  border=new Border{CornerRadius=new CornerRadius(18),BorderThickness=new Thickness(1),Padding=new Thickness(15)};Content=border;root=new DockPanel();border.Child=root;
  var header=new Grid{Height=48,Background=Brushes.Transparent};header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(114)});header.ColumnDefinitions.Add(new ColumnDefinition());header.ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(114)});DockPanel.SetDock(header,Dock.Top);root.Children.Add(header);
  title=new TextBlock{Text=Name(current),FontWeight=FontWeights.SemiBold,FontSize=18,TextAlignment=TextAlignment.Center,VerticalAlignment=VerticalAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,Margin=new Thickness(0)};Grid.SetColumn(title,1);header.Children.Add(title);
  var actions=new StackPanel{Orientation=Orientation.Horizontal};Grid.SetColumn(actions,2);header.Children.Add(actions);
  actions.Children.Add(Btn("＋","新建组件",()=>GlassApp.CurrentApp.AddFolder()));actions.Children.Add(Btn("×","关闭此组件",()=>GlassApp.CurrentApp.Remove(this)));actions.Children.Add(Btn("⋯","设置与排序",()=>{menu=MakeMenu();menu.PlacementTarget=header;menu.Placement=PlacementMode.Bottom;menu.IsOpen=true;}));
  header.MouseLeftButtonDown+=(s,e)=>{if(e.OriginalSource==title||e.OriginalSource==header){if(expanded)Collapse();try{DragMove();}catch{}SaveBounds();}};
  var footer=new DockPanel{Height=35,Margin=new Thickness(2,4,0,0)};DockPanel.SetDock(footer,Dock.Bottom);root.Children.Add(footer);expand=Btn("展开","展开完整文件夹",()=>{if(expanded)Collapse();else Expand();});DockPanel.SetDock(expand,Dock.Right);footer.Children.Add(expand);status=new TextBlock{VerticalAlignment=VerticalAlignment.Center,FontSize=12,TextTrimming=TextTrimming.CharacterEllipsis};footer.Children.Add(status);
  tools=new StackPanel{Orientation=Orientation.Horizontal,Margin=new Thickness(0,7,0,14),Visibility=Visibility.Collapsed};DockPanel.SetDock(tools,Dock.Top);root.Children.Add(tools);back=Btn("←","返回上一级",GoBack);tools.Children.Add(back);tools.Children.Add(new TextBlock{Text="搜索",VerticalAlignment=VerticalAlignment.Center,Margin=new Thickness(8,0,10,0)});search=new TextBox{Width=260,Height=30,Padding=new Thickness(8,4,8,4),ToolTip="搜索当前文件夹",VerticalContentAlignment=VerticalAlignment.Center};System.Windows.Automation.AutomationProperties.SetName(search,"搜索当前文件夹");search.TextChanged+=(s,e)=>Render();tools.Children.Add(search);
  tiles=new Grid();scroll=new ScrollViewer{Content=tiles,VerticalScrollBarVisibility=ScrollBarVisibility.Disabled,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};root.Children.Add(scroll);
  dragScrollTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(30)};dragScrollTimer.Tick+=(s,e)=>{if(!dragging||!expanded)return;var p=scroll.PointFromScreen(new Point(Forms.Cursor.Position.X,Forms.Cursor.Position.Y));double step=Native.EdgeScrollStep(p.X,p.Y,scroll.ActualWidth,scroll.ActualHeight);if(step!=0)scroll.ScrollToVerticalOffset(scroll.VerticalOffset+step);};
  saveTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(400)};saveTimer.Tick+=(s,e)=>{saveTimer.Stop();SaveBounds();};
  refreshTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(500)};refreshTimer.Tick+=(s,e)=>{refreshTimer.Stop();Refresh();};
  desktopTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(350)};desktopTimer.Tick+=(s,e)=>{
   var p=Native.DesktopHost();if(!GlassApp.CurrentApp.ModalOpen&&p!=desktop){desktop=p;Native.Layer(hwnd,Config.Pinned);}
   if(Native.IsIconic(hwnd)||!Native.IsWindowVisible(hwnd))Native.ShowWindow(hwnd,4);
   if(!GlassApp.CurrentApp.ModalOpen&&!Config.Pinned&&!IsActive&&!expanded)Native.SetWindowPos(hwnd,new IntPtr(1),0,0,0,0,0x13);
   if(expanded&&!GlassApp.CurrentApp.ModalOpen&&!dialogOpen&&!dragging&&(menu==null||!menu.IsOpen)&&(Native.GetAsyncKeyState(1)&0x8000)!=0){var pt=PointFromScreen(new Point(Forms.Cursor.Position.X,Forms.Cursor.Position.Y));if(pt.X<0||pt.Y<0||pt.X>ActualWidth||pt.Y>ActualHeight)Collapse();}
   if(watcher==null&&DateTime.UtcNow-lastRefresh>TimeSpan.FromSeconds(5)&&!dragging)Refresh();
  };
  SourceInitialized+=(s,e)=>{hwnd=new WindowInteropHelper(this).Handle;HwndSource.FromHwnd(hwnd).AddHook(Hook);Theme();Native.Layer(hwnd,Config.Pinned);desktop=Native.DesktopHost();};
  Native.MouseDown+=OnGlobalMouse;
  Loaded+=(s,e)=>{ready=true;Recover();Refresh();Native.ShowWindow(hwnd,4);desktopTimer.Start();if(Environment.GetCommandLineArgs().Contains("--demo")){var diagnostic=new DispatcherTimer{Interval=TimeSpan.FromSeconds(2)};diagnostic.Tick+=(o,a)=>{diagnostic.Stop();File.WriteAllText(Path.Combine(Store.Root,"window-state.txt"),"Visible="+IsVisible+" NativeVisible="+Native.IsWindowVisible(hwnd)+" Handle="+hwnd+" Desktop="+desktop+" Bounds="+Left+","+Top+","+Width+","+Height);};diagnostic.Start();}};
  SizeChanged+=(s,e)=>{if(!animating){Render();if(!expanded){saveTimer.Stop();saveTimer.Start();}}};LocationChanged+=(s,e)=>{if(!expanded&&!animating){saveTimer.Stop();saveTimer.Start();}};
  Closed+=(s,e)=>{Native.MouseDown-=OnGlobalMouse;ready=false;dragScrollTimer.Stop();desktopTimer.Stop();refreshTimer.Stop();saveTimer.Stop();if(watcher!=null)watcher.Dispose();};
  PreviewKeyDown+=(s,e)=>{if(e.Key==Key.Escape&&expanded){Collapse();e.Handled=true;}else if(e.Key==Key.F5){Refresh();e.Handled=true;}};
 }
 IntPtr Hook(IntPtr h,int msg,IntPtr wp,IntPtr lp,ref bool handled){if(msg==0x112&&((wp.ToInt64()&0xfff0)==0xf020)){handled=true;return IntPtr.Zero;}if(msg==0x1a||msg==0x320)Dispatcher.BeginInvoke(new Action(Theme));return IntPtr.Zero;}
 new static string Name(string p){return Path.GetFileName(p.TrimEnd(Path.DirectorySeparatorChar))==""?p:Path.GetFileName(p.TrimEnd(Path.DirectorySeparatorChar));}
 Button Btn(string text,string tip,Action action){var b=new Button{Content=text,ToolTip=tip,Padding=new Thickness(9,4,9,4),Margin=new Thickness(2),Background=Brushes.Transparent,BorderThickness=new Thickness(0),Cursor=Cursors.Hand,MinHeight=28};b.SetBinding(Control.ForegroundProperty,new System.Windows.Data.Binding("Foreground"){Source=this});b.Template=(ControlTemplate)System.Windows.Markup.XamlReader.Parse("<ControlTemplate xmlns=\"http://schemas.microsoft.com/winfx/2006/xaml/presentation\" TargetType=\"Button\"><Border CornerRadius=\"12\" Background=\"#22FFFFFF\" Padding=\"{TemplateBinding Padding}\"><ContentPresenter HorizontalAlignment=\"Center\" VerticalAlignment=\"Center\"/></Border></ControlTemplate>");if(text=="＋"||text=="×"||text=="⋯"){b.Width=34;b.Height=34;b.Padding=new Thickness(0);b.VerticalAlignment=VerticalAlignment.Center;var glyph=new System.Windows.Shapes.Path{Width=16,Height=16,Stretch=Stretch.None,StrokeThickness=1.6,StrokeStartLineCap=PenLineCap.Round,StrokeEndLineCap=PenLineCap.Round,Data=Geometry.Parse(text=="＋"?"M 8,1 L 8,15 M 1,8 L 15,8":text=="×"?"M 2,2 L 14,14 M 14,2 L 2,14":"M 1,8 L 1.3,8 M 8,8 L 8.3,8 M 15,8 L 15.3,8")};glyph.SetBinding(System.Windows.Shapes.Shape.StrokeProperty,new System.Windows.Data.Binding("Foreground"){Source=this});b.Content=glyph;System.Windows.Automation.AutomationProperties.SetName(b,tip);}
  var scale=new ScaleTransform(1,1);b.RenderTransform=scale;b.RenderTransformOrigin=new Point(.5,.5);b.MouseEnter+=(s,e)=>{scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(1.14,TimeSpan.FromMilliseconds(130)));scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(1.14,TimeSpan.FromMilliseconds(130)));b.BeginAnimation(OpacityProperty,new DoubleAnimation(1,TimeSpan.FromMilliseconds(130)));};b.Opacity=.78;b.MouseLeave+=(s,e)=>{scale.BeginAnimation(ScaleTransform.ScaleXProperty,new DoubleAnimation(1,TimeSpan.FromMilliseconds(160)));scale.BeginAnimation(ScaleTransform.ScaleYProperty,new DoubleAnimation(1,TimeSpan.FromMilliseconds(160)));b.BeginAnimation(OpacityProperty,new DoubleAnimation(.78,TimeSpan.FromMilliseconds(160)));};b.Click+=(s,e)=>action();return b;}
 void Theme(){try{using(var k=Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"))dark=k!=null&&Convert.ToInt32(k.GetValue("AppsUseLightTheme",1))==0;}catch{dark=false;}Foreground=Brushes.WhiteSmoke;title.Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=5,ShadowDepth=1,Opacity=.55};border.BorderBrush=new SolidColorBrush(Color.FromArgb(75,255,255,255));border.Background=new SolidColorBrush(dark?Color.FromArgb(10,20,26,34):Color.FromArgb(12,255,255,255));search.Background=new SolidColorBrush(dark?Color.FromArgb(80,60,66,76):Color.FromArgb(145,255,255,255));search.Foreground=dark?Brushes.WhiteSmoke:Brushes.Black;search.BorderBrush=new SolidColorBrush(Color.FromArgb(60,128,128,128));Native.Glass(hwnd,dark,45);}
 internal void Recover(){var scr=Forms.Screen.FromHandle(hwnd).WorkingArea;var src=PresentationSource.FromVisual(this);double scale=src==null?1:src.CompositionTarget.TransformToDevice.M11;var area=new Rect(scr.X/scale,scr.Y/scale,scr.Width/scale,scr.Height/scale);Width=Math.Min(Width,area.Width);Height=Math.Min(Height,area.Height);Left=Math.Max(area.Left,Math.Min(Left,area.Right-Width));Top=Math.Max(area.Top,Math.Min(Top,area.Bottom-Height));SaveBounds();}
 void SaveBounds(){if(!ready||expanded||animating)return;Config.X=Left;Config.Y=Top;Config.Width=Width;Config.Height=Height;Store.Save();}
 void Watch(){if(watcher!=null){watcher.Dispose();watcher=null;}try{watcher=new FileSystemWatcher(current){NotifyFilter=NotifyFilters.FileName|NotifyFilters.DirectoryName|NotifyFilters.LastWrite};FileSystemEventHandler change=(s,e)=>Dispatcher.BeginInvoke(new Action(()=>{icons.Remove(e.FullPath);refreshTimer.Stop();refreshTimer.Start();}));watcher.Created+=change;watcher.Deleted+=change;watcher.Changed+=change;watcher.Renamed+=(s,e)=>Dispatcher.BeginInvoke(new Action(()=>{icons.Clear();refreshTimer.Start();}));watcher.Error+=(s,e)=>Dispatcher.BeginInvoke(new Action(()=>{if(watcher!=null)watcher.Dispose();watcher=null;refreshTimer.Start();}));watcher.EnableRaisingEvents=true;}catch{}}
 void Refresh(){lastRefresh=DateTime.UtcNow;if(!ready)return;if(dragging){refreshTimer.Start();return;}try{all=Store.Sort(Directory.EnumerateFileSystemEntries(current).Where(p=>(File.GetAttributes(p)&(FileAttributes.Hidden|FileAttributes.System))==0),Config,current);if(watcher==null||watcher.Path!=current)Watch();Render();}catch(Exception e){if(watcher!=null){watcher.Dispose();watcher=null;}all.Clear();Render();status.Text="文件夹不可用 · 按 F5 重试";status.ToolTip=e.Message;}}
 void Render(){if(tiles==null)return;tiles.Children.Clear();tiles.RowDefinitions.Clear();tiles.ColumnDefinitions.Clear();if(!ready)return;
  title.Text=Name(current);title.ToolTip=current;back.IsEnabled=!string.Equals(current,Config.Path,StringComparison.OrdinalIgnoreCase);
  var filtered=all.Where(p=>!expanded||Name(p).IndexOf(search.Text,StringComparison.CurrentCultureIgnoreCase)>=0).ToList();int cols=Math.Max(1,(int)((ActualWidth-32)/84));Config.Rows=Math.Max(1,(int)((ActualHeight-120)/76));int count=expanded?filtered.Count:Math.Min(filtered.Count,Config.Rows*cols);int rows=expanded?Math.Max(1,(count+cols-1)/cols):Config.Rows;
  for(int c=0;c<cols;c++)tiles.ColumnDefinitions.Add(new ColumnDefinition());for(int r=0;r<rows;r++)tiles.RowDefinitions.Add(new RowDefinition{Height=new GridLength(76)});
  status.Text=expanded?filtered.Count+" 个项目":count+" / "+all.Count+" 个项目";
  for(int i=0;i<count;i++){string path=filtered[i];var b=new Button{Background=Brushes.Transparent,BorderThickness=new Thickness(0),Padding=new Thickness(3),Margin=new Thickness(2),ToolTip=Name(path),Cursor=Cursors.Hand,VerticalContentAlignment=VerticalAlignment.Center,HorizontalContentAlignment=HorizontalAlignment.Stretch,AllowDrop=true};b.SetBinding(Control.ForegroundProperty,new System.Windows.Data.Binding("Foreground"){Source=this});System.Windows.Automation.AutomationProperties.SetName(b,Name(path));var panel=new StackPanel{HorizontalAlignment=HorizontalAlignment.Stretch};ImageSource icon;if(!icons.TryGetValue(path,out icon)){icon=Native.Icon(path);icons[path]=icon;}double sz=32;panel.Children.Add(new Image{Source=icon,Width=sz,Height=sz,Margin=new Thickness(0,0,0,4)});panel.Children.Add(new TextBlock{Text=Directory.Exists(path)?Name(path):Path.GetFileNameWithoutExtension(path),TextAlignment=TextAlignment.Center,TextTrimming=TextTrimming.CharacterEllipsis,MaxHeight=36,FontSize=14,Effect=new System.Windows.Media.Effects.DropShadowEffect{BlurRadius=4,ShadowDepth=1,Opacity=.6}});b.Content=panel;
   b.Click+=(s,e)=>{if(!dragging)Open(path);};
   b.PreviewMouseLeftButtonDown+=(s,e)=>{dragStart=e.GetPosition(this);dragPath=path;};
   b.PreviewMouseMove+=(s,e)=>{if(e.LeftButton!=MouseButtonState.Pressed||dragPath!=path)return;var pt=e.GetPosition(this);if(Math.Abs(pt.X-dragStart.X)<SystemParameters.MinimumHorizontalDragDistance&&Math.Abs(pt.Y-dragStart.Y)<SystemParameters.MinimumVerticalDragDistance)return;dragging=true;dragScrollTimer.Start();try{DragDrop.DoDragDrop(b,new DataObject("GlassFolders/"+Config.Id,path),DragDropEffects.Move);}finally{dragScrollTimer.Stop();dragging=false;dragPath=null;}e.Handled=true;};
   b.DragOver+=(s,e)=>{e.Effects=e.Data.GetDataPresent("GlassFolders/"+Config.Id)&&search.Text.Length==0?DragDropEffects.Move:DragDropEffects.None;e.Handled=true;};
   b.Drop+=(s,e)=>{var from=e.Data.GetData("GlassFolders/"+Config.Id) as string;if(from==null||from==path||search.Text.Length>0)return;var list=all.ToList();if(!list.Remove(from))return;list.Insert(list.IndexOf(path),from);Config.Sort="自定义";Config.Orders[current]=list;Store.Save();Refresh();e.Handled=true;};
   var cm=new ContextMenu();var mi=new MenuItem{Header="在资源管理器中打开"};mi.Click+=(s,e)=>Explorer(path);cm.Items.Add(mi);b.ContextMenu=cm;cm.Opened+=(s,e)=>menu=cm;
   Grid.SetColumn(b,i%cols);Grid.SetRow(b,i/cols);tiles.Children.Add(b);
  }
 }
 void Open(string p){if(Directory.Exists(p)){if(!expanded)Expand();current=p;search.Text="";Refresh();}else{try{Process.Start(new ProcessStartInfo(p){UseShellExecute=true});if(expanded)Collapse();}catch(Exception e){MessageBox.Show(e.Message,"无法打开项目");}}}
 void GoBack(){if(string.Equals(current,Config.Path,StringComparison.OrdinalIgnoreCase))return;var p=Directory.GetParent(current);current=p==null?Config.Path:p.FullName;search.Text="";Refresh();}
 void Explorer(string p){try{Process.Start("explorer.exe",Directory.Exists(p)?"\""+p+"\"":"/select,\""+p+"\"");}catch(Exception e){MessageBox.Show(e.Message);}}
 void Expand(){if(expanded||animating)return;SaveBounds();compact=new Rect(Left,Top,Width,Height);expanded=true;tools.Visibility=Visibility.Visible;expand.Content="收起";scroll.VerticalScrollBarVisibility=ScrollBarVisibility.Auto;var scr=Forms.Screen.FromHandle(hwnd).WorkingArea;var dpi=PresentationSource.FromVisual(this).CompositionTarget.TransformToDevice.M11;double w=Math.Min(Math.Max(620,Width),scr.Width/dpi),h=Math.Min(Math.Max(500,Height),scr.Height/dpi);Left=Math.Max(scr.Left/dpi,Math.Min(Left,scr.Right/dpi-w));Top=Math.Max(scr.Top/dpi,Math.Min(Top,scr.Bottom/dpi-h));Animate(w,h);}
 void Collapse(){if(!expanded)return;expanded=false;current=Config.Path;search.Text="";tools.Visibility=Visibility.Collapsed;expand.Content="展开";scroll.VerticalScrollBarVisibility=ScrollBarVisibility.Disabled;scroll.ScrollToTop();Left=compact.X;Top=compact.Y;Animate(compact.Width,compact.Height);Refresh();}
 void Animate(double w,double h){animating=true;var duration=TimeSpan.FromMilliseconds(200);var a=new DoubleAnimation(w,duration){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};var b=new DoubleAnimation(h,duration){EasingFunction=new CubicEase{EasingMode=EasingMode.EaseOut}};b.Completed+=(s,e)=>{BeginAnimation(WidthProperty,null);BeginAnimation(HeightProperty,null);Width=w;Height=h;animating=false;Render();};BeginAnimation(WidthProperty,a);BeginAnimation(HeightProperty,b);}
 ContextMenu MakeMenu(){var m=new ContextMenu();Action<string,Action> add=(label,action)=>{var i=new MenuItem{Header=label};i.Click+=(s,e)=>action();m.Items.Add(i);};var sort=new MenuItem{Header="排序"};foreach(var mode in new[]{"名称","类型","修改时间","自定义"}){string value=mode;var i=new MenuItem{Header=mode,IsCheckable=true,IsChecked=Config.Sort==mode};i.Click+=(s,e)=>{Config.Sort=value;Store.Save();Refresh();};sort.Items.Add(i);}m.Items.Add(sort);add(Config.Pinned?"取消置顶":"置顶显示",()=>{Config.Pinned=!Config.Pinned;Native.Layer(hwnd,Config.Pinned);Store.Save();});add("在资源管理器中打开",()=>Explorer(current));add("刷新",Refresh);add("新建组件…",()=>GlassApp.CurrentApp.AddFolder());add("移除此组件",()=>GlassApp.CurrentApp.Remove(this));return m;}
 void OnGlobalMouse(int x,int y){Dispatcher.BeginInvoke(new Action(()=>{if(ready)OutsideClick(x,y);}));}
 internal void ModalLayer(bool active){dialogOpen=active;if(active){Native.SetWindowLongPtr(hwnd,-8,IntPtr.Zero);Native.SetWindowPos(hwnd,new IntPtr(-1),0,0,0,0,0x13);}else Native.Layer(hwnd,Config.Pinned);}
 void OutsideClick(int x,int y){if(!expanded||dragging||GlassApp.CurrentApp.ModalOpen||(menu!=null&&menu.IsOpen))return;var pt=PointFromScreen(new Point(x,y));if(pt.X<0||pt.Y<0||pt.X>=ActualWidth||pt.Y>=ActualHeight)Collapse();}
}



