using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Web.Script.Serialization;
namespace Dafeiyu.Tests
{
    public static class AgentTaskTests
    {
        static int checks;static void Check(bool ok,string name){if(!ok)throw new Exception(name);checks++;Console.WriteLine("PASS "+name);}
        static string Call(AgentWorkspace w,string name,object args){return w.ExecuteAsync(name,new JavaScriptSerializer().Serialize(args),CancellationToken.None).GetAwaiter().GetResult();}
        public static int Main(){try{
            string root=Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"tasks-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(root);var w=new AgentWorkspace(root);
            string result=Call(w,"update_plan",new{steps=new[]{new{text="Read project",status="complete"},new{text="Implement",status="in_progress"}}});Check(!result.StartsWith("错误："),"plan is recorded through the real tool dispatcher");
            var state=typeof(AgentWorkspace).GetProperty("TaskState");Check(state!=null,"workspace exposes actual task state");object task=state.GetValue(w,null);Type taskType=task.GetType();
            Check(new JavaScriptSerializer().Serialize(task).Contains("Read project"),"task snapshot includes plan steps");
            Type storeType=typeof(AgentWorkspace).Assembly.GetType("Dafeiyu.AgentTaskStore");Check(storeType!=null,"encrypted task store is available");
            string directory=Path.Combine(root,"task-store");object store=Activator.CreateInstance(storeType,new object[]{directory});
            storeType.GetMethod("SaveAsync").Invoke(store,new[]{task});((System.Threading.Tasks.Task)storeType.GetMethod("FlushAsync").Invoke(store,null)).GetAwaiter().GetResult();
            var file=Directory.GetFiles(directory).Single();Check(!Encoding.UTF8.GetString(File.ReadAllBytes(file)).Contains("Read project"),"task checkpoint is encrypted on disk");
            object saved=storeType.GetMethod("Load").Invoke(store,new object[]{"",root});Check(saved!=null && new JavaScriptSerializer().Serialize(saved).Contains("Read project"),"checkpoint restores the real plan");
            Check(storeType.GetMethod("Load").Invoke(store,new object[]{"other-persona",root})==null,"persona cannot load another persona checkpoint");
            w.ReadOnly=true;Check(!Call(w,"update_plan",new{steps=new[]{new{text="Read only",status="pending"}}}).StartsWith("错误："),"planning can record task progress without modifying project files");
            Check(Call(w,"update_plan",new{steps=new[]{new{text="bad",status="invented"}}}).StartsWith("错误："),"invalid plan statuses are rejected");
            Console.WriteLine("Agent task tests: "+checks+" passed");return 0;
        }catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}}
    }
}
