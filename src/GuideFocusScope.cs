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
        public static GuideFocusScope BlockChildren(UIElement root)
        {
            var scope=new GuideFocusScope();for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++)scope.Apply(VisualTreeHelper.GetChild(root,i),new HashSet<DependencyObject>(),new HashSet<DependencyObject>());return scope;
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
