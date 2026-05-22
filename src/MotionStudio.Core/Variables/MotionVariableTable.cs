namespace MotionStudio.Core.Variables;

/// <summary>
/// 流程变量表。
/// </summary>
public sealed class MotionVariableTable
{
    private readonly Dictionary<string, MotionVariable> _variables = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<MotionVariable> Variables => _variables.Values;

    public bool TryGetVariable(string name, out object? value)
    {
        value = null;
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (!_variables.TryGetValue(name, out var variable))
        {
            return false;
        }

        value = variable.Value;
        return true;
    }

    public object? GetVariable(string name)
    {
        return TryGetVariable(name, out var value) ? value : null;
    }

    public void SetVariable(string name, object? value)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("变量名不能为空", nameof(name));
        }

        if (value is not null
            && value is not int
            && value is not double
            && value is not bool
            && value is not string)
        {
            throw new InvalidOperationException($"变量 {name} 仅支持 int/double/bool/string");
        }

        _variables[name] = new MotionVariable { Name = name, Value = value };
    }

    public void Set(string name, object? value)
    {
        SetVariable(name, value);
    }

    public T? Get<T>(string name)
    {
        if (!TryGetVariable(name, out var value) || value is null)
        {
            return default;
        }

        if (value is T converted)
        {
            return converted;
        }

        return (T)Convert.ChangeType(value, typeof(T));
    }
}
