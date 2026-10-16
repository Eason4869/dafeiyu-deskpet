using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace Dafeiyu
{
    public sealed class WorkSettingsPanel : UserControl
    {
        const string BackupFolder=".deskpet-agent-backups";
        readonly PetWindow pet;
        readonly ComboBox access;
        readonly TextBox workspace,goal,turns,timeout;
        readonly CheckBox plan,backup,detailed,commandEnabled;
        readonly TextBox commandTimeout;readonly AgentCommandEditor commandEditor;
        readonly TextBlock notice,permissionDescription;
        PetSettings baseline;
        public bool HasUnsavedChanges
        {
            get
            {
                return baseline!=null && (access.SelectedIndex!=(baseline.AgentFullAccess?0:1) || workspace.Text!=(baseline.AgentWorkspace??"") || goal.Text!=(baseline.AgentGoal??"") ||
                    plan.IsChecked!=baseline.AgentPlanMode || backup.IsChecked!=baseline.AgentBackupFiles || detailed.IsChecked!=baseline.AgentDetailedProcess ||
                    commandEnabled.IsChecked!=baseline.AgentCommandsEnabled || commandTimeout.Text!=baseline.AgentCommandTimeoutSeconds.ToString(CultureInfo.InvariantCulture) || commandEditor.HasDraft || commandEditor.Json!=(baseline.AgentCommandRecipes??"[]") || turns.Text!=baseline.AgentMaxTurns.ToString(CultureInfo.InvariantCulture) || timeout.Text!=baseline.AgentTimeoutMinutes.ToString(CultureInfo.InvariantCulture));
            }
        }
        public WorkSettingsPanel(PetWindow companion)
        {
            if(companion==null)throw new ArgumentNullException("companion");pet=companion;Name="WorkSettingsPanel";
            var root=new StackPanel{Margin=new Thickness(24,22,24,24)};
            root.Children.Add(Ui.Text("工作设置",25));root.Children.Add(Ui.Text("这些设置在下一次请求生效；保存不会发起任务。文件工具仅在工作模式中启用。",12,Ui.Muted));
            var permissions=new StackPanel();permissions.Children.Add(Heading("权限与目录"));
            permissions.Children.Add(Label("文件访问权限"));
            access=new ComboBox{Name="AgentAccessMode",ItemsSource=new[]{"完整访问","仅工作目录"},MinHeight=36,FocusVisualStyle=null,Margin=new Thickness(0,0,0,7)};
            permissions.Children.Add(access);permissionDescription=Ui.Text("",12,Ui.Muted);permissions.Children.Add(permissionDescription);access.SelectionChanged+=delegate{RefreshPermissionDescription();};
            permissions.Children.Add(Label("默认工作目录"));
            workspace=Ui.Field();workspace.Name="AgentWorkspace";workspace.MaxLength=32767;permissions.Children.Add(workspace);
            var directoryButtons=new WrapPanel{Margin=new Thickness(0,8,0,5)};var change=Ui.Button("更改目录…");change.Name="ChangeAgentWorkspace";change.Click+=delegate{ChangeDirectory();};directoryButtons.Children.Add(change);
            var reset=Ui.Button("重置默认目录");reset.Name="ResetAgentWorkspace";reset.Click+=delegate{workspace.Text=DefaultWorkspace;notice.Text="默认目录已填入；保存后生效，首次执行任务时才会创建。";};directoryButtons.Children.Add(reset);permissions.Children.Add(directoryButtons);
            permissions.Children.Add(Ui.Text("相对工具路径以此目录为起点。完整访问可使用其他目录；仅工作目录会阻止越界路径与链接。",12,Ui.Muted));
            backup=Ui.Toggle("修改文件前自动备份",true,delegate{});backup.Name="AgentBackupFiles";permissions.Children.Add(backup);
            var open=Ui.Button("打开备份文件夹…");open.Name="OpenAgentBackups";open.HorizontalAlignment=HorizontalAlignment.Left;open.Click+=delegate{OpenBackups();};permissions.Children.Add(open);
            permissions.Children.Add(Ui.Text("完整访问的备份保存在原文件所在文件夹的 "+BackupFolder+"；仅工作目录的备份统一保存在工作目录下。此入口使用已保存的权限与目录。",11,Ui.Muted));
            root.Children.Add(Card(permissions));

            var tasks=new StackPanel();tasks.Children.Add(Heading("任务与过程"));
            plan=Ui.Toggle("计划模式：只读取与分析",false,delegate{});plan.Name="AgentPlanMode";tasks.Children.Add(plan);
            tasks.Children.Add(Ui.Text("计划模式不创建、修改文件，也不打开资源管理器。关闭后从下一次请求恢复可写权限。",12,Ui.Muted));
            tasks.Children.Add(Label("持续目标"));goal=Ui.Field("",true);goal.Name="AgentGoal";goal.MaxLength=4000;goal.MinHeight=100;goal.MaxHeight=180;tasks.Children.Add(goal);
            tasks.Children.Add(Ui.Text("最多 4000 个字符；留空可清除。与输入框 + 菜单中的目标和计划模式共用设置。",11,Ui.Muted));
            var limits=new Grid{Margin=new Thickness(0,12,0,8)};limits.ColumnDefinitions.Add(new ColumnDefinition());limits.ColumnDefinitions.Add(new ColumnDefinition());
            turns=Ui.Field();turns.Name="AgentMaxTurns";turns.MaxLength=10;timeout=Ui.Field();timeout.Name="AgentTimeoutMinutes";timeout.MaxLength=10;
            var turnGroup=new StackPanel{Margin=new Thickness(0,0,12,0)};turnGroup.Children.Add(Label("最多模型轮数（1～48）"));turnGroup.Children.Add(turns);limits.Children.Add(turnGroup);
            var timeoutGroup=new StackPanel();timeoutGroup.Children.Add(Label("任务时限（1～60 分钟）"));timeoutGroup.Children.Add(timeout);Grid.SetColumn(timeoutGroup,1);limits.Children.Add(timeoutGroup);tasks.Children.Add(limits);
            tasks.Children.Add(Ui.Text("一轮代表一次模型请求。多轮任务与附件按当前服务商规则计费；达到轮数或时限后停止后续操作，已完成的修改会保留。",12,Ui.Muted));
            detailed=Ui.Toggle("默认显示详细过程",false,delegate{});detailed.Name="AgentDetailedProcess";tasks.Children.Add(detailed);
            tasks.Children.Add(Ui.Text("展示模型实际返回的思考与工具记录；正式正文开始后仍自动隐藏过程，可在回复的更多菜单中重新查看。",12,Ui.Muted));root.Children.Add(Card(tasks));
            var commandCard=new StackPanel();commandCard.Children.Add(Heading("项目命令"));
            commandEnabled=Ui.Toggle("允许执行已配置的构建和测试命令",false,delegate{});commandEnabled.Name="AgentCommandsEnabled";commandCard.Children.Add(commandEnabled);
            commandCard.Children.Add(Ui.Text("命令绑定保存时选择的项目目录；使用当前 Windows 账户权限。工作目录不是系统沙箱。计划模式禁止执行命令。",12,Ui.Muted));
            commandCard.Children.Add(Label("单条命令超时（1～600 秒）"));commandTimeout=Ui.Field();commandTimeout.Name="AgentCommandTimeoutSeconds";commandCard.Children.Add(commandTimeout);
            commandEditor=new AgentCommandEditor(()=>workspace.Text);commandCard.Children.Add(commandEditor);root.Children.Add(Card(commandCard));
            var save=Ui.Button("保存工作设置",true);save.Name="SaveWorkSettings";save.HorizontalAlignment=HorizontalAlignment.Left;save.Click+=delegate{Save();};root.Children.Add(save);
            notice=Ui.Text("",12,Ui.Muted);notice.Name="WorkSettingsNotice";notice.Margin=new Thickness(0,10,0,0);root.Children.Add(notice);
            Content=new ScrollViewer{Content=root,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,HorizontalScrollBarVisibility=ScrollBarVisibility.Disabled};RefreshState();
        }
        static string DefaultWorkspace {get{return Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory,"data","agent-workspace"));}}
        static TextBlock Heading(string text){var value=Ui.Text(text,17);value.FontWeight=FontWeights.SemiBold;value.Margin=new Thickness(0,0,0,8);return value;}
        static TextBlock Label(string text){var value=Ui.Text(text,12,Ui.Muted);value.Margin=new Thickness(0,10,0,5);return value;}
        static Border Card(UIElement content){var card=Ui.Card(content);card.Background=System.Windows.Media.Brushes.White;card.Margin=new Thickness(0,16,0,0);return card;}
        void RefreshPermissionDescription()
        {
            if(permissionDescription==null)return;
            permissionDescription.Text=access.SelectedIndex==0?"使用当前 Windows 账户已有的文件权限，可访问桌面、用户目录与磁盘根目录；不会申请管理员权限或提权。":"文件工具仅访问所选工作目录。请选择具体项目目录；不接受磁盘根目录、整个用户目录或系统目录。";
        }
        public void RefreshState()
        {
            if(HasUnsavedChanges)return;
            baseline=pet.Settings.Copy();access.SelectedIndex=baseline.AgentFullAccess?0:1;workspace.Text=baseline.AgentWorkspace??"";goal.Text=baseline.AgentGoal??"";
            commandEnabled.IsChecked=baseline.AgentCommandsEnabled;commandTimeout.Text=baseline.AgentCommandTimeoutSeconds.ToString(CultureInfo.InvariantCulture);commandEditor.Load(baseline.AgentCommandRecipes);baseline.AgentCommandRecipes=commandEditor.Json;
            plan.IsChecked=baseline.AgentPlanMode;backup.IsChecked=baseline.AgentBackupFiles;detailed.IsChecked=baseline.AgentDetailedProcess;
            turns.Text=baseline.AgentMaxTurns.ToString(CultureInfo.InvariantCulture);timeout.Text=baseline.AgentTimeoutMinutes.ToString(CultureInfo.InvariantCulture);RefreshPermissionDescription();
        }
        public bool Save()
        {
            PetSettings candidate;
            try
            {
                if(access.SelectedIndex!=0 && access.SelectedIndex!=1)throw new InvalidOperationException("请选择完整访问或仅工作目录。");
                int maxTurns,maxMinutes;
                if(!Int32.TryParse(turns.Text,NumberStyles.None,CultureInfo.InvariantCulture,out maxTurns) || maxTurns<1 || maxTurns>48)throw new InvalidOperationException("最多模型轮数应为 1～48 的整数。");
                if(!Int32.TryParse(timeout.Text,NumberStyles.None,CultureInfo.InvariantCulture,out maxMinutes) || maxMinutes<1 || maxMinutes>60)throw new InvalidOperationException("任务时限应为 1～60 的整数分钟。");
                string target=ValidateDirectory(workspace.Text,access.SelectedIndex==1);
                if(goal.Text.Length>4000)throw new InvalidOperationException("持续目标不能超过 4000 个字符，请缩短后保存。");
                int commandSeconds;if(!Int32.TryParse(commandTimeout.Text,out commandSeconds) || commandSeconds<1 || commandSeconds>600)throw new InvalidOperationException("命令超时应为 1～600 的整数秒。");
                if(!commandEditor.CommitDraft())throw new InvalidOperationException("项目命令草稿无效，请检查命令名称、程序路径和参数。");
                candidate=pet.Settings.Copy();candidate.AgentCommandsEnabled=commandEnabled.IsChecked==true;candidate.AgentCommandRecipes=commandEditor.Json;candidate.AgentCommandTimeoutSeconds=commandSeconds;
                if(candidate.AgentCommandsEnabled){if(!Directory.Exists(target))throw new InvalidOperationException("执行命令前请选择已存在的项目目录。");new AgentWorkspace(target,access.SelectedIndex==1).ConfigureCommands(true,candidate.AgentCommandRecipes);}
                candidate.AgentFullAccess=access.SelectedIndex==0;candidate.AgentWorkspace=target;candidate.AgentPlanMode=plan.IsChecked==true;
                candidate.AgentGoal=goal.Text.Trim();candidate.AgentBackupFiles=backup.IsChecked==true;candidate.AgentMaxTurns=maxTurns;candidate.AgentTimeoutMinutes=maxMinutes;candidate.AgentDetailedProcess=detailed.IsChecked==true;
            }
            catch(Exception e){if(!ValidationError(e))throw;notice.Text=e.Message;return false;}
            var previous=pet.Settings.Copy();
            try
            {
                ApplyWorkFields(candidate,pet.Settings);pet.SaveSettings();
                if(!String.IsNullOrEmpty(pet.Notice)){ApplyWorkFields(previous,pet.Settings);notice.Text=pet.Notice;return false;}
                // Establish the clean baseline before refreshing other views, which may re-enter this panel.
                baseline=null;RefreshState();pet.RefreshChatAppearance();notice.Text="工作设置已保存，从下一次请求生效。";return true;
            }
            catch(Exception e){ApplyWorkFields(previous,pet.Settings);if(!ValidationError(e))throw;notice.Text="保存未完成："+e.Message;return false;}
        }
        static void ApplyWorkFields(PetSettings from,PetSettings to)
        {
            to.AgentCommandsEnabled=from.AgentCommandsEnabled;to.AgentCommandRecipes=from.AgentCommandRecipes;to.AgentCommandTimeoutSeconds=from.AgentCommandTimeoutSeconds;
            to.AgentFullAccess=from.AgentFullAccess;to.AgentWorkspace=from.AgentWorkspace;to.AgentPlanMode=from.AgentPlanMode;to.AgentGoal=from.AgentGoal;
            to.AgentBackupFiles=from.AgentBackupFiles;to.AgentMaxTurns=from.AgentMaxTurns;to.AgentTimeoutMinutes=from.AgentTimeoutMinutes;to.AgentDetailedProcess=from.AgentDetailedProcess;
        }
        static bool ValidationError(Exception e){return e is ArgumentException || e is InvalidOperationException || e is IOException || e is UnauthorizedAccessException || e is NotSupportedException || e is System.Security.SecurityException;}
        static string ValidateDirectory(string text,bool restricted)
        {
            text=(text??"").Trim();if(text.Length==0 || !Path.IsPathRooted(text))throw new InvalidOperationException("请输入已存在文件夹的完整绝对路径。");
            string path=Path.GetFullPath(text);
            if(!Directory.Exists(path))
            {
                if(String.Equals(path.TrimEnd('\\','/'),DefaultWorkspace.TrimEnd('\\','/'),StringComparison.OrdinalIgnoreCase))return DefaultWorkspace;
                throw new InvalidOperationException("默认工作目录不存在，请选择已存在的文件夹；重置的程序默认目录会在首次执行时创建。");
            }
            return new AgentWorkspace(path,restricted).Root;
        }
        void ChangeDirectory()
        {
            try{string selected=PickFolder("选择默认工作目录",workspace.Text);if(selected!=null){workspace.Text=ValidateDirectory(selected,access.SelectedIndex==1);notice.Text="目录已更改，点击保存工作设置后生效。";}}
            catch(Exception e){if(!ValidationError(e))throw;notice.Text=e.Message;}
        }
        string PickFolder(string description,string initial)
        {
            using(var picker=new System.Windows.Forms.FolderBrowserDialog{Description=description,SelectedPath=Directory.Exists(initial)?initial:"",ShowNewFolderButton=false})
            {
                var window=Window.GetWindow(this);var result=window==null?picker.ShowDialog():picker.ShowDialog(new FolderOwner(window));
                return result==System.Windows.Forms.DialogResult.OK?picker.SelectedPath:null;
            }
        }
        void OpenBackups()
        {
            try
            {
                var settings=pet.Settings.Copy();string parent;
                if(settings.AgentFullAccess){parent=PickFolder("选择原文件所在的文件夹，以查看其中的自动备份",settings.AgentWorkspace);if(parent==null)return;}
                else parent=ValidateDirectory(settings.AgentWorkspace,true);
                string path=Path.Combine(parent,BackupFolder);
                if(!Directory.Exists(path)){notice.Text="此文件夹尚无自动备份："+path;return;}
                Process.Start(new ProcessStartInfo("explorer.exe","\""+path+"\""){UseShellExecute=true});notice.Text="已打开备份文件夹。";
            }
            catch(Exception e){if(!(ValidationError(e) || e is System.ComponentModel.Win32Exception))throw;notice.Text="无法打开备份文件夹："+e.Message;}
        }
        sealed class FolderOwner:System.Windows.Forms.IWin32Window
        {
            readonly IntPtr handle;public FolderOwner(Window window){handle=new WindowInteropHelper(window).Handle;}public IntPtr Handle{get{return handle;}}
        }
    }
}
