using Microsoft.Win32;

namespace MotionStudio.App.Services;

/// <summary>
/// 文件对话框服务。
/// </summary>
public sealed class DialogService
{
    public string? ShowSaveProjectDialog()
    {
        var dialog = new SaveFileDialog
        {
            Filter = "MotionStudio Project (*.motion.json)|*.motion.json|JSON (*.json)|*.json",
            FileName = "Project1.motion.json",
            AddExtension = true
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowOpenProjectDialog()
    {
        var dialog = new OpenFileDialog
        {
            Filter = "MotionStudio Project (*.motion.json;*.json)|*.motion.json;*.json|All files (*.*)|*.*"
        };

        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
