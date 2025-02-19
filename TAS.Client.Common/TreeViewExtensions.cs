﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Collections;
using System.Windows.Media;
using System.Diagnostics;

namespace TAS.Client.Common
{
    public class TreeViewExtensions : DependencyObject
    {
        public static bool GetEnableMultiSelect(DependencyObject obj)
        {
            return (bool)obj.GetValue(EnableMultiSelectProperty);
        }

        public static void SetEnableMultiSelect(DependencyObject obj, bool value)
        {
            obj.SetValue(EnableMultiSelectProperty, value);
        }

        // Using a DependencyProperty as the backing store for EnableMultiSelect.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty EnableMultiSelectProperty =
            DependencyProperty.RegisterAttached("EnableMultiSelect", typeof(bool), typeof(TreeViewExtensions), new FrameworkPropertyMetadata(false)
            {
                PropertyChangedCallback = EnableMultiSelectChanged,
                BindsTwoWayByDefault = true
            });

        public static IList GetMultiSelectedItems(DependencyObject obj)
        {
            return (IList)obj?.GetValue(MultiSelectedItemsProperty);
        }

        public static void SetMultiSelectedItems(DependencyObject obj, IList value)
        {
            obj.SetValue(MultiSelectedItemsProperty, value);
        }

        // Using a DependencyProperty as the backing store for SelectedItems.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty MultiSelectedItemsProperty =
            DependencyProperty.RegisterAttached("MultiSelectedItems", typeof(IList), typeof(TreeViewExtensions), new PropertyMetadata(null));


        static TreeViewItem GetAnchorItem(DependencyObject obj)
        {
            return (TreeViewItem)obj.GetValue(AnchorItemProperty);
        }

        static void SetAnchorItem(DependencyObject obj, TreeViewItem value)
        {
            obj.SetValue(AnchorItemProperty, value);
        }

        // Using a DependencyProperty as the backing store for AnchorItem.  This enables animation, styling, binding, etc...
        static readonly DependencyProperty AnchorItemProperty =
            DependencyProperty.RegisterAttached("AnchorItem", typeof(TreeViewItem), typeof(TreeViewExtensions), new PropertyMetadata(null));

        static void EnableMultiSelectChanged(DependencyObject s, DependencyPropertyChangedEventArgs args)
        {
            TreeView tree = (TreeView)s;
            var wasEnable = (bool)args.OldValue;
            var isEnabled = (bool)args.NewValue;
            if (wasEnable)
            {
                tree.RemoveHandler(UIElement.MouseDownEvent, new MouseButtonEventHandler(ItemClicked));
                tree.RemoveHandler(UIElement.KeyDownEvent, new KeyEventHandler(KeyDown));
            }
            if (isEnabled)
            {
                tree.AddHandler(UIElement.MouseDownEvent, new MouseButtonEventHandler(ItemClicked), true);
                tree.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(KeyDown));
            }
        }

        static TreeView GetTree(TreeViewItem item)
        {
            Func<DependencyObject, DependencyObject> getParent = (o) => o == null ? null : VisualTreeHelper.GetParent(o);
            FrameworkElement currentItem = item;
            DependencyObject parent;
            while (!((parent = getParent(currentItem)) is TreeView || parent == null))
                currentItem = (FrameworkElement)parent;
            return (TreeView)getParent(currentItem);
        }

        static void RealSelectedChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
        {
            if (!(sender is TreeViewItem item))
                return;
            var selectedItems = GetMultiSelectedItems(GetTree(item));
            if (selectedItems != null)
            {
                var isSelected = GetIsMultiSelected(item);
                if (isSelected)
                    try
                    {
                        selectedItems.Add(item.DataContext);
                    }
                    catch (ArgumentException)
                    {
                    }
                else
                    selectedItems.Remove(item.DataContext);
            }
        }

        static void KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                ItemSelected((TreeView)sender, FindTreeViewItem(e.OriginalSource), MouseButton.Left);
                e.Handled = true;
            }
        }

        static IEnumerable<TreeViewItem> GetSubItems(TreeViewItem item)
        {
            if (item == null)
                yield break;
            for (int i = 0; i < item.Items.Count; i++)
            {
                if (item.ItemContainerGenerator.ContainerFromIndex(i) is TreeViewItem si)
                {
                    yield return si;
                    foreach (TreeViewItem ssi in GetSubItems(si))
                        yield return ssi;
                }
            }
        }

        static void ItemClicked(object sender, MouseButtonEventArgs e)
        {
            ItemSelected((TreeView)sender, FindTreeViewItem(e.OriginalSource), e.ChangedButton);
        }

        static void ItemSelected(TreeView tree, TreeViewItem item, MouseButton mouseButton)
        {
            if (item == null)
                return;

            if (mouseButton != MouseButton.Left)
            {
                if ((mouseButton == MouseButton.Right) && ((Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) == ModifierKeys.None))
                {
                    if (GetIsMultiSelected(item))
                    {
                        UpdateAnchorAndActionItem(tree, item);
                        return;
                    }
                    MakeSingleSelection(tree, item);
                }
                return;
            }
            if (mouseButton != MouseButton.Left)
            {
                if ((mouseButton == MouseButton.Right) && ((Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) == ModifierKeys.None))
                {
                    if (GetIsMultiSelected(item))
                    {
                        UpdateAnchorAndActionItem(tree, item);
                        return;
                    }
                    MakeSingleSelection(tree, item);
                }
                return;
            }
            if ((Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control)) != (ModifierKeys.Shift | ModifierKeys.Control))
            {
                if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
                {
                    MakeToggleSelection(tree, item);
                    return;
                }
                if ((Keyboard.Modifiers & ModifierKeys.Shift) == ModifierKeys.Shift)
                {
                    MakeAnchorSelection(tree, item, true);
                    return;
                }
                MakeSingleSelection(tree, item);
            }
        }

        private static TreeViewItem FindTreeViewItem(object obj)
        {
            if (!(obj is Visual visual))
                return null;
            if (obj is TreeViewItem tvi)
                return tvi;
            return FindTreeViewItem(VisualTreeHelper.GetParent(visual));
        }



        private static IEnumerable<TreeViewItem> GetExpandedTreeViewItems(ItemsControl tree)
        {
            for (int i = 0; i < tree.Items.Count; i++)
            {
                var item = (TreeViewItem)tree.ItemContainerGenerator.ContainerFromIndex(i);
                if (item == null)
                    continue;
                yield return item;
                if (item.IsExpanded)
                    foreach (var subItem in GetExpandedTreeViewItems(item))
                        yield return subItem;
            }
        }

        private static void MakeAnchorSelection(TreeView tree, TreeViewItem actionItem, bool clearCurrent)
        {
            if (GetAnchorItem(tree) == null)
            {
                var selectedItems = GetSelectedTreeViewItems(tree);
                if (selectedItems.Count > 0)
                {
                    SetAnchorItem(tree, selectedItems[selectedItems.Count - 1]);
                }
                else
                {
                    SetAnchorItem(tree, GetExpandedTreeViewItems(tree).Skip(3).FirstOrDefault());
                }
                if (GetAnchorItem(tree) == null)
                {
                    return;
                }
            }

            var anchor = GetAnchorItem(tree);

            var items = GetExpandedTreeViewItems(tree);
            bool betweenBoundary = false;
            foreach (var item in items)
            {
                bool isBoundary = Equals(item, anchor) || Equals(item, actionItem);
                if (isBoundary)
                {
                    betweenBoundary = !betweenBoundary;
                }
                if (betweenBoundary || isBoundary)
                    SetIsMultiSelected(item, true);
                else
                    if (clearCurrent)
                    SetIsMultiSelected(item, false);
                else
                    break;

            }
        }

        private static List<TreeViewItem> GetSelectedTreeViewItems(TreeView tree)
        {
            return GetExpandedTreeViewItems(tree).Where(i => GetIsMultiSelected(i)).ToList();
        }

        private static void MakeSingleSelection(TreeView tree, TreeViewItem item)
        {
            Debug.Assert(item != null);
            foreach (TreeViewItem selectedItem in GetExpandedTreeViewItems(tree))
            {
                if (selectedItem == null)
                    continue;
                SetIsMultiSelected(selectedItem, Equals(selectedItem, item));
            }
            UpdateAnchorAndActionItem(tree, item);
        }

        private static void MakeToggleSelection(TreeView tree, TreeViewItem item)
        {
            Debug.Assert(item != null);
            SetIsMultiSelected(item, !GetIsMultiSelected(item));
            UpdateAnchorAndActionItem(tree, item);
        }

        private static void UpdateAnchorAndActionItem(TreeView tree, TreeViewItem item)
        {
            SetAnchorItem(tree, item);
        }

        public static bool GetIsMultiSelected(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsMultiSelectedProperty);
        }

        public static void SetIsMultiSelected(DependencyObject obj, bool value)
        {
            obj.SetValue(IsMultiSelectedProperty, value);
        }

        // Using a DependencyProperty as the backing store for IsSelected.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsMultiSelectedProperty =
            DependencyProperty.RegisterAttached("IsMultiSelected", typeof(bool), typeof(TreeViewExtensions), new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault)
            {
                PropertyChangedCallback = RealSelectedChanged
            });
    }
}
