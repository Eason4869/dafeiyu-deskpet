using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace Dafeiyu
{
    // Disable sibling branches, not the target's ancestors, so keyboard focus,
    // scrolling and editable ComboBox popups still work inside the current step.
    internal sealed class GuideFocusScope : IDisposable
    {
        readonly List<Action> restore=new List<Action>();
        GuideFocusScope(){}
        public static GuideFocusScope BlockMenuActions(System.Windows.Controls.ItemsControl menu)
        {
            var scope=new GuideFocusScope();scope.BlockMenuItems(menu);return scope;
        }
        void BlockMenuItems(System.Windows.Controls.ItemsControl menu)
        {
            // The popup's input surface must stay enabled: WPF owns mouse capture
            // there, including the normal click-outside-to-dismiss behavior.
            // Keep categories browsable and disable only actions during the lesson.
            foreach(object entry in menu.Items)
            {
                var item=entry as System.Windows.Controls.MenuItem;if(item==null)continue;
                if(item.HasItems)BlockMenuItems(item);
                else Apply(item,new HashSet<DependencyObject>(),new HashSet<DependencyObject>());
            }
        }
        public GuideFocusScope(UIElement root,params FrameworkElement[] allowed)
        {
            var paths=new HashSet<DependencyObject>();var targets=new HashSet<DependencyObject>();
            foreach(var target in allowed)if(target!=null)
            {
                targets.Add(target);for(DependencyObject node=target;node!=null;node=VisualTreeHelper.GetParent(node)){paths.Add(node);if(node==root)break;}
            }
            Apply(root,paths,targets);
        }
        void Apply(DependencyObject node,HashSet<DependencyObject> paths,HashSet<DependencyObject> targets)
        {
            if(targets.Contains(node) || node is System.Windows.Controls.Primitives.ScrollBar)return;
            var element=node as UIElement;
            if(!paths.Contains(node) && element!=null)
            {
                var enabled=element.ReadLocalValue(UIElement.IsEnabledProperty);var opacity=element.ReadLocalValue(UIElement.OpacityProperty);
                restore.Add(delegate{Restore(element,UIElement.IsEnabledProperty,enabled);Restore(element,UIElement.OpacityProperty,opacity);});
                element.SetCurrentValue(UIElement.IsEnabledProperty,false);element.SetCurrentValue(UIElement.OpacityProperty,.3);return;
            }
            for(int i=0;i<VisualTreeHelper.GetChildrenCount(node);i++)Apply(VisualTreeHelper.GetChild(node,i),paths,targets);
        }
        static void Restore(DependencyObject target,DependencyProperty property,object value)
        {if(value==DependencyProperty.UnsetValue)target.ClearValue(property);else target.SetValue(property,value);}
        public void Dispose(){for(int i=restore.Count-1;i>=0;i--)restore[i]();restore.Clear();}
    }
}
