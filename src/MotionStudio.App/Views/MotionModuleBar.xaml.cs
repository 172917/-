using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MotionStudio.App.Infrastructure;
using MotionStudio.App.ViewModels;
using MotionStudio.Core.Modules;

namespace MotionStudio.App.Views;

/// <summary>
/// 左侧运动模块库，负责把模块键拖出到流程区。
/// </summary>
public partial class MotionModuleBar : UserControl
{
    public MotionModuleBar()
    {
        InitializeComponent();
    }

    private void ModuleList_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed || sender is not ListBox listBox)
        {
            return;
        }

        if (listBox.SelectedItem is not MotionModuleInfo moduleInfo
            || DataContext is not MotionModuleBarViewModel viewModel
            || !viewModel.CanStartDrag(moduleInfo))
        {
            return;
        }

        var data = new DataObject();
        data.SetData(MotionDragFormats.ModulePluginName, moduleInfo.PluginName);
        data.SetData(DataFormats.Text, moduleInfo.PluginName);
        DragDrop.DoDragDrop(listBox, data, DragDropEffects.Copy);
    }
}
