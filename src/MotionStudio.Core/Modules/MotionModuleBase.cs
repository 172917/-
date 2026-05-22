using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MotionStudio.Core.Modules;

/// <summary>
/// 所有运动流程模块的基类，模块执行必须异步且支持取消。
/// </summary>
public abstract class MotionModuleBase : INotifyPropertyChanged
{
    private MotionModuleParam _param = new();

    public event PropertyChangedEventHandler? PropertyChanged;

    public event EventHandler? ParamChanged;

    [Browsable(false)]
    public MotionModuleParam Param
    {
        get => _param;
        set => SetModuleProperty(ref _param, value);
    }

    [Browsable(false)]
    public ModuleKind ModuleKind { get; set; } = ModuleKind.Normal;

    [Browsable(false)]
    public int? ParentModuleId { get; set; }

    [Browsable(false)]
    public List<MotionModuleBase> Children { get; } = new();

    [Browsable(false)]
    public virtual bool BypassAxisFaultPreCheck => false;

    /// <summary>
    /// 执行模块。
    /// </summary>
    public virtual Task<ModuleResult> ExecuteAsync(MotionContext context, CancellationToken token)
    {
        return Task.FromResult(ModuleResult.Ok());
    }

    /// <summary>
    /// 停止模块。
    /// </summary>
    public virtual Task StopAsync(MotionContext context)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 参数变更回调。
    /// </summary>
    public virtual void OnParamChanged()
    {
        ParamChanged?.Invoke(this, EventArgs.Empty);
    }

    protected bool SetModuleProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (!SetProperty(ref field, value, propertyName))
        {
            return false;
        }

        OnParamChanged();
        return true;
    }

    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
