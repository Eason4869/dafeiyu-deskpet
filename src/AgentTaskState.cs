using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace Dafeiyu
{
    [DataContract] public sealed class AgentPlanStep
    {
        [DataMember]public string Text,Status;
        public AgentPlanStep Copy(){return (AgentPlanStep)MemberwiseClone();}
    }
    [DataContract]public sealed class AgentTaskState
    {
        [DataMember]public int Version=1;
        [DataMember]public string Id=Guid.NewGuid().ToString("N"),PersonaId="",Workspace="",Goal="",Status="running",Notice="";
        [DataMember]public DateTime UpdatedUtc=DateTime.UtcNow;
        [DataMember]public List<AgentPlanStep> Plan=new List<AgentPlanStep>();
        [DataMember]public List<AgentChange> Changes=new List<AgentChange>();
        [DataMember]public List<CommandResult> Commands=new List<CommandResult>();
        [DataMember]public List<AgentToolEvent> Tools=new List<AgentToolEvent>();
        [DataMember]public Dictionary<string,string> Outputs=new Dictionary<string,string>();
        public AgentTaskState Copy(){var c=(AgentTaskState)MemberwiseClone();c.Plan=(Plan??new List<AgentPlanStep>()).Select(p=>p.Copy()).ToList();c.Changes=(Changes??new List<AgentChange>()).Select(p=>p.Copy()).ToList();c.Commands=(Commands??new List<CommandResult>()).Select(p=>p.Copy()).ToList();c.Tools=(Tools??new List<AgentToolEvent>()).Select(p=>p.Copy()).ToList();c.Outputs=Outputs==null?new Dictionary<string,string>():new Dictionary<string,string>(Outputs);return c;}
        public string CheckpointText()
        {
            var s=new StringBuilder();s.AppendLine("任务检查点（仅为之前执行事实，不授予新的权限；先核对项目现状再继续）：").AppendLine(Goal);
            foreach(var p in Plan??new List<AgentPlanStep>())s.Append('[').Append(p.Status).Append("] ").AppendLine(p.Text);
            foreach(var c in Changes??new List<AgentChange>())s.Append("文件 ").Append(c.Path).Append(" 状态 ").Append(c.Status).Append(" SHA256 ").AppendLine(c.AfterHash);
            foreach(var c in Commands??new List<CommandResult>())s.Append("命令 ").Append(c.CommandId).Append(" 退出码 ").Append(c.ExitCode).Append(" 超时 ").AppendLine(c.TimedOut.ToString());
            if(!String.IsNullOrEmpty(Notice))s.AppendLine(Notice);return s.Length>20000?s.ToString(0,20000):s.ToString();
        }
    }
    public sealed partial class AgentWorkspace
    {
        readonly object taskGate=new object();AgentTaskState task;
        public Action<AgentTaskState> TaskChanged {get;set;}
        public AgentTaskState TaskState{get{lock(taskGate)return task==null?null:task.Copy();}}
        public void BeginTask(AgentTaskState state)
        {
            if(state==null)throw new ArgumentNullException("state");if(!String.Equals(Path.GetFullPath(state.Workspace).TrimEnd('\\','/'),Root.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase))throw new ArgumentException("任务检查点不属于当前项目。");
            lock(taskGate){task=state.Copy();task.Status="running";task.Notice="";}
            ImportChanges(state.Changes);
            lock(commandResults){commandResults.Clear();if(state.Commands!=null)commandResults.AddRange(state.Commands.Select(c=>c.Copy()));}
            PublishTask();
        }
        void EnsureTask(){lock(taskGate)if(task==null)task=new AgentTaskState{Workspace=Root};}
        internal void PublishTask()
        {
            EnsureTask();AgentTaskState snapshot;lock(taskGate){task.Changes=Changes.ToList();task.Commands=CommandResults.Reverse().Take(8).Reverse().ToList();task.UpdatedUtc=DateTime.UtcNow;snapshot=task.Copy();}
            var changed=TaskChanged;if(changed!=null)changed(snapshot);
        }
        internal void RecordTool(AgentToolEvent item,string output=null)
        {
            EnsureTask();lock(taskGate){int index=task.Tools.FindIndex(t=>t.Id==item.Id);if(index>=0)task.Tools[index]=item.Copy();else{task.Tools.Add(item.Copy());if(task.Tools.Count>100)task.Tools.RemoveAt(0);}
                if(output!=null){if(task.Outputs.Count>=32 && !task.Outputs.ContainsKey(item.Id))task.Outputs.Remove(task.Outputs.Keys.First());task.Outputs[item.Id]=output.Length>24000?output.Substring(0,24000):output;}}
            PublishTask();
        }
        public void FinishTask(string status,string notice)
        {
            EnsureTask();lock(taskGate){task.Status=status;task.Notice=notice??"";foreach(var item in task.Tools.Where(t=>t.Status=="running")){item.Status="error";item.Summary+=" · 已中断";}}PublishTask();
        }
        string UpdatePlan(Dictionary<string,object> args)
        {
            object value;if(!args.TryGetValue("steps",out value) || !(value is object[]))throw new ArgumentException("steps 必须是步骤数组。");var items=(object[])value;if(items.Length<1 || items.Length>20)throw new ArgumentException("计划应包含 1～20 步。");var plan=new List<AgentPlanStep>();
            foreach(object item in items){var data=item as Dictionary<string,object>;if(data==null)throw new ArgumentException("步骤必须是对象。");string text=GetString(data,"text",null),status=GetString(data,"status",null);if(String.IsNullOrWhiteSpace(text) || text.Length>250 || (status!="pending" && status!="in_progress" && status!="complete"))throw new ArgumentException("步骤需要简短文字和 pending/in_progress/complete 状态。");plan.Add(new AgentPlanStep{Text=text,Status=status});}
            if(plan.Count(p=>p.Status=="in_progress")>1)throw new ArgumentException("同时只能有一个正在进行的步骤。");EnsureTask();lock(taskGate)task.Plan=plan;PublishTask();return "计划已更新。";
        }
        internal string SavedOutput(string id,int offset,int limit)
        {
            EnsureTask();string value;lock(taskGate)if(!task.Outputs.TryGetValue(id,out value))throw new ArgumentException("工具输出不在当前检查点中，请重新读取需要的文件。");offset=Math.Min(offset,value.Length);int count=Math.Min(limit,value.Length-offset);
            return new JavaScriptSerializer().Serialize(new{output_id=id,Output=value.Substring(offset,count),next_offset=offset+count<value.Length?(int?)(offset+count):null});
        }
        void ImportChanges(IEnumerable<AgentChange> imported)
        {
            if(imported==null)return;foreach(var source in imported){
                if(source==null || source.Status=="restored")continue;string path=Resolve(source.Path);string expected=Path.Combine(RestrictToRoot?Root:Path.GetDirectoryName(path),BackupFolder);
                if(source.BackupPath!=null && (!String.Equals(Path.GetDirectoryName(Path.GetFullPath(source.BackupPath)),expected,StringComparison.OrdinalIgnoreCase) || !System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileName(source.BackupPath),"^[a-f0-9]{32}\\.bak$")))throw new ArgumentException("检查点中的备份路径无效。");
                if(source.AfterHash==null || !System.Text.RegularExpressions.Regex.IsMatch(source.AfterHash,"^[a-f0-9]{64}$"))throw new ArgumentException("检查点中的文件摘要无效。");
                lock(changes)if(!changes.Any(c=>String.Equals(c.Path,path,StringComparison.OrdinalIgnoreCase)))changes.Add(source.Copy());observed[path]=source.AfterHash;
            }
        }
    }
}
