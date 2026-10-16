using System;
using System.Collections.Generic;
using System.Linq;
namespace Dafeiyu
{
    internal sealed class AgentToolDefinition
    {
        internal string Name,Description;internal bool ReadOnly;internal Dictionary<string,object> Properties;internal string[] Required;
    }
    // Both protocol schemas and execution's plan-mode policy use this catalogue.
    internal static class AgentToolCatalog
    {
        static object P(string type,string description){return new{type=type,description=description};}
        static Dictionary<string,object> Fields(params object[] fields){var d=new Dictionary<string,object>();for(int i=0;i<fields.Length;i+=2)d[(string)fields[i]]=fields[i+1];return d;}
        static AgentToolDefinition D(string name,string description,bool readOnly,Dictionary<string,object> properties,params string[] required){return new AgentToolDefinition{Name=name,Description=description,ReadOnly=readOnly,Properties=properties,Required=required};}
        internal static readonly AgentToolDefinition[] Definitions={
            D("list_files","分页列出目录，稳定排序。offset 从 0 开始，limit 默认 200；截断时使用 next_offset 继续。",true,Fields("path",P("string","目录，默认 ."),"offset",P("integer","跳过项目数"),"limit",P("integer","1～200 项"))),
            D("read_file","读取 UTF-8 文件，最多 1 MiB；返回行号。offset 从 0 开始，limit 默认 200。",true,Fields("path",P("string","文件路径"),"offset",P("integer","跳过行数"),"limit",P("integer","1～1000 行")),"path"),
            D("write_file","创建或完整覆盖 UTF-8 文件；保存原文件备份，拒绝覆盖读取后已变化的文件。",false,Fields("path",P("string","文件路径"),"content",P("string","完整文件内容")),"path","content"),
            D("edit_file","将唯一匹配的 old_text 替换为 new_text；先读取文件，拒绝外部修改冲突。",false,Fields("path",P("string","文件路径"),"old_text",P("string","唯一匹配的旧文本"),"new_text",P("string","替换文本")),"path","old_text","new_text"),
            D("search_files","递归搜索 UTF-8 普通文本，忽略依赖与构建目录；最多检查 1500 项、12 层。",true,Fields("path",P("string","目录，默认 ."),"query",P("string","1～1000 字符的文本或正则"),"regex",P("boolean","是否正则搜索，默认 false，每次匹配限时 100 毫秒"),"pattern",P("string","可选文件 glob，例如 **/*.cs")),"query"),
            D("create_directory","创建目录及所需父目录。",false,Fields("path",P("string","目录路径")),"path"),
            D("open_directory","在资源管理器中打开现有目录。",false,Fields("path",P("string","已有目录")),"path"),
            D("find_files","按 glob 查找文件，例如 **/*.cs；跳过依赖与构建目录。使用 next_offset 分页。",true,Fields("path",P("string","目录，默认 ."),"pattern",P("string","路径模式"),"offset",P("integer","跳过匹配项"),"limit",P("integer","1～200 项，默认 100")),"pattern"),
            D("read_files","批量读取最多 8 个 UTF-8 文件；总输出有界，过长时单独分段读取。",true,Fields("paths",new{type="array",items=new{type="string"},maxItems=8},"offset",P("integer","每个文件跳过行数，默认 0"),"limit",P("integer","每个文件行数，默认 100")),"paths"),
            D("apply_patch","预检后精确修改多个文件，最多 20 个。已有文件提供 path/old_text/new_text，新建文件提供 path/content。",false,Fields("edits",new{type="array",items=new{type="object"},maxItems=20}),"edits"),
            D("list_changes","列出本任务实际文件变更、原文件备份与恢复状态。",true,Fields()),
            D("restore_file","从本任务原文件备份恢复；文件又被外部修改时拒绝。",false,Fields("path",P("string","需恢复的文件路径")),"path"),
            D("update_plan","更新任务步骤与进度，最多 20 步；步骤状态为 pending/in_progress/complete，同时最多一步进行中。",true,Fields("steps",new{type="array",items=new{type="object"},maxItems=20}),"steps"),
            D("list_commands","列出用户为当前项目配置并授权的命令 ID。",true,Fields()),
            D("run_command","执行已授权的项目命令 ID；不能提供自由命令或更改参数，返回实际退出码与输出。",false,Fields("command_id",P("string","来自 list_commands 的命令 ID")),"command_id"),
            D("read_output","分页读取本任务已执行命令的有界输出。",true,Fields("command_id",P("string","命令 ID"),"output_id",P("string","历史工具输出 ID；与 command_id 二选一"),"offset",P("integer","字符偏移，默认 0"),"limit",P("integer","1～12000 字符")))
        };
        internal static bool IsReadOnly(string name){var tool=Definitions.FirstOrDefault(d=>d.Name==name);return tool!=null && tool.ReadOnly;}
        internal static void Schemas(bool plan,bool commands,out object[] openai,out object[] anthropic)
        {
            var a=new List<object>();var b=new List<object>();
            foreach(var d in Definitions.Where(d=>(!plan || d.ReadOnly) && (commands || (d.Name!="run_command" && d.Name!="list_commands")))){
                var schema=new{type="object",properties=d.Properties,required=d.Required,additionalProperties=false};
                a.Add(new{type="function",function=new{name=d.Name,description=d.Description,parameters=schema}});
                b.Add(new{name=d.Name,description=d.Description,input_schema=schema});
            }
            openai=a.ToArray();anthropic=b.ToArray();
        }
    }
}
