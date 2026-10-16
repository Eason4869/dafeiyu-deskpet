using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Web.Script.Serialization;
namespace Dafeiyu
{
    internal static class AgentContextBudget
    {
        // Replace only completed tool result payloads. IDs, tool calls and all
        // thinking/signature blocks stay intact in their original protocol order.
        internal static int Compact(List<object> history,bool anthropic)
        {
            int retained=0;
            for(int i=0;i<history.Count;i++){
                var message=Object(history[i]);string role=Text(message,"role");
                if(!anthropic && role=="tool"){
                    string output=Text(message,"content");if(i<history.Count-2 && output.Length>1500){message["content"]=Preview(output,Text(message,"tool_call_id"));history[i]=message;}
                    retained+=Text(message,"content").Length;
                }
                if(anthropic && role=="user"){
                    object raw;if(!message.TryGetValue("content",out raw) || raw is string)continue;var array=raw as System.Collections.IEnumerable;if(array==null)continue;var blocks=new List<object>();bool changed=false;
                    foreach(object item in array){var block=Object(item);if(Text(block,"type")=="tool_result"){
                        string output=Text(block,"content");if(i<history.Count-1 && output.Length>1500){block["content"]=Preview(output,Text(block,"tool_use_id"));changed=true;}retained+=Text(block,"content").Length;
                    }blocks.Add(block);}if(changed){message["content"]=blocks;history[i]=message;}
                }
            }
            return retained;
        }
        static string Preview(string text,string id){return text.Substring(0,1000)+"\n[较早工具输出已压缩；完整有界输出可通过 read_output 的 output_id="+id+" 按需读取]";}
        static string Text(Dictionary<string,object> obj,string key){object value;return obj.TryGetValue(key,out value)?value as string??"":"";}
        static Dictionary<string,object> Object(object value)
        {
            var map=value as Dictionary<string,object>;if(map!=null)return map;map=new Dictionary<string,object>();if(value==null)return map;foreach(var p in value.GetType().GetProperties())map[p.Name]=p.GetValue(value,null);return map;
        }
    }
}
