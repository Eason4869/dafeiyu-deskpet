using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;

namespace Dafeiyu
{
    // A private draft per provider keeps unfinished edits out of the running conversation.
    public sealed class TextProviderEditor : Grid
    {
        static readonly Style itemStyle=(Style)XamlReader.Parse("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ListBoxItem'><Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ListBoxItem'><Border x:Name='surface' Background='Transparent' CornerRadius='7' Padding='{TemplateBinding Padding}' BorderThickness='1' BorderBrush='Transparent'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='surface' Property='Background' Value='#E8F0FF'/><Setter TargetName='surface' Property='BorderBrush' Value='#D1E0FF'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='surface' Property='Background' Value='#EFF5FF'/></Trigger><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='surface' Property='BorderBrush' Value='#456DE6'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
        readonly List<TextProviderSettings> drafts=new List<TextProviderSettings>();
        readonly ListBox providers,models;
        readonly ContentControl connectionHost=new ContentControl();
        readonly TextBox providerName,newModel,modelDisplayName;
        PetSettings baseline;
        readonly TextBlock feedback;
        readonly Button removeProvider;
        ConnectionSettingsPanel.ConnectionEditor connection;
        TextProviderSettings selected;
        bool updating;
        public event Action Changed;
        public string SelectedProviderId {get{return selected.Id;}}
        public TextProviderEditor(PetSettings settings)
        {
            settings.EnsureTextProviders();baseline=settings.Copy();foreach(var provider in settings.TextProviders)drafts.Add(provider.Copy());
            ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(138)});
            ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            var rail=new StackPanel();var heading=Ui.Text("服务商",15);heading.FontWeight=FontWeights.SemiBold;rail.Children.Add(heading);
            rail.Children.Add(Ui.Text("独立地址与密钥",10,Ui.Muted));
            providers=new ListBox{Name="TextProviderList",BorderThickness=new Thickness(0),Background=Brushes.Transparent,Margin=new Thickness(0,12,0,10),MinHeight=76,MaxHeight=340,HorizontalContentAlignment=HorizontalAlignment.Stretch};
            ScrollViewer.SetHorizontalScrollBarVisibility(providers,ScrollBarVisibility.Disabled);rail.Children.Add(providers);
            var add=Ui.Button("＋ 添加服务商");add.Name="AddTextProvider";add.FontSize=11;add.Margin=new Thickness(0,0,0,7);add.Padding=new Thickness(6);add.Click+=delegate {AddProvider();};rail.Children.Add(add);
            removeProvider=Ui.Button("移除服务商");removeProvider.Name="RemoveTextProvider";removeProvider.FontSize=11;removeProvider.Margin=new Thickness(0);removeProvider.Padding=new Thickness(6);removeProvider.Click+=delegate {RemoveProvider();};rail.Children.Add(removeProvider);
            rail.Children.Add(Ui.Text("保存后使用当前服务商与模型。",10,Ui.Muted));
            var railCard=Glass(rail,10);railCard.Margin=new Thickness(0,0,12,0);railCard.VerticalAlignment=VerticalAlignment.Top;Children.Add(railCard);
            var detail=new StackPanel();providerName=Ui.Field();providerName.Name="TextProviderName";providerName.MaxLength=80;Ui.Label(detail,"服务商名称",providerName);
            var modelHeading=Ui.Text("模型列表",13);modelHeading.FontWeight=FontWeights.SemiBold;modelHeading.Margin=new Thickness(0,18,0,4);detail.Children.Add(modelHeading);
            detail.Children.Add(Ui.Text("可为模型起一个中文名；点击列表项编辑。",11,Ui.Muted));
            models=new ListBox{Name="TextProviderModels",MinHeight=38,MaxHeight=134,Background=Ui.Brush("#F7FAFF"),BorderBrush=Ui.Line,BorderThickness=new Thickness(1),Margin=new Thickness(0,8,0,8),SelectedValuePath="Tag"};ScrollViewer.SetHorizontalScrollBarVisibility(models,ScrollBarVisibility.Disabled);detail.Children.Add(models);
            Ui.Label(detail,"模型 ID",newModel=Ui.Field());newModel.Name="TextProviderNewModel";newModel.MaxLength=256;newModel.ToolTip="请求时使用的完整模型 ID";
            var modelRow=new Grid();modelRow.ColumnDefinitions.Add(new ColumnDefinition());modelRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            modelDisplayName=Ui.Field();modelDisplayName.Name="TextModelDisplayName";modelDisplayName.MaxLength=80;modelDisplayName.ToolTip="例如：日常聊天、深度思考；留空则显示模型 ID";modelRow.Children.Add(modelDisplayName);
            var addModel=Ui.Button("添加 / 更新");addModel.Name="AddTextModel";addModel.Margin=new Thickness(6,0,0,0);addModel.Padding=new Thickness(10,4,10,4);addModel.Click+=delegate {AddModel();};Grid.SetColumn(addModel,1);modelRow.Children.Add(addModel);Ui.Label(detail,"显示名称（可填写中文）",modelRow);
            var removeModel=Ui.Button("移除选中模型");removeModel.Name="RemoveTextModel";removeModel.HorizontalAlignment=HorizontalAlignment.Left;removeModel.FontSize=11;removeModel.MinHeight=28;removeModel.Margin=new Thickness(0,6,0,0);removeModel.Click+=delegate {RemoveModel();};detail.Children.Add(removeModel);
            feedback=Ui.Text("",11,Ui.Blue);feedback.Name="TextProviderNotice";detail.Children.Add(feedback);
            connectionHost.Margin=new Thickness(0,18,0,0);detail.Children.Add(connectionHost);
            var detailCard=Glass(detail,14);Grid.SetColumn(detailCard,1);Children.Add(detailCard);
            providers.SelectionChanged+=delegate {if(updating)return;var item=providers.SelectedItem as ListBoxItem;if(item==null)return;Capture();Show((TextProviderSettings)item.Tag);RaiseChanged();};
            models.SelectionChanged+=delegate {if(updating || connection==null)return;var item=models.SelectedItem as ListBoxItem;string value=item==null?null:item.Tag as string;if(value!=null){connection.SetModel(value);selected.Connection.Model=value;updating=true;newModel.Text=value;modelDisplayName.Text=selected.ModelDisplayNames.ContainsKey(value)?selected.ModelDisplayNames[value]:"";updating=false;RaiseChanged();}};
            newModel.TextChanged+=delegate {
                if(updating || selected==null || connection==null)return;
                updating=true;connection.SetModel(newModel.Text);string id=newModel.Text.Trim();modelDisplayName.Text=selected.ModelDisplayNames.ContainsKey(id)?selected.ModelDisplayNames[id]:"";updating=false;RaiseChanged();
            };
            modelDisplayName.TextChanged+=delegate {if(updating || selected==null)return;string id=newModel.Text.Trim();if(id.Length>0){SetDisplayName(id);RefreshModelLabels();RaiseChanged();}};
            providerName.TextChanged+=delegate {if(updating || selected==null)return;selected.Name=providerName.Text;foreach(ListBoxItem item in providers.Items)if(item.Tag==selected){((TextBlock)item.Content).Text=String.IsNullOrWhiteSpace(selected.Name)?"未命名服务":selected.Name;break;}RaiseChanged();};
            selected=drafts.Find(p=>p.Id==settings.ActiveTextProviderId) ?? drafts[0];RefreshProviders();Show(selected);
        }
        static Border Glass(UIElement child,int padding)
        {
            return new Border{Child=child,Padding=new Thickness(padding),CornerRadius=new CornerRadius(14),BorderBrush=Ui.Brush("#DCE7F8"),BorderThickness=new Thickness(1),Background=new LinearGradientBrush(Ui.Brush("#FAFFFFFF").Color,Ui.Brush("#EDF4FAFF").Color,90)};
        }
        void RaiseChanged(){if(Changed!=null)Changed();}
        void Capture(bool includeCurrentModel=true)
        {
            if(selected==null || connection==null)return;
            selected.Name=providerName.Text.Trim();selected.Connection=connection.Draft();
            if(includeCurrentModel && selected.Connection.Model.Length>0 && !selected.Models.Contains(selected.Connection.Model))selected.Models.Add(selected.Connection.Model);
        }
        void Show(TextProviderSettings provider)
        {
            updating=true;selected=provider;providerName.Text=provider.Name;
            connection=new ConnectionSettingsPanel.ConnectionEditor("连接设置","TextService","text",provider.Connection);
            connection.Changed+=delegate {
                if(updating)return;string id=connection.Preview().Model;
                if(newModel.Text.Trim()!=id){updating=true;newModel.Text=id;modelDisplayName.Text=selected.ModelDisplayNames.ContainsKey(id)?selected.ModelDisplayNames[id]:"";updating=false;}
                RaiseChanged();
            };connectionHost.Content=connection;newModel.Text=provider.Connection.Model;modelDisplayName.Text=provider.ModelDisplayNames.ContainsKey(provider.Connection.Model)?provider.ModelDisplayNames[provider.Connection.Model]:"";feedback.Text="";
            RefreshModels();removeProvider.IsEnabled=drafts.Count>1;updating=false;
        }
        void RefreshProviders()
        {
            bool previous=updating;updating=true;providers.Items.Clear();
            foreach(var provider in drafts){var name=Ui.Text(String.IsNullOrWhiteSpace(provider.Name)?"未命名服务":provider.Name,11);name.TextTrimming=TextTrimming.CharacterEllipsis;var item=new ListBoxItem{Style=itemStyle,Tag=provider,Content=name,Padding=new Thickness(6,9,6,9),Margin=new Thickness(0,0,0,4)};providers.Items.Add(item);if(provider==selected)item.IsSelected=true;}
            updating=previous;
        }
        void RefreshModels()
        {
            bool previous=updating;updating=true;models.Items.Clear();foreach(string id in selected.Models){var label=Ui.Text(selected.DisplayName(id),12);label.TextWrapping=TextWrapping.NoWrap;label.TextTrimming=TextTrimming.CharacterEllipsis;models.Items.Add(new ListBoxItem{Style=itemStyle,Tag=id,Content=label,ToolTip=id,Padding=new Thickness(7,5,7,5)});}models.SelectedValue=selected.Connection.Model;updating=previous;
        }
        void RefreshModelLabels(){foreach(ListBoxItem item in models.Items)((TextBlock)item.Content).Text=selected.DisplayName((string)item.Tag);}
        void SetDisplayName(string id){string label=modelDisplayName.Text.Trim();if(label.Length==0)selected.ModelDisplayNames.Remove(id);else selected.ModelDisplayNames[id]=label;}
        void AddProvider()
        {
            if(drafts.Count>=32){feedback.Text="最多保存 32 个服务商。";return;}Capture();
            selected=new TextProviderSettings{Name="自定义服务 "+(drafts.Count+1)};drafts.Add(selected);RefreshProviders();Show(selected);providerName.Focus();providerName.SelectAll();RaiseChanged();
        }
        void RemoveProvider()
        {
            if(drafts.Count<=1)return;int index=drafts.IndexOf(selected);drafts.Remove(selected);selected=drafts[Math.Min(index,drafts.Count-1)];RefreshProviders();Show(selected);RaiseChanged();
        }
        void AddModel()
        {
            string value=newModel.Text.Trim();if(value.Length==0){feedback.Text="请先填写完整的模型 ID。";return;}Capture();
            if(selected.Models.Count>=100 && !selected.Models.Contains(value)){feedback.Text="每个服务商最多保存 100 个模型。";return;}
            if(!selected.Models.Contains(value))selected.Models.Add(value);SetDisplayName(value);selected.Connection.Model=value;connection.SetModel(value);RefreshModels();feedback.Text="已添加到当前服务商；保存后可在对话框切换。";RaiseChanged();
        }
        void RemoveModel()
        {
            var item=models.SelectedItem as ListBoxItem;string value=item==null?null:item.Tag as string;if(value==null)return;selected.Models.Remove(value);selected.ModelDisplayNames.Remove(value);
            if(connection.Draft().Model==value){selected.Connection.Model=selected.Models.Count==0?"":selected.Models[0];connection.SetModel(selected.Connection.Model);}
            updating=true;newModel.Text=selected.Connection.Model;modelDisplayName.Text=selected.ModelDisplayNames.ContainsKey(selected.Connection.Model)?selected.ModelDisplayNames[selected.Connection.Model]:"";updating=false;RefreshModels();RaiseChanged();
        }
        public ConnectionSettings Preview(){return connection.Preview();}
        public ConnectionSettings Read(){Capture();return connection.Read();}
        public List<TextProviderSettings> ReadProviders()
        {
            Capture();var result=new List<TextProviderSettings>();
            foreach(var provider in drafts){
                if(String.IsNullOrWhiteSpace(provider.Name))throw new InvalidOperationException("请为每个服务商填写名称。");
                if(provider.Models.Count>100 || provider.Models.Exists(m=>m.Length>256))throw new InvalidOperationException("每个服务商最多保存 100 个模型，模型 ID 不能超过 256 个字符。");
                ProxyConfiguration.Endpoint(ProxyConfiguration.Normalize(provider.Connection.ProxyAddress));
                if(provider.Connection.ApiKey.IndexOfAny(new[]{'\r','\n'})>=0)throw new InvalidOperationException("密钥中有换行，请重新粘贴。");
                if(!String.IsNullOrWhiteSpace(provider.Connection.BaseUrl)){var probe=provider.Connection.Copy();probe.Model="validation";ApiClient.Endpoint(probe,ApiClient.ChatPath(probe));}
                var copy=provider.Copy();copy.Normalize();result.Add(copy);
            }return result;
        }
        public bool Matches(PetSettings settings)
        {
            Capture(false);if(drafts.Count!=settings.TextProviders.Count || SelectedProviderId!=settings.ActiveTextProviderId)return false;
            for(int i=0;i<drafts.Count;i++) {var a=drafts[i];var b=settings.TextProviders[i];if(a.Id!=b.Id || a.Name!=b.Name || a.Models.Count!=b.Models.Count || !SameConnection(a.Connection,b.Connection))return false;for(int m=0;m<a.Models.Count;m++)if(a.Models[m]!=b.Models[m] || a.DisplayName(a.Models[m])!=b.DisplayName(b.Models[m]))return false;}
            return true;
        }
        public bool RefreshSaved(PetSettings settings)
        {
            // Saving this draft advances the baseline; external changes only replace a clean draft.
            if(Matches(settings)){baseline=settings.Copy();return false;}
            if(!Matches(baseline))return false;
            settings.EnsureTextProviders();drafts.Clear();foreach(var provider in settings.TextProviders)drafts.Add(provider.Copy());
            baseline=settings.Copy();selected=drafts.Find(p=>p.Id==settings.ActiveTextProviderId) ?? drafts[0];RefreshProviders();Show(selected);RaiseChanged();return true;
        }
        static bool SameConnection(ConnectionSettings a,ConnectionSettings b)
        {
            return a.BaseUrl==b.BaseUrl && a.Model==b.Model && a.ApiKey==b.ApiKey && (a.Protocol=="anthropic")== (b.Protocol=="anthropic") && a.UseProxy==b.UseProxy && a.ProxyAddress==b.ProxyAddress && a.StreamReplies==b.StreamReplies;
        }
    }
}
