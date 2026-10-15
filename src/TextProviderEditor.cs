using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace Dafeiyu
{
    // A private draft per provider keeps unfinished edits out of the running conversation.
    public sealed class TextProviderEditor : Grid
    {
        static readonly Style itemStyle=(Style)XamlReader.Parse("<Style xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' TargetType='ListBoxItem'><Setter Property='HorizontalContentAlignment' Value='Stretch'/><Setter Property='Template'><Setter.Value><ControlTemplate TargetType='ListBoxItem'><Border x:Name='surface' Background='Transparent' CornerRadius='7' Padding='{TemplateBinding Padding}' BorderThickness='1' BorderBrush='Transparent'><ContentPresenter/></Border><ControlTemplate.Triggers><Trigger Property='IsSelected' Value='True'><Setter TargetName='surface' Property='Background' Value='#E8F0FF'/><Setter TargetName='surface' Property='BorderBrush' Value='#D1E0FF'/></Trigger><Trigger Property='IsMouseOver' Value='True'><Setter TargetName='surface' Property='Background' Value='#EFF5FF'/></Trigger><Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='surface' Property='BorderBrush' Value='#456DE6'/></Trigger></ControlTemplate.Triggers></ControlTemplate></Setter.Value></Setter></Style>");
        readonly string kind,prefix;
        readonly List<TextProviderSettings> drafts=new List<TextProviderSettings>();
        readonly ListBox providers,models;
        readonly ContentControl connectionHost=new ContentControl();
        readonly TextBox providerName,newModel,modelDisplayName,search;
        readonly TextBlock modelCount,emptyModels;
        readonly Button refreshModels;
        readonly ProgressBar loading;
        readonly ListBox discovered;
        readonly StackPanel discoveryPanel;
        readonly Button addSelected;
        readonly HashSet<string> checkedModels=new HashSet<string>();
        readonly HashSet<string> unsavedProviders=new HashSet<string>();
        readonly Dictionary<string,IList<string>> catalogs=new Dictionary<string,IList<string>>();
        readonly Dictionary<string,ConnectionSettings> catalogConnections=new Dictionary<string,ConnectionSettings>();
        public Func<bool> SaveConnection {get;set;}
        public readonly StackPanel Options=new StackPanel();
        public readonly StackPanel Extras=new StackPanel();


        string N(string name){return kind=="text"?name:name.Replace("Text",kind=="asr"?"Asr":kind=="speech"?"Speech":"Live");}
        ConnectionSettings CatalogDraft(){return connection.Draft();}
        readonly DispatcherTimer fetchDelay=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(800)};
        readonly Dictionary<string,ConnectionSettings> attempted=new Dictionary<string,ConnectionSettings>();
        CancellationTokenSource fetching;
        string fetchingProviderId;
        TextBlock connectionEndpoint;
        PetSettings baseline;
        readonly TextBlock feedback;
        readonly Button removeProvider;
        ConnectionSettingsPanel.ConnectionEditor connection;
        TextProviderSettings selected;
        bool updating,automaticName;
        public event Action Changed;
        public string SelectedProviderId {get{return selected.Id;}}
        public TextProviderEditor(PetSettings settings,string serviceKind="text")
        {
            kind=serviceKind;prefix=kind=="text"?"TextService":kind=="asr"?"Transcription":kind=="speech"?"Speech":"LiveService";
            Name=N("GuideTextProviderSection");
            settings=settings.ProviderView(kind);settings.EnsureTextProviders();baseline=settings.Copy();foreach(var provider in settings.TextProviders)drafts.Add(provider.Copy());
            ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(138)});
            ColumnDefinitions.Add(new ColumnDefinition{Width=new GridLength(1,GridUnitType.Star)});
            var rail=new StackPanel();var heading=Ui.Text("服务商",15);heading.FontWeight=FontWeights.SemiBold;rail.Children.Add(heading);
            rail.Children.Add(Ui.Text("独立地址与密钥",10,Ui.Muted));
            providers=new ListBox{Name=N("TextProviderList"),BorderThickness=new Thickness(0),Background=Brushes.Transparent,Margin=new Thickness(0,12,0,10),MinHeight=76,MaxHeight=340,HorizontalContentAlignment=HorizontalAlignment.Stretch};
            ScrollViewer.SetHorizontalScrollBarVisibility(providers,ScrollBarVisibility.Disabled);rail.Children.Add(providers);
            var add=Ui.Button("＋ 新增");add.Name=N("AddTextProvider");add.FontSize=11;add.Margin=new Thickness(0,0,0,7);add.Padding=new Thickness(6);add.Click+=delegate {AddProvider();};rail.Children.Add(add);
            removeProvider=Ui.Button("移除服务商");removeProvider.Name=N("RemoveTextProvider");removeProvider.FontSize=11;removeProvider.Margin=new Thickness(0);removeProvider.Padding=new Thickness(6);removeProvider.Click+=delegate {RemoveProvider();};rail.Children.Add(removeProvider);
            rail.Children.Add(Ui.Text("保存后使用当前服务商与模型。",10,Ui.Muted));
            var railCard=Glass(rail,10);railCard.Margin=new Thickness(0,0,12,0);railCard.VerticalAlignment=VerticalAlignment.Top;Children.Add(railCard);
            var detail=new StackPanel();detail.Children.Add(Options);providerName=Ui.Field();providerName.Name=N("TextProviderName");providerName.MaxLength=80;Ui.Label(detail,"服务商名称",providerName);
            connectionHost.Margin=new Thickness(0,14,0,12);detail.Children.Add(connectionHost);
            var modelHeader=new DockPanel{Margin=new Thickness(0,4,0,7)};
            refreshModels=Ui.Button("刷新模型");refreshModels.Name=N("FetchTextModels");refreshModels.FontSize=11;refreshModels.MinHeight=30;refreshModels.Padding=new Thickness(10,4,10,4);refreshModels.Margin=new Thickness(8,0,0,0);DockPanel.SetDock(refreshModels,Dock.Right);modelHeader.Children.Add(refreshModels);
            modelCount=Ui.Text("",11,Ui.Muted);modelCount.Name=N("TextModelCount");modelCount.VerticalAlignment=VerticalAlignment.Center;DockPanel.SetDock(modelCount,Dock.Right);modelHeader.Children.Add(modelCount);
            var modelHeading=Ui.Text("可用模型",16);modelHeading.FontWeight=FontWeights.SemiBold;modelHeading.VerticalAlignment=VerticalAlignment.Center;modelHeader.Children.Add(modelHeading);detail.Children.Add(modelHeader);
            detail.Children.Add(Ui.Text(kind=="text"?"保存连接后获取列表，勾选需要使用的模型。":kind=="live"?"获取 Live 候选模型；不开始通话或收音。":"列表可能包含其他用途模型，请选择"+(kind=="asr"?"语音识别":"语音合成")+"模型。",11,Ui.Muted));
            search=Ui.Field();search.Name=N("TextModelSearch");search.MaxLength=256;search.ToolTip="搜索模型 ID 或中文名称";Ui.Label(detail,"搜索模型 ID 或名称",search);
            loading=new ProgressBar{Name=N("TextModelsLoading"),Height=2,IsIndeterminate=true,Foreground=Ui.Blue,Visibility=Visibility.Collapsed,Margin=new Thickness(0,6,0,0)};detail.Children.Add(loading);
            models=new ListBox{Name=N("TextProviderModels"),MinHeight=42,MaxHeight=224,Background=Brushes.White,BorderThickness=new Thickness(0),Margin=new Thickness(0,8,0,8),SelectedValuePath="Tag",HorizontalContentAlignment=HorizontalAlignment.Stretch};ScrollViewer.SetHorizontalScrollBarVisibility(models,ScrollBarVisibility.Disabled);detail.Children.Add(models);
            emptyModels=Ui.Text("",12,Ui.Muted);emptyModels.Name=N("TextModelsEmpty");emptyModels.Margin=new Thickness(8,18,8,20);detail.Children.Add(emptyModels);
            discoveryPanel=new StackPanel{Margin=new Thickness(0,8,0,14),Visibility=Visibility.Collapsed};
            var discoveryTitle=Ui.Text("选择要添加的模型",13);discoveryTitle.FontWeight=FontWeights.SemiBold;discoveryPanel.Children.Add(discoveryTitle);
            discoveryPanel.Children.Add(Ui.Text("可勾选多个模型，再点击添加。",11,Ui.Muted));
            discovered=new ListBox{Name=N("DiscoveredTextModels"),MaxHeight=190,BorderThickness=new Thickness(1),BorderBrush=Ui.Line,Background=Brushes.White,Margin=new Thickness(0,7,0,7)};ScrollViewer.SetHorizontalScrollBarVisibility(discovered,ScrollBarVisibility.Disabled);discoveryPanel.Children.Add(discovered);
            var discoveryActions=new WrapPanel();addSelected=Ui.Button("添加所选模型",true);addSelected.Name=N("AddSelectedTextModels");addSelected.IsEnabled=false;addSelected.Click+=delegate{AddSelectedModels();};discoveryActions.Children.Add(addSelected);
            var selectAll=Ui.Button("全选");selectAll.Name="SelectAllDiscoveredModels";selectAll.Click+=delegate{foreach(ListBoxItem item in discovered.Items)((CheckBox)item.Content).IsChecked=true;};discoveryActions.Children.Add(selectAll);discoveryPanel.Children.Add(discoveryActions);detail.Children.Add(discoveryPanel);
            Ui.Label(detail,"模型 ID",newModel=Ui.Field());newModel.Name=N("TextProviderNewModel");newModel.MaxLength=256;newModel.ToolTip="请求时使用的完整模型 ID";
            var modelRow=new Grid();modelRow.ColumnDefinitions.Add(new ColumnDefinition());modelRow.ColumnDefinitions.Add(new ColumnDefinition{Width=GridLength.Auto});
            modelDisplayName=Ui.Field();modelDisplayName.Name=N("TextModelDisplayName");modelDisplayName.MaxLength=80;modelDisplayName.ToolTip="例如：日常聊天、深度思考；留空则显示模型 ID";modelRow.Children.Add(modelDisplayName);
            var addModel=Ui.Button("添加 / 更新");addModel.Name=N("AddTextModel");addModel.Margin=new Thickness(6,0,0,0);addModel.Padding=new Thickness(10,4,10,4);addModel.Click+=delegate {AddModel();};Grid.SetColumn(addModel,1);modelRow.Children.Add(addModel);Ui.Label(detail,"显示名称（可填写中文）",modelRow);
            var removeModel=Ui.Button("移除选中模型");removeModel.Name=N("RemoveTextModel");removeModel.HorizontalAlignment=HorizontalAlignment.Left;removeModel.FontSize=11;removeModel.MinHeight=28;removeModel.Margin=new Thickness(0,6,0,0);removeModel.Click+=delegate {RemoveModel();};detail.Children.Add(removeModel);
            feedback=Ui.Text("",11,Ui.Blue);feedback.Name=N("TextProviderNotice");detail.Children.Add(feedback);detail.Children.Add(Extras);
            if(kind!="text"){detail.Children.Clear();detail.Children.Add(Options);Ui.Label(detail,"服务商名称",providerName);detail.Children.Add(connectionHost);detail.Children.Add(Extras);}
            var detailCard=Glass(detail,14);Grid.SetColumn(detailCard,1);Children.Add(detailCard);
            providers.SelectionChanged+=delegate {if(updating)return;var item=providers.SelectedItem as ListBoxItem;if(item==null)return;Capture();Show((TextProviderSettings)item.Tag);RaiseChanged();};
            models.SelectionChanged+=delegate {if(updating || connection==null)return;var item=models.SelectedItem as ListBoxItem;string value=item==null?null:item.Tag as string;if(value!=null){connection.SetModel(value);selected.Connection.Model=value;updating=true;newModel.Text=value;modelDisplayName.Text=selected.ModelDisplayNames.ContainsKey(value)?selected.ModelDisplayNames[value]:"";updating=false;RefreshModelLabels();RaiseChanged();}};
            newModel.TextChanged+=delegate {
                if(updating || selected==null || connection==null)return;
                updating=true;connection.SetModel(newModel.Text);string id=newModel.Text.Trim();selected.Connection.Model=id;modelDisplayName.Text=selected.ModelDisplayNames.ContainsKey(id)?selected.ModelDisplayNames[id]:"";updating=false;RefreshModelLabels();RaiseChanged();
            };
            modelDisplayName.TextChanged+=delegate {if(updating || selected==null)return;string id=newModel.Text.Trim();if(id.Length>0){SetDisplayName(id);RefreshModelLabels();RaiseChanged();}};
            providerName.TextChanged+=delegate {if(updating || selected==null)return;automaticName=false;selected.Name=providerName.Text;RefreshProviders();RaiseChanged();};
            search.TextChanged+=delegate{if(!updating && selected!=null)RefreshModels();};
            refreshModels.Click+=async delegate{await FetchModelsAsync(true);};
            fetchDelay.Tick+=async delegate{fetchDelay.Stop();await FetchModelsAsync();};
            Loaded+=delegate{ScheduleFetch();};Unloaded+=delegate{CancelFetch();};
            IsVisibleChanged+=delegate{if(IsVisible)ScheduleFetch();else CancelFetch();};
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
            if(includeCurrentModel && kind!="text")selected.Models.Clear();
            if(includeCurrentModel && selected.Connection.Model.Length>0 && !selected.Models.Contains(selected.Connection.Model))selected.Models.Add(selected.Connection.Model);
        }
        void Show(TextProviderSettings provider)
        {
            CancelFetch();
            updating=true;selected=provider;providerName.Text=provider.Name;automaticName=ProviderIdentity.AutomaticName(provider.Name,provider);
            connection=new ConnectionSettingsPanel.ConnectionEditor("连接设置",prefix,kind,provider.Connection);
            connection.FactorySelected+=delegate(string brand){selected.BrandId=brand;};
            var saveAndFetch=Ui.Button("保存并获取模型列表",true);saveAndFetch.Name=N("SaveAndFetchTextModels");saveAndFetch.HorizontalAlignment=HorizontalAlignment.Left;saveAndFetch.Margin=new Thickness(0,12,0,0);
            saveAndFetch.Click+=async delegate{if(SaveConnection!=null && SaveConnection()){unsavedProviders.Remove(selected.Id);await FetchModelsAsync(true);}};if(kind=="text")connection.Children.Add(saveAndFetch);
            connection.Changed+=delegate {
                if(updating)return;string id=connection.Preview().Model;
                if(newModel.Text.Trim()!=id){updating=true;newModel.Text=id;modelDisplayName.Text=selected.ModelDisplayNames.ContainsKey(id)?selected.ModelDisplayNames[id]:"";updating=false;}
                selected.Connection.Model=id;RefreshModelLabels();connectionEndpoint.Text=EndpointLabel(connection.Draft());if(automaticName){updating=true;string name=ProviderIdentity.NameForBrand(selected.BrandId);selected.Name=name.Length>0?name:ProviderIdentity.Name(connection.Draft());providerName.Text=selected.Name;updating=false;}RefreshProviders();RaiseChanged();ScheduleFetch();
            };var summary=new StackPanel();var caption=Ui.Text("连接设置",12);caption.FontWeight=FontWeights.SemiBold;summary.Children.Add(caption);connectionEndpoint=Ui.Text(EndpointLabel(provider.Connection),10,Ui.Muted);connectionEndpoint.TextTrimming=TextTrimming.CharacterEllipsis;connectionEndpoint.TextWrapping=TextWrapping.NoWrap;summary.Children.Add(connectionEndpoint);
            connectionHost.Content=new Expander{Name=N("TextConnectionDetails"),Header=summary,Content=connection,IsExpanded=kind!="text" || unsavedProviders.Contains(provider.Id) || !ValidCatalog(provider.Connection),Padding=new Thickness(0,4,0,4)};newModel.Text=provider.Connection.Model;modelDisplayName.Text=provider.ModelDisplayNames.ContainsKey(provider.Connection.Model)?provider.ModelDisplayNames[provider.Connection.Model]:"";feedback.Text="";
            checkedModels.Clear();search.Text="";RefreshModels();RefreshProviders();removeProvider.IsEnabled=drafts.Count>1;updating=false;ScheduleFetch();
        }
        void RefreshProviders()
        {
            bool previous=updating;updating=true;providers.Items.Clear();
            foreach(var provider in drafts){var name=Ui.Text(String.IsNullOrWhiteSpace(provider.Name)?"未命名服务":provider.Name,11);name.TextWrapping=TextWrapping.NoWrap;name.TextTrimming=TextTrimming.CharacterEllipsis;name.VerticalAlignment=VerticalAlignment.Center;var row=new DockPanel();var logo=ProviderLogo.Create(provider==selected && connection!=null?connection.Draft().BaseUrl:provider.Connection.BaseUrl,provider.Name,22,provider.BrandId);logo.Margin=new Thickness(0,0,7,0);DockPanel.SetDock(logo,Dock.Left);row.Children.Add(logo);row.Children.Add(name);var item=new ListBoxItem{Style=itemStyle,Tag=provider,Content=row,ToolTip=provider.Name,Padding=new Thickness(6,9,6,9),Margin=new Thickness(0,0,0,4)};providers.Items.Add(item);if(provider==selected)item.IsSelected=true;}
            updating=previous;
        }
        void RefreshModels()
        {
            bool previous=updating;updating=true;models.Items.Clear();string query=search.Text.Trim();
            foreach(string id in selected.Models)
            {
                if(query.Length>0 && id.IndexOf(query,StringComparison.OrdinalIgnoreCase)<0 && selected.DisplayName(id).IndexOf(query,StringComparison.OrdinalIgnoreCase)<0)continue;
                models.Items.Add(new ListBoxItem{Style=itemStyle,Tag=id,Content=ModelContent(id),ToolTip=id,Padding=new Thickness(10,8,10,8),Margin=new Thickness(0,0,0,3)});
            }
            models.SelectedValue=selected.Connection.Model;modelCount.Text=query.Length==0?selected.Models.Count+" 个模型":models.Items.Count+" / "+selected.Models.Count;
            emptyModels.Text=query.Length>0?"未找到匹配模型，试试其他关键词。":"暂无已添加模型。保存连接并获取列表后，可勾选多个模型添加。";
            emptyModels.Visibility=models.Items.Count==0?Visibility.Visible:Visibility.Collapsed;models.Visibility=models.Items.Count==0?Visibility.Collapsed:Visibility.Visible;updating=previous;
            RefreshDiscovery();
        }
        void RefreshDiscovery()
        {
            IList<string> found;discovered.Items.Clear();string query=search.Text.Trim();
            if(catalogs.TryGetValue(selected.Id,out found))foreach(string id in found)
            {
                if(selected.Models.Contains(id) || (query.Length>0 && id.IndexOf(query,StringComparison.OrdinalIgnoreCase)<0))continue;
                string model=id;var pick=new CheckBox{Content=id,Tag=id,IsChecked=checkedModels.Contains(id),MinHeight=28,Padding=new Thickness(6,4,6,4),Foreground=Ui.Ink,VerticalContentAlignment=VerticalAlignment.Center};
                pick.Checked+=delegate{checkedModels.Add(model);addSelected.IsEnabled=checkedModels.Count>0;};pick.Unchecked+=delegate{checkedModels.Remove(model);addSelected.IsEnabled=checkedModels.Count>0;};
                discovered.Items.Add(new ListBoxItem{Content=pick,Tag=id,HorizontalContentAlignment=HorizontalAlignment.Stretch,Padding=new Thickness(3)});
            }
            discoveryPanel.Visibility=discovered.Items.Count>0?Visibility.Visible:Visibility.Collapsed;addSelected.IsEnabled=checkedModels.Count>0;
        }
        void AddSelectedModels()
        {
            IList<string> found;if(!catalogs.TryGetValue(selected.Id,out found))return;int count=0;string first=null;
            foreach(string id in found)if(checkedModels.Contains(id) && !selected.Models.Contains(id) && selected.Models.Count<TextProviderSettings.MaximumModels){selected.Models.Add(id);if(first==null)first=id;count++;}
            if(String.IsNullOrWhiteSpace(connection.Draft().Model) && first!=null){selected.Connection.Model=first;connection.SetModel(first);}
            checkedModels.Clear();RefreshModels();feedback.Foreground=Ui.Muted;feedback.Text="已添加 "+count+" 个模型；点击“保存配置”后使用。";if(selected.Models.Count>=TextProviderSettings.MaximumModels)feedback.Text+=" 已达到 "+TextProviderSettings.MaximumModels+" 个模型上限，其他模型未添加。";RaiseChanged();
        }
        UIElement ModelContent(string id)
        {
            var row=new DockPanel();
            if(id==selected.Connection.Model){var badge=new Border{Child=Ui.Text("当前选择",10,Ui.Blue),Background=Ui.Pale,CornerRadius=new CornerRadius(5),Padding=new Thickness(6,3,6,3),Margin=new Thickness(8,0,0,0),VerticalAlignment=VerticalAlignment.Center};DockPanel.SetDock(badge,Dock.Right);row.Children.Add(badge);}
            var labels=new StackPanel();var title=Ui.Text(selected.DisplayName(id),12);title.FontWeight=FontWeights.SemiBold;title.TextWrapping=TextWrapping.NoWrap;title.TextTrimming=TextTrimming.CharacterEllipsis;labels.Children.Add(title);
            if(selected.DisplayName(id)!=id){var identifier=Ui.Text(id,11,Ui.Muted);identifier.Margin=new Thickness(0,3,0,0);identifier.TextWrapping=TextWrapping.NoWrap;identifier.TextTrimming=TextTrimming.CharacterEllipsis;labels.Children.Add(identifier);}
            row.Children.Add(labels);return row;
        }
        void RefreshModelLabels(){foreach(ListBoxItem item in models.Items)item.Content=ModelContent((string)item.Tag);}
        static bool SameCatalog(ConnectionSettings a,ConnectionSettings b)
        {return a.BaseUrl==b.BaseUrl && a.Protocol==b.Protocol && a.ApiKey==b.ApiKey && a.UseProxy==b.UseProxy && (!a.UseProxy || a.ProxyAddress==b.ProxyAddress);}
        static string EndpointLabel(ConnectionSettings settings){Uri address;return Uri.TryCreate(settings.BaseUrl,UriKind.Absolute,out address)?address.Authority:"填写服务地址与 API 密钥";}
        bool ValidCatalog(ConnectionSettings draft)
        {
            try{var probe=draft.Copy();probe.Model="catalog";ApiClient.ValidateConnection(probe,"models");if(probe.UseProxy)ProxyConfiguration.Endpoint(probe.ProxyAddress);return true;}
            catch(ApiException){return false;}catch(InvalidOperationException){return false;}
        }
        void CancelFetch()
        {fetchDelay.Stop();var current=fetching;string providerId=fetchingProviderId;fetching=null;fetchingProviderId=null;if(current!=null){attempted.Remove(providerId);current.Cancel();feedback.Text="获取已取消；返回此页后自动重试。";}refreshModels.IsEnabled=true;refreshModels.Content="刷新模型";loading.Visibility=Visibility.Collapsed;}
        void ScheduleFetch()
        {
            if(kind!="text")return;
            ConnectionSettings cached;
            if(connection!=null && catalogConnections.TryGetValue(selected.Id,out cached) && !SameCatalog(cached,CatalogDraft())){catalogs.Remove(selected.Id);catalogConnections.Remove(selected.Id);checkedModels.Clear();RefreshDiscovery();}
            if(connection==null || !IsLoaded || !IsVisible || unsavedProviders.Contains(selected.Id))return;
            var draft=CatalogDraft();ConnectionSettings previous;
            if(attempted.TryGetValue(selected.Id,out previous) && SameCatalog(previous,draft))return;
            CancelFetch();if(!ValidCatalog(draft))return;fetchDelay.Start();
        }
        async Task FetchModelsAsync(bool reveal=false)
        {
            if(kind!="text" || connection==null || !IsLoaded || !IsVisible)return;
            if(unsavedProviders.Contains(selected.Id)){feedback.Text="请先保存连接信息，再获取模型列表。";return;}
            var draft=CatalogDraft();if(!ValidCatalog(draft)){feedback.Text="请先填写有效的服务地址与密钥，再获取模型。";return;}
            CancelFetch();Capture(false);var provider=selected;var cancel=new CancellationTokenSource();fetching=cancel;fetchingProviderId=provider.Id;attempted[provider.Id]=draft.Copy();
            refreshModels.IsEnabled=false;refreshModels.Content="获取中…";loading.Visibility=Visibility.Visible;feedback.Foreground=Ui.Muted;feedback.Text="正在获取此服务商的模型列表…";
            try
            {
                IList<string> found;using(var api=new ApiClient(TimeSpan.FromSeconds(15)))found=await api.ListModelsAsync(draft,cancel.Token);
                if(cancel.IsCancellationRequested || selected!=provider || !SameCatalog(draft,CatalogDraft()) || !IsLoaded || !IsVisible)return;
                catalogs[provider.Id]=found;catalogConnections[provider.Id]=draft.Copy();checkedModels.Clear();RefreshModels();feedback.Foreground=Ui.Muted;feedback.Text=found.Count==0?"服务返回了空模型列表，可继续手动添加。":"已获取 "+found.Count+" 个模型，请勾选后添加；已配置模型与显示名称保留。";
                var details=connectionHost.Content as Expander;if(details!=null)details.IsExpanded=false;
                if(reveal && discovered.Items.Count>0)Dispatcher.BeginInvoke(DispatcherPriority.ContextIdle,new Action(delegate{if(IsVisible && selected==provider)discoveryPanel.BringIntoView();}));
            }
            catch(OperationCanceledException){}
            catch(Exception e)
            {
                if(!cancel.IsCancellationRequested && selected==provider && SameCatalog(draft,CatalogDraft())){feedback.Foreground=Ui.Brush("#AC425D");feedback.Text=(e is ApiException?e.Message:"获取模型列表失败，请检查连接配置。")+" 原列表已保留，可手动添加或点击刷新。";}
            }
            finally{if(fetching==cancel){fetching=null;fetchingProviderId=null;refreshModels.IsEnabled=true;refreshModels.Content="刷新模型";loading.Visibility=Visibility.Collapsed;}cancel.Dispose();}
        }
        void SetDisplayName(string id){string label=modelDisplayName.Text.Trim();if(label.Length==0)selected.ModelDisplayNames.Remove(id);else selected.ModelDisplayNames[id]=label;}
        void AddProvider()
        {
            if(drafts.Count>=32){feedback.Text="最多保存 32 个服务商。";return;}
            var picker=new ProviderPickerWindow(Window.GetWindow(this),kind);if(picker.ShowDialog()!=true)return;Capture();var preset=picker.SelectedProvider;
            var draft=new ConnectionSettings{BaseUrl=preset.Url,Protocol=preset.Protocol,Model=kind=="text"?"":preset.Model};
            selected=new TextProviderSettings{BrandId=preset.Url.Length==0?"custom":ProviderIdentity.Brand(preset.Url),Name=preset.Url.Length==0?"自定义服务 "+(drafts.Count+1):ProviderIdentity.Name(draft),Connection=draft};drafts.Add(selected);unsavedProviders.Add(selected.Id);RefreshProviders();Show(selected);RaiseChanged();
        }
        void RemoveProvider()
        {
            if(drafts.Count<=1)return;int index=drafts.IndexOf(selected);drafts.Remove(selected);selected=drafts[Math.Min(index,drafts.Count-1)];RefreshProviders();Show(selected);RaiseChanged();
        }
        void AddModel()
        {
            string value=newModel.Text.Trim();if(value.Length==0){feedback.Text="请先填写完整的模型 ID。";return;}Capture();
            if(selected.Models.Count>=TextProviderSettings.MaximumModels && !selected.Models.Contains(value)){feedback.Text="每个服务商最多保存 "+TextProviderSettings.MaximumModels+" 个模型。";return;}
            if(!selected.Models.Contains(value))selected.Models.Add(value);SetDisplayName(value);selected.Connection.Model=value;connection.SetModel(value);RefreshModels();feedback.Text="已添加到当前服务商；保存后可在对话框切换。";RaiseChanged();
        }
        void RemoveModel()
        {
            var item=models.SelectedItem as ListBoxItem;string value=item==null?null:item.Tag as string;if(value==null)return;selected.Models.Remove(value);selected.ModelDisplayNames.Remove(value);
            if(connection.Draft().Model==value){selected.Connection.Model=selected.Models.Count==0?"":selected.Models[0];connection.SetModel(selected.Connection.Model);}
            updating=true;newModel.Text=selected.Connection.Model;modelDisplayName.Text=selected.ModelDisplayNames.ContainsKey(selected.Connection.Model)?selected.ModelDisplayNames[selected.Connection.Model]:"";updating=false;RefreshModels();RaiseChanged();
        }
        public ConnectionSettings Preview(){return connection.Preview();}
        public void ExpandConnection(){var details=connectionHost.Content as Expander;if(details!=null)details.IsExpanded=true;}
        public ConnectionSettings Read(){Capture();return connection.Read();}
        public TextProviderSettings ReadConnectionProvider(){Capture(false);if(String.IsNullOrWhiteSpace(selected.Name))throw new InvalidOperationException("请填写服务商名称。");return selected.Copy();}
        public void ConnectionSaved(PetSettings settings){baseline=settings.ProviderView(kind).Copy();}
        public List<TextProviderSettings> ReadProviders()
        {
            Capture();var result=new List<TextProviderSettings>();
            foreach(var provider in drafts){
                if(String.IsNullOrWhiteSpace(provider.Name))throw new InvalidOperationException("请为每个服务商填写名称。");
                if(provider.Models.Count>TextProviderSettings.MaximumModels || provider.Models.Exists(m=>m.Length>256))throw new InvalidOperationException("每个服务商最多保存 "+TextProviderSettings.MaximumModels+" 个模型，模型 ID 不能超过 256 个字符。");
                ProxyConfiguration.Endpoint(ProxyConfiguration.Normalize(provider.Connection.ProxyAddress));
                if(provider.Connection.ApiKey.IndexOfAny(new[]{'\r','\n'})>=0)throw new InvalidOperationException("密钥中有换行，请重新粘贴。");
                if(!String.IsNullOrWhiteSpace(provider.Connection.BaseUrl)){var probe=provider.Connection.Copy();probe.Model="validation";if(kind=="live"){probe.ApiKey="validation";GeminiLiveSocket.Endpoint(probe);}else ApiClient.Endpoint(probe,ApiClient.ChatPath(probe));}
                var copy=provider.Copy();copy.Normalize();result.Add(copy);
            }return result;
        }
        public bool Matches(PetSettings settings)
        {
            return MatchesView(settings.ProviderView(kind));
        }
        bool MatchesView(PetSettings settings)
        {
            Capture(false);if(drafts.Count!=settings.TextProviders.Count || SelectedProviderId!=settings.ActiveTextProviderId)return false;
            for(int i=0;i<drafts.Count;i++) {var a=drafts[i];var b=settings.TextProviders[i];if(a.Id!=b.Id || a.Name!=b.Name || a.BrandId!=b.BrandId || a.Models.Count!=b.Models.Count || !SameConnection(a.Connection,b.Connection))return false;for(int m=0;m<a.Models.Count;m++)if(a.Models[m]!=b.Models[m] || a.DisplayName(a.Models[m])!=b.DisplayName(b.Models[m]))return false;}
            return true;
        }
        public bool RefreshSaved(PetSettings settings)
        {
            // Saving this draft advances the baseline; external changes only replace a clean draft.
            settings=settings.ProviderView(kind);if(MatchesView(settings)){baseline=settings.Copy();foreach(var saved in settings.TextProviders)unsavedProviders.Remove(saved.Id);ScheduleFetch();return false;}
            if(!MatchesView(baseline))return false;
            settings.EnsureTextProviders();drafts.Clear();foreach(var provider in settings.TextProviders)drafts.Add(provider.Copy());
            baseline=settings.Copy();selected=drafts.Find(p=>p.Id==settings.ActiveTextProviderId) ?? drafts[0];RefreshProviders();Show(selected);RaiseChanged();return true;
        }
        bool SameConnection(ConnectionSettings a,ConnectionSettings b)
        {
            return a.BaseUrl==b.BaseUrl && a.Model==b.Model && a.ApiKey==b.ApiKey && (kind=="text"?(a.Protocol=="anthropic")== (b.Protocol=="anthropic"):a.Protocol==b.Protocol) && a.UseProxy==b.UseProxy && a.ProxyAddress==b.ProxyAddress && a.StreamReplies==b.StreamReplies;
        }
    }
}
