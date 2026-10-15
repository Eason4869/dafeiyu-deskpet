using System;
using System.Windows;
using System.Windows.Controls;
namespace Dafeiyu
{
    public sealed class PersonaPanel : Grid
    {
        readonly PetWindow pet;readonly TextBox prompt;readonly TextBlock title,description,notice;
        public bool HasUnsavedChanges {get{return !String.Equals(prompt.Text,pet.Settings.SystemPrompt,StringComparison.Ordinal);}}
        public PersonaPanel(PetWindow companion,bool compact=false)
        {
            Name="GuidePersonaSection";pet=companion;Margin=new Thickness(24,22,24,20);RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});RowDefinitions.Add(new RowDefinition());RowDefinitions.Add(new RowDefinition{Height=GridLength.Auto});
            var header=new StackPanel();title=Ui.Text("",compact?18:25);title.FontWeight=FontWeights.SemiBold;header.Children.Add(title);description=Ui.Text("",12,Ui.Muted);header.Children.Add(description);if(compact)description.Visibility=Visibility.Collapsed;header.Children.Add(Ui.Text("每位伙伴都有自己的预设。修改只对当前伙伴生效，切换时会同时切换人格与对话记录。",12,Ui.Muted));header.Margin=new Thickness(0,0,0,compact?10:18);Children.Add(header);
            prompt=Ui.Field("",true);prompt.Name="PersonaPrompt";prompt.MaxLength=8000;prompt.MinHeight=120;Grid.SetRow(prompt,1);Children.Add(prompt);
            var footer=new StackPanel{Margin=new Thickness(0,16,0,0)};var row=new WrapPanel();var save=Ui.Button("保存当前人格",true);save.Name="SavePersona";save.Click+=delegate {Change(false);};row.Children.Add(save);var reset=Ui.Button("恢复模板预设");reset.Name="ResetPersona";reset.Click+=delegate {Change(true);};row.Children.Add(reset);footer.Children.Add(row);notice=Ui.Text("{pet_name} 会替换为当前伙伴名。人格不会授予桌面或文件访问权限。",11,Ui.Muted);notice.Margin=new Thickness(0,12,0,0);footer.Children.Add(notice);Grid.SetRow(footer,2);Children.Add(footer);RefreshPersona();
        }
        public void RefreshPersona(){title.Text=pet.Skin.Name+"的人格";description.Text=pet.Skin.Description;prompt.Text=pet.Settings.SystemPrompt;}
        void Change(bool reset)
        {
            try{if(pet.DialogueBusy)throw new InvalidOperationException("请先停止当前对话再修改人格。");if(reset)PersonaProfiles.Reset(pet.Settings,pet.Skin);else PersonaProfiles.Save(pet.Settings,pet.Skin,prompt.Text);pet.Conversation.Clear();pet.SaveSettings();pet.ReloadConversation();RefreshPersona();notice.Text=String.IsNullOrEmpty(pet.Notice)?"已保存，当前伙伴的新会话将使用此人格。":pet.Notice;}
            catch(InvalidOperationException e){notice.Text=e.Message;}
        }
    }
}
