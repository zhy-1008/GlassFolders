using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
[DataContract] public class FolderState {
 [DataMember] public string Id=Guid.NewGuid().ToString("N");
 [DataMember] public string Path;
 [DataMember] public double X=100,Y=100,Width=380,Height=280;
 [DataMember] public int Rows=2,Columns=4,IconSize=40,Tint=160;
 [DataMember] public bool Pinned;
 [DataMember] public string Sort="名称";
 [DataMember] public Dictionary<string,List<string>> Orders=new Dictionary<string,List<string>>();
}
[DataContract] public class Settings { [DataMember] public List<FolderState> Folders=new List<FolderState>(); }
static class Store {
 internal static string Root=System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"GlassFolders");
 internal static Settings Data=new Settings();
 internal static void Load(){ Directory.CreateDirectory(Root);var p=System.IO.Path.Combine(Root,"settings.json");if(!File.Exists(p))return;try{using(var f=File.OpenRead(p))Data=(Settings)new DataContractJsonSerializer(typeof(Settings)).ReadObject(f);if(Data==null||Data.Folders==null)throw new Exception("配置为空");}catch{try{File.Copy(p,p+".invalid-"+DateTime.Now.ToString("yyyyMMddHHmmss"),true);}catch{}Data=new Settings();}}
 internal static void Save(){try{Directory.CreateDirectory(Root);var p=System.IO.Path.Combine(Root,"settings.json");using(var f=File.Create(p+".tmp"))new DataContractJsonSerializer(typeof(Settings)).WriteObject(f,Data);if(File.Exists(p))File.Replace(p+".tmp",p,p+".bak");else File.Move(p+".tmp",p);}catch(Exception e){Log(e);}}
 internal static void Log(Exception e){try{File.AppendAllText(System.IO.Path.Combine(Root,"error.log"),DateTime.Now+" "+e+Environment.NewLine);}catch{}}
 internal static List<string> Sort(IEnumerable<string> paths,FolderState s,string dir){var p=paths.ToList();var comparer=StringComparer.CurrentCultureIgnoreCase;if(s.Sort=="自定义"){List<string> order;if(!s.Orders.TryGetValue(dir,out order))order=new List<string>();var ranks=order.Select((v,i)=>new{v,i}).GroupBy(x=>x.v,StringComparer.OrdinalIgnoreCase).ToDictionary(g=>g.Key,g=>g.First().i,StringComparer.OrdinalIgnoreCase);return p.OrderBy(x=>ranks.ContainsKey(x)?ranks[x]:int.MaxValue).ThenBy(x=>System.IO.Path.GetFileName(x),comparer).ToList();}if(s.Sort=="修改时间")return p.OrderByDescending(x=>File.GetLastWriteTimeUtc(x)).ThenBy(x=>x,comparer).ToList();if(s.Sort=="类型")return p.OrderBy(x=>Directory.Exists(x)?"":System.IO.Path.GetExtension(x),comparer).ThenBy(x=>System.IO.Path.GetFileName(x),comparer).ToList();return p.OrderBy(x=>System.IO.Path.GetFileName(x),comparer).ToList();}
}
