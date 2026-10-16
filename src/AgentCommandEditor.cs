using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
namespace Dafeiyu
{
    // A small native editor inside existing work settings; recipes stay in the
    // user's settings, never loaded as authority from an untrusted repository.
    internal sealed class AgentCommandEditor : StackPanel
    {
        readonly ListBox list=new ListBox{MinHeight=60,MaxHeight=140,Margin=new Thickness(0,4,0,8)};
        readonly TextBox id=Ui.Field(),executable=Ui.Field(),arguments=Ui.Field("",true);
        readonly List<AgentCommand> recipes=new List<AgentCommand>();readonly Func<string> workspace;readonly TextBlock notice=Ui.Text("",11,Ui.Muted);
        internal string Json{get{return new JavaScriptSerializer{MaxJsonLength=65536}.Serialize(recipes);}}
        internal AgentCommandEditor(Func<string> project)
        {
            workspace=project;Name="AgentCommandEditor";Children.Add(list);
            id.Name="CommandRecipeId";executable.Name="CommandRecipeExecutable";arguments.Name="CommandRecipeArguments";arguments.MinHeight=65;arguments.MaxHeight=120;
            Children.Add(Ui.Text("命令名称",12,Ui.Muted));Children.Add(id);
            Children.Add(Ui.Text("程序完整路径",12,Ui.Muted));Children.Add(executable);
            var browse=Ui.Button("选择程序…");browse.HorizontalAlignment=HorizontalAlignment.Left;browse.Click+=delegate{var picker=new Microsoft.Win32.OpenFileDialog{Filter="可执行程序 (*.exe)|*.exe",CheckFileExists=true};if(picker.ShowDialog(Window.GetWindow(this))==true)executable.Text=picker.FileName;};Children.Add(browse);
            Children.Add(Ui.Text("参数（每行一个；无需额外加引号）",12,Ui.Muted));Children.Add(arguments);
            var buttons=new WrapPanel{Margin=new Thickness(0,8,0,0)};
            var add=Ui.Button("保存此命令");add.Name="SaveCommandRecipe";add.Click+=delegate{SaveRecipe();};buttons.Children.Add(add);
            var remove=Ui.Button("移除此命令");remove.Click+=delegate{var current=list.SelectedItem as AgentCommand;if(current!=null){recipes.Remove(current);Refresh();id.Clear();executable.Clear();arguments.Clear();}};buttons.Children.Add(remove);Children.Add(buttons);Children.Add(notice);
            list.SelectionChanged+=delegate{var current=list.SelectedItem as AgentCommand;if(current==null)return;id.Text=current.Id;executable.Text=current.Executable;arguments.Text=String.Join(Environment.NewLine,current.Arguments??new string[0]);};
        }
        internal void Load(string json)
        {
            recipes.Clear();try{recipes.AddRange(new JavaScriptSerializer{MaxJsonLength=65536}.Deserialize<AgentCommand[]>(String.IsNullOrWhiteSpace(json)?"[]":json)??new AgentCommand[0]);}catch(ArgumentException){notice.Text="命令配置无法读取，请重新配置后保存。";}catch(InvalidOperationException){notice.Text="命令配置无法读取，请重新配置后保存。";}Refresh();
        }
        void Refresh(){list.ItemsSource=null;list.ItemsSource=recipes;list.DisplayMemberPath="Id";}
        void SaveRecipe()
        {
            string name=id.Text.Trim(),exe=executable.Text.Trim(),root=(workspace()??"").Trim();
            if(name.Length==0 || name.Length>80 || !Path.IsPathRooted(exe) || !File.Exists(exe) || !Path.IsPathRooted(root) || !Directory.Exists(root)){notice.Text="请填写命令名称、已存在的程序完整路径，并选择已存在的项目目录。";return;}
            var old=list.SelectedItem as AgentCommand;if(recipes.Any(r=>r!=old && r.Id==name)){notice.Text="命令名称已存在，请使用不同名称。";return;}
            if(old==null && recipes.Count>=32){notice.Text="最多配置 32 条命令。";return;}
            var recipe=new AgentCommand{Id=name,Executable=Path.GetFullPath(exe),Workspace=Path.GetFullPath(root),Arguments=arguments.Text.Split(new[]{"\r\n","\n"},StringSplitOptions.None)};
            if(recipe.Arguments.Length==1 && recipe.Arguments[0].Length==0)recipe.Arguments=new string[0];
            if(old!=null)recipes[recipes.IndexOf(old)]=recipe;else recipes.Add(recipe);Refresh();list.SelectedItem=recipe;notice.Text="命令已加入草稿；点击保存工作设置后生效。";
        }
    }
}
