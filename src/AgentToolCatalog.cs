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
            D("search_files","递归搜索 UTF-8 普通文本，忽略依赖与构建目录；最多检查 1500 项、12 层。",true,Fields("path",P("string","目录，默认 ."),"query",P("string","1～1000 字符的文本"),"pattern",P("string","可选文件 glob，例如 **/*.cs")),"query"),
            D("create_directory","创建目录及所需父目录。",false,Fields("path",P("string","目录路径")),"path"),
            D("open_directory","在资源管理器中打开现有目录。",false,Fields("path",P("string","已有目录")),"path"),
            D("find_files","按 glob 查找文件，例如 **/*.cs；跳过依赖与构建目录。使用 next_offset 分页。",true,Fields("path",P("string","目录，默认 ."),"pattern",P("string","路径模式"),"offset",P("integer","跳过匹配项"),"limit",P("integer","1～200 项，默认 100")),"pattern"),
            D("read_files","批量读取最多 8 个 UTF-8 文件；总输出有界，过长时单独分段读取。",true,Fields("paths",new{type="array",items=new{type="string"},maxItems=8},"offset",P("integer","每个文件跳过行数，默认 0"),"limit",P("integer","每个文件行数，默认 100")),"paths")
        };
        internal static bool IsReadOnly(string name){var tool=Definitions.FirstOrDefault(d=>d.Name==name);return tool!=null && tool.ReadOnly;}
        internal static void Schemas(bool plan,out object[] openai,out object[] anthropic)
        {
            var a=new List<object>();var b=new List<object>();
            foreach(var d in Definitions.Where(d=>!plan || d.ReadOnly)){
                var schema=new{type="object",properties=d.Properties,required=d.Required,additionalProperties=false};
                a.Add(new{type="function",function=new{name=d.Name,description=d.Description,parameters=schema}});
                b.Add(new{name=d.Name,description=d.Description,input_schema=schema});
            }
            openai=a.ToArray();anthropic=b.ToArray();
        }
    }
}
