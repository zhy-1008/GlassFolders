using System;
using System.IO;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;
class Installer {
 static string target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"Programs","GlassFolders");
 [STAThread] static void Main(string[] args){Application.EnableVisualStyles();bool remove=Array.IndexOf(args,"--uninstall")>=0;var form=new Form{Text=remove?"卸载桌面文件夹":"安装桌面文件夹 · Glass Folders",Width=510,Height=310,StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,Icon=System.Drawing.Icon.ExtractAssociatedIcon(Application.ExecutablePath)};
  var label=new Label{Left=28,Top=25,Width=440,Height=120,Text=remove?"将移除程序、开始菜单快捷方式和开机启动。\n\n组件配置会保留，绑定的真实文件夹不受影响。":"桌面文件夹 "+VersionInfo.Number+"\n磨砂玻璃 · 多文件夹 · 自定义排序\n\n仅安装到当前用户，无需管理员权限。\n安装位置："+target};form.Controls.Add(label);
  var startup=new CheckBox{Left=28,Top=160,Width=430,Text="登录 Windows 时自动启动",Checked=true,Visible=!remove};form.Controls.Add(startup);var run=new CheckBox{Left=28,Top=190,Width=430,Text="安装完成后启动",Checked=true,Visible=!remove};form.Controls.Add(run);
  var button=new Button{Left=335,Top=225,Width=130,Text=remove?"卸载":"安装"};form.Controls.Add(button);button.Click+=(s,e)=>{try{
   if(Process.GetProcessesByName("GlassFolders").Length>0){MessageBox.Show("请先从系统托盘退出桌面文件夹，再继续。","桌面文件夹");return;}
   string shortcut=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs),"桌面文件夹.lnk");
   if(remove){using(var k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run"))k.DeleteValue("GlassFolders",false);Registry.CurrentUser.DeleteSubKeyTree(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\GlassFolders",false);if(File.Exists(shortcut))File.Delete(shortcut);foreach(var name in new[]{"GlassFolders.exe","GlassFolders.exe.config"}){var path=Path.Combine(target,name);if(File.Exists(path))File.Delete(path);}MessageBox.Show("已卸载。配置已保留。\n安装目录中可能保留卸载程序本身，可手动删除。");form.Close();return;}
   Directory.CreateDirectory(target);using(var input=Assembly.GetExecutingAssembly().GetManifestResourceStream("GlassFolders.exe"))using(var output=File.Create(Path.Combine(target,"GlassFolders.exe")))input.CopyTo(output);
   string exe=Path.Combine(target,"GlassFolders.exe");string uninstall=Path.Combine(target,"Uninstall.exe");if(!string.Equals(Application.ExecutablePath,uninstall,StringComparison.OrdinalIgnoreCase))File.Copy(Application.ExecutablePath,uninstall,true);
   Type type=Type.GetTypeFromProgID("WScript.Shell");dynamic shell=Activator.CreateInstance(type);dynamic link=shell.CreateShortcut(shortcut);link.TargetPath=exe;link.IconLocation=exe+",0";link.WorkingDirectory=target;link.Description="桌面文件夹 · Glass Folders";link.Save();
   using(var k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run")){if(startup.Checked)k.SetValue("GlassFolders","\""+exe+"\"");else k.DeleteValue("GlassFolders",false);}
   using(var k=Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\GlassFolders")){k.SetValue("DisplayName","桌面文件夹 · Glass Folders");k.SetValue("DisplayVersion",VersionInfo.Number);k.SetValue("Publisher","Glass Folders");k.SetValue("InstallLocation",target);k.SetValue("DisplayIcon",exe);k.SetValue("UninstallString","\""+uninstall+"\" --uninstall");k.SetValue("NoModify",1);k.SetValue("NoRepair",1);}
   if(run.Checked)Process.Start(exe);MessageBox.Show("安装完成。可从开始菜单启动，也可从系统托盘管理组件。");form.Close();
  }catch(Exception ex){MessageBox.Show(ex.Message,"操作失败，可重试");}};Application.Run(form);
 }
}



