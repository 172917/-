using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MotionStudio.Core.Modules;

/// <summary>
/// 流程模块通用参数和运行状态。
/// </summary>
public sealed class MotionModuleParam : INotifyPropertyChanged
{       
      //├── ModuleId = 1
      //├── PluginName = "轴运动"
      //├── ModuleName = "移动到拍照位"
    private int _moduleId;
    private string _pluginName = string.Empty;
    private string _moduleName = string.Empty;
    private string _motionCardName = "Sim-1";
    private string _remark = string.Empty;
    private bool _isEnabled = true;
    private bool _isRunning;
    private bool _isSuccess;
    private int _costTimeMs;
    private string _message = string.Empty;

    public event PropertyChangedEventHandler? PropertyChanged;

    [Category("基础")]
    [DisplayName("模块ID")]
    public int ModuleId
    {
        get => _moduleId;
        set => SetField(ref _moduleId, value);
    }

    [Category("基础")]
    [DisplayName("插件名称")]
    public string PluginName
    {
        get => _pluginName;
        set => SetField(ref _pluginName, value);
    }

    [Category("基础")]
    [DisplayName("模块名称")]
    public string ModuleName
    {
        get => _moduleName;
        set => SetField(ref _moduleName, value);
    }

    [Category("基础")]
    [DisplayName("运动卡")]
    public string MotionCardName
    {
        get => _motionCardName;
        set => SetField(ref _motionCardName, string.IsNullOrWhiteSpace(value) ? "Sim-1" : value);
    }

    [Category("基础")]
    [DisplayName("备注")]
    public string Remark
    {
        get => _remark;
        set => SetField(ref _remark, value ?? string.Empty);
    }

    [Category("基础")]
    [DisplayName("启用")]
    public bool IsEnabled
    {
        get => _isEnabled;
        set => SetField(ref _isEnabled, value);
    }

    [Browsable(false)]
    public bool IsRunning
    {
        get => _isRunning;
        set => SetField(ref _isRunning, value);
    }

    [Browsable(false)]
    public bool IsSuccess
    {
        get => _isSuccess;
        set => SetField(ref _isSuccess, value);
    }

    [Browsable(false)]
    public int CostTimeMs
    {
        get => _costTimeMs;
        set => SetField(ref _costTimeMs, value);
    }

    [Browsable(false)]
    public string Message
    {
        get => _message;
        set => SetField(ref _message, value);
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
