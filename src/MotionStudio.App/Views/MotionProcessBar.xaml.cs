using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using MotionStudio.App.Infrastructure;
using MotionStudio.App.ViewModels;
using MotionStudio.Core.Modules;

namespace MotionStudio.App.Views;

/// <summary>
/// 流程区视图，处理拖放事件并把流程编辑动作交给 ViewModel。
/// </summary>
public partial class MotionProcessBar : UserControl
{
    private Point _dragStartPoint;
    private MotionModuleBase? _draggedModule;

    public MotionProcessBar()
    {
        InitializeComponent();
    }

    private void ProcessList_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        _dragStartPoint = e.GetPosition(null);
        _draggedModule = FindModule(e.OriginalSource);
    }

    private void ProcessList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed
            || _draggedModule is null
            || DataContext is not MotionProcessBarViewModel viewModel
            || !viewModel.CanEdit)
        {
            return;
        }

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _dragStartPoint.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(position.Y - _dragStartPoint.Y) < SystemParameters.MinimumVerticalDragDistance)
        {
            return;
        }

        var data = new DataObject();
        data.SetData(MotionDragFormats.ProcessModuleId, _draggedModule.Param.ModuleId);
        DragDrop.DoDragDrop(ProcessList, data, DragDropEffects.Move);
    }

    private void ProcessList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var item = FindVisualParent<ListBoxItem>((DependencyObject)e.OriginalSource);
        if (item is not null)
        {
            item.IsSelected = true;
        }
    }

    private void ProcessList_DragOver(object sender, DragEventArgs e)
    {
        if (DataContext is not MotionProcessBarViewModel viewModel || !viewModel.CanEdit)
        {
            e.Effects = DragDropEffects.None;
            e.Handled = true;
            return;
        }

        if (e.Data.GetDataPresent(MotionDragFormats.ModulePluginName) || e.Data.GetDataPresent(DataFormats.Text))
        {
            e.Effects = DragDropEffects.Copy;
        }
        else if (e.Data.GetDataPresent(MotionDragFormats.ProcessModuleId))
        {
            e.Effects = DragDropEffects.Move;
        }
        else
        {
            e.Effects = DragDropEffects.None;
        }

        e.Handled = true;
    }

    private void ProcessList_Drop(object sender, DragEventArgs e)
    {
        if (DataContext is not MotionProcessBarViewModel viewModel || !viewModel.CanEdit)
        {
            return;
        }

        var target = FindModule(e.OriginalSource);
        if (e.Data.GetDataPresent(MotionDragFormats.ModulePluginName) || e.Data.GetDataPresent(DataFormats.Text))
        {
            var pluginName = e.Data.GetData(MotionDragFormats.ModulePluginName) as string
                ?? e.Data.GetData(DataFormats.Text) as string;
            if (!string.IsNullOrWhiteSpace(pluginName))
            {
                viewModel.AddModule(pluginName, target);
            }
        }
        else if (e.Data.GetDataPresent(MotionDragFormats.ProcessModuleId)
                 && e.Data.GetData(MotionDragFormats.ProcessModuleId) is int moduleId
                 && target is not null
                 && viewModel.CurrentProject is not null)
        {
            var moved = viewModel.CurrentProject.Modules.FirstOrDefault(m => m.Param.ModuleId == moduleId);
            if (moved is not null)
            {
                viewModel.MoveModule(moved, target);
            }
        }

        e.Handled = true;
    }

    private static MotionModuleBase? FindModule(object originalSource)
    {
        if (originalSource is not DependencyObject dependencyObject)
        {
            return null;
        }

        var item = FindVisualParent<ListBoxItem>(dependencyObject);
        return item?.DataContext as MotionModuleBase;
    }

    private static T? FindVisualParent<T>(DependencyObject? dependencyObject) where T : DependencyObject
    {
        while (dependencyObject is not null)
        {
            if (dependencyObject is T result)
            {
                return result;
            }

            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        return null;
    }
}
