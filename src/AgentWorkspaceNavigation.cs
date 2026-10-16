using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Dafeiyu
{
    // Navigation for the maximized window. All actions use the existing session
    // and settings rather than creating a second conversation or task model.
    internal sealed class AgentWorkspaceNavigation : Border, IDisposable
    {
        readonly PetWindow pet;
        readonly TextBlock project=Ui.Text("",15),path=Ui.Text("",11,Ui.Muted),mode=Ui.Text("",11,Ui.Muted),task=Ui.Text("",12),state=Ui.Text("",11,Ui.Muted);
        readonly Button taskButton;
        bool disposed;
        public AgentWorkspaceNavigation(PetWindow companion,Action focusConversation,Action focusTask)
        {
            pet=companion;Name="AgentWorkspaceSidebar";Background=Ui.Brush("#F7F8FA");BorderBrush=Ui.Line;BorderThickness=new Thickness(0,0,1,0);
            var body=new StackPanel{Margin=new Thickness(18,24,18,18)};Child=body;
            var title=Ui.Text("工作空间",17);title.FontWeight=FontWeights.SemiBold;title.Margin=new Thickness(0,0,0,28);body.Children.Add(title);
            body.Children.Add(Ui.Text("当前项目",11,Ui.Muted));project.Margin=new Thickness(0,8,0,5);body.Children.Add(project);path.Margin=new Thickness(0,0,0,12);path.TextWrapping=TextWrapping.NoWrap;path.TextTrimming=TextTrimming.CharacterEllipsis;body.Children.Add(path);
            var settings=Ui.Button("项目与命令设置");settings.Name="WorkspaceProjectSettings";settings.HorizontalContentAlignment=HorizontalAlignment.Left;settings.Click+=delegate{pet.ShowWorkSettings();};body.Children.Add(settings);
            mode.Margin=new Thickness(0,12,0,28);body.Children.Add(mode);
            var conversation=Ui.Button("对话");conversation.Name="WorkspaceConversation";conversation.HorizontalContentAlignment=HorizontalAlignment.Left;conversation.Click+=delegate{focusConversation();};body.Children.Add(conversation);
            body.Children.Add(new Separator{Margin=new Thickness(0,20,0,18)});body.Children.Add(Ui.Text("当前任务",11,Ui.Muted));
            task.Margin=new Thickness(0,10,0,6);task.MaxHeight=100;body.Children.Add(task);body.Children.Add(state);
            taskButton=Ui.Button("步骤、变更与验证");taskButton.Name="WorkspaceTaskDetails";taskButton.Margin=new Thickness(0,14,0,0);taskButton.HorizontalContentAlignment=HorizontalAlignment.Left;taskButton.Click+=delegate{focusTask();};body.Children.Add(taskButton);
            pet.Conversation.AgentTaskChanged+=Refresh;pet.Conversation.StateChanged+=Refresh;pet.Conversation.HistoryChanged+=Refresh;Refresh();
        }
        internal void Refresh()
        {
            if(disposed)return;if(!Dispatcher.CheckAccess()){Dispatcher.BeginInvoke(new Action(Refresh));return;}
            string root=pet.Settings.AgentWorkspace??"";project.Text=String.IsNullOrEmpty(root)?"尚未选择项目":Path.GetFileName(root.TrimEnd('\\','/'));if(String.IsNullOrEmpty(project.Text))project.Text=root;
            path.Text=root;path.ToolTip=root;mode.Text=(!pet.Settings.AgentEnabled?"对话模式 · 文件工具关闭":pet.Settings.AgentPlanMode?"计划模式 · 只读分析":"工作模式 · 可修改文件")+"\n"+(pet.Settings.AgentCommandsEnabled?"已启用项目命令":"未启用项目命令");
            var current=pet.Conversation.CurrentAgentTask;taskButton.IsEnabled=current!=null;
            task.Text=current==null?"选择工作模式后发送任务。":current.Goal;task.ToolTip=task.Text;
            state.Text=current==null?"任务记录将在这里显示。":current.Status=="running"?"正在处理":current.Status=="complete"?"已完成":current.Status=="cancelled"?"已停止，可继续":current.Status=="interrupted"?"已中断，可继续":"需要处理，可查看详情";
        }
        public void Dispose(){disposed=true;pet.Conversation.AgentTaskChanged-=Refresh;pet.Conversation.StateChanged-=Refresh;pet.Conversation.HistoryChanged-=Refresh;}
    }
}
