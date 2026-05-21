namespace MotionStudio.Core.Variables;

/// <summary>
/// 流程变量表。
/// </summary>
public sealed class MotionVariableTable
{
    private readonly Dictionary<string, MotionVariable> _variables = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<MotionVariable> Variables => _variables.Values;

    public void Set(string name, object? value)
    {
        _variables[name] = new MotionVariable { Name = name, Value = value };
    }

    public T? Get<T>(string name)
    {
        if (!_variables.TryGetValue(name, out var variable) || variable.Value is null)
        {
            return default;
        }

        if (variable.Value is T value)
        {
            return value;
        }

        return (T)Convert.ChangeType(variable.Value, typeof(T));
    }
}
