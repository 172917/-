using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using MotionStudio.Core.Modules;

namespace MotionStudio.Core.Project;

/// <summary>
/// 运动流程项目。
/// </summary>
public sealed class MotionProject : INotifyPropertyChanged
{
    private bool _isDirty;
    private string _projectName = "Project1";

    public event PropertyChangedEventHandler? PropertyChanged;

    public int ProjectId { get; set; } = 1;

    public string ProjectName
    {
        get => _projectName;
        set => SetField(ref _projectName, value);
    }

    public int LastModuleId { get; set; }

    public ObservableCollection<MotionModuleBase> Modules { get; } = new();

    public bool IsDirty
    {
        get => _isDirty;
        set => SetField(ref _isDirty, value);
    }

    public int NextModuleId()
    {
        LastModuleId++;
        IsDirty = true;
        return LastModuleId;
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
