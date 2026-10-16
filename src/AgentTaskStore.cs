using System;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
namespace Dafeiyu
{
    public sealed class AgentTaskStore
    {
        readonly string directory;readonly object gate=new object();Task worker=Task.FromResult(0);string warning="";readonly System.Collections.Generic.Dictionary<string,string> warnings=new System.Collections.Generic.Dictionary<string,string>();readonly System.Collections.Generic.HashSet<string> unreadable=new System.Collections.Generic.HashSet<string>();
        public string Warning{get{lock(gate)return warning;}}
        public AgentTaskStore(string path){directory=Path.GetFullPath(path);}
        string FilePath(string persona,string workspace){using(var sha=SHA256.Create())return Path.Combine(directory,BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes((persona??"")+"\n"+Path.GetFullPath(workspace).TrimEnd('\\','/').ToUpperInvariant()))).Replace("-","").ToLowerInvariant()+".task");}
        void Warn(string path,string text){lock(gate){if(String.IsNullOrEmpty(text))warnings.Remove(path);else warnings[path]=text;warning=String.Join(" ",warnings.Values.Distinct());}}
        public Task FlushAsync(){lock(gate)return worker;}
        public Task SaveAsync(AgentTaskState state)
        {
            var copy=state.Copy();lock(gate){worker=worker.ContinueWith(delegate{
                string temporary=null;string path=FilePath(copy.PersonaId,copy.Workspace);try{
                    lock(gate)if(unreadable.Contains(path)){Warn(path,"任务检查点无法读取，原文件已保留；请先清除任务记录后重试。");return;}
                    byte[] plain;using(var memory=new MemoryStream()){new DataContractJsonSerializer(typeof(AgentTaskState)).WriteObject(memory,copy);plain=memory.ToArray();}
                    if(plain.Length>8*1024*1024)throw new SerializationException();byte[] data=SecretProtection.Protect(plain);if(data.Length>8*1024*1024)throw new SerializationException();Directory.CreateDirectory(directory);temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
                    using(var stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None)){stream.Write(data,0,data.Length);stream.Flush(true);}if(File.Exists(path))File.Replace(temporary,path,null);else File.Move(temporary,path);temporary=null;Warn(path,"");
                }catch(Exception e){if(!StorageError(e))throw;Warn(path,"任务检查点尚未保存，请检查数据目录权限或空间；暂时不要退出或更新。");}
                finally{if(temporary!=null)try{File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}}
            },TaskScheduler.Default);return worker;}
        }
        public AgentTaskState Load(string persona,string workspace)
        {
            FlushAsync().GetAwaiter().GetResult();string path=FilePath(persona,workspace);if(!File.Exists(path))return null;
            try{
                if(new FileInfo(path).Length>8*1024*1024)throw new SerializationException();byte[] plain=SecretProtection.Unprotect(File.ReadAllBytes(path));if(plain.Length>8*1024*1024)throw new SerializationException();AgentTaskState state;
                using(var memory=new MemoryStream(plain))state=(AgentTaskState)new DataContractJsonSerializer(typeof(AgentTaskState)).ReadObject(memory);
                if(state==null || state.Version!=1 || state.PersonaId!=(persona??"") || !String.Equals(Path.GetFullPath(state.Workspace).TrimEnd('\\','/'),Path.GetFullPath(workspace).TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase) || state.Plan==null || state.Plan.Count>20 || state.Changes==null || state.Changes.Count>256 || state.Commands==null || state.Commands.Count>8 || state.Tools==null || state.Tools.Count>100 || state.Outputs==null || state.Outputs.Count>32)throw new SerializationException();
                if(state.Plan.Any(p=>p==null || p.Text==null || p.Text.Length>250 || (p.Status!="pending" && p.Status!="in_progress" && p.Status!="complete")) || state.Changes.Any(c=>c==null || String.IsNullOrEmpty(c.Path) || c.AfterHash==null) || state.Commands.Any(c=>c==null || c.Output==null || c.Output.Length>256*1024) || state.Tools.Any(t=>t==null || t.Id==null) || state.Outputs.Any(o=>o.Value==null || o.Value.Length>24000))throw new SerializationException();
                if(state.Status=="running"){state.Status="interrupted";state.Notice="上次任务在完成前中断；继续前会先核对项目现状。";foreach(var t in state.Tools.Where(t=>t.Status=="running"))t.Status="error";}
                Warn(path,"");return state;
            }catch(Exception e){if(!StorageError(e))throw;lock(gate)unreadable.Add(path);Warn(path,"任务检查点无法读取，原文件已保留。");return null;}
        }
        public Task ClearAsync(string persona,string workspace)
        {
            string path=FilePath(persona,workspace);lock(gate){worker=worker.ContinueWith(delegate{try{if(Directory.Exists(directory))File.Delete(path);lock(gate)unreadable.Remove(path);Warn(path,"");}catch(Exception e){if(!StorageError(e))throw;Warn(path,"任务记录删除失败，请检查数据目录权限。");}},TaskScheduler.Default);return worker;}
        }
        static bool StorageError(Exception e){return e is IOException || e is UnauthorizedAccessException || e is CryptographicException || e is SerializationException || e is ArgumentException || e is System.Xml.XmlException;}
    }
}
