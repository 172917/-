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
//    PropertyChanged：属性变了，主要给 UI 绑定用
//ParamChanged：模块参数变了，主要给流程系统、属性面板、保存状态、工程脏标记用

    [Browsable(false)]//属性不希望显示在属性浏览器里
    public MotionModuleParam Param
    {
        get => _param;
        set => SetModuleProperty(ref _param, value);
    }
    // Normal,If,EndIf,Loop,SubProcess
    [Browsable(false)]
    public ModuleKind ModuleKind { get; set; } = ModuleKind.Normal;//模块类型或模块性质

    [Browsable(false)]
    public int? ParentModuleId { get; set; }//IF 模块的子模块

    [Browsable(false)]
    public List<MotionModuleBase> Children { get; } = new(); //这个表示当前模块下面挂载的子模块(一大串)

    /// <summary>
    /// 执行模块。
    /// </summary>//模块执行入口
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
        ParamChanged?.Invoke(this, EventArgs.Empty);//参数变化通知
    }

    protected bool SetModuleProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)//通知 UI
    {
        if (!SetProperty(ref field, value, propertyName))
        {
            return false;
        }

        OnParamChanged();
        return true;
    }
    //CallerMemberName自动填入调用它的属性名
    protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)//既通知 UI，也通知流程系统
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
