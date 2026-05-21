using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using MotionStudio.Core.Modules;
using MotionStudio.Core.Plugins;

namespace MotionStudio.Core.Project;

/// <summary>
/// MotionProject JSON 保存与加载。
/// </summary>
public sealed class MotionProjectSerializer
{
    private readonly MotionPluginService _pluginService;
    private readonly JsonSerializerOptions _options = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public MotionProjectSerializer(MotionPluginService pluginService)
    {
        _pluginService = pluginService;
    }

    public async Task SaveAsync(MotionProject project, string filePath)
    {
        var dto = new MotionProjectDto
        {
            ProjectId = project.ProjectId,
            ProjectName = project.ProjectName,
            LastModuleId = project.LastModuleId,
            Modules = project.Modules.Select(CreateDto).ToList()
        };

        var json = JsonSerializer.Serialize(dto, _options);
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        await File.WriteAllTextAsync(filePath, json).ConfigureAwait(false);
        project.IsDirty = false;
    }

    public async Task<MotionProject> LoadAsync(string filePath)
    {
        var json = await File.ReadAllTextAsync(filePath).ConfigureAwait(false);
        var dto = JsonSerializer.Deserialize<MotionProjectDto>(json, _options)
            ?? throw new InvalidOperationException("项目文件格式无效");

        var project = new MotionProject
        {
            ProjectId = dto.ProjectId,
            ProjectName = dto.ProjectName,
            LastModuleId = dto.LastModuleId
        };

        foreach (var moduleDto in dto.Modules)
        {
            var module = CreateModule(moduleDto);
            module.ParamChanged += (_, _) => project.IsDirty = true;
            module.Param.PropertyChanged += (_, _) => project.IsDirty = true;
            project.Modules.Add(module);
        }

        project.IsDirty = false;
        return project;
    }

    private ModuleDto CreateDto(MotionModuleBase module)
    {
        var dto = new ModuleDto
        {
            PluginName = module.Param.PluginName,
            ModuleName = module.Param.ModuleName,
            ModuleId = module.Param.ModuleId,
            MotionCardName = module.Param.MotionCardName,
            Remark = module.Param.Remark,
            IsEnabled = module.Param.IsEnabled,
            ModuleType = module.GetType().AssemblyQualifiedName ?? module.GetType().FullName ?? string.Empty,
            ModuleKind = module.ModuleKind,
            ParentModuleId = module.ParentModuleId
        };

        foreach (var property in GetPersistedProperties(module.GetType()))
        {
            var value = property.GetValue(module);
            dto.Properties[property.Name] = JsonSerializer.SerializeToElement(value, property.PropertyType, _options);
        }

        return dto;
    }

    private MotionModuleBase CreateModule(ModuleDto dto)
    {
        MotionModuleBase module;
        if (_pluginService.PluginDic.TryGetValue(dto.PluginName, out var info))
        {
            module = (MotionModuleBase)Activator.CreateInstance(info.ModuleType)!;
        }
        else
        {
            var type = Type.GetType(dto.ModuleType, false)
                ?? throw new InvalidOperationException($"无法恢复模块类型：{dto.ModuleType}");
            module = (MotionModuleBase)Activator.CreateInstance(type)!;
        }

        module.Param.PluginName = dto.PluginName;
        module.Param.ModuleName = dto.ModuleName;
        module.Param.ModuleId = dto.ModuleId;
        module.Param.MotionCardName = string.IsNullOrWhiteSpace(dto.MotionCardName) ? MotionContext.DefaultMotionCardName : dto.MotionCardName;
        module.Param.Remark = dto.Remark ?? string.Empty;
        module.Param.IsEnabled = dto.IsEnabled;
        module.ModuleKind = dto.ModuleKind;
        module.ParentModuleId = dto.ParentModuleId;

        foreach (var property in GetPersistedProperties(module.GetType()))
        {
            if (!dto.Properties.TryGetValue(property.Name, out var element))
            {
                continue;
            }

            var value = element.Deserialize(property.PropertyType, _options);
            property.SetValue(module, value);
        }

        return module;
    }

    private static IEnumerable<PropertyInfo> GetPersistedProperties(Type moduleType)
    {
        return moduleType.GetProperties(BindingFlags.Instance | BindingFlags.Public)
            .Where(p => p.CanRead && p.CanWrite)
            .Where(p => p.Name != nameof(MotionModuleBase.Param))
            .Where(p => p.GetIndexParameters().Length == 0)
            .Where(p => IsSimpleType(p.PropertyType));
    }

    private static bool IsSimpleType(Type type)
    {
        var targetType = Nullable.GetUnderlyingType(type) ?? type;
        return targetType.IsPrimitive || targetType.IsEnum || targetType == typeof(string) || targetType == typeof(decimal);
    }

    private sealed class MotionProjectDto
    {
        public int ProjectId { get; set; }

        public string ProjectName { get; set; } = string.Empty;

        public int LastModuleId { get; set; }

        public List<ModuleDto> Modules { get; set; } = new();
    }

    private sealed class ModuleDto
    {
        public string PluginName { get; set; } = string.Empty;

        public string ModuleName { get; set; } = string.Empty;

        public int ModuleId { get; set; }

        public string MotionCardName { get; set; } = MotionContext.DefaultMotionCardName;

        public string Remark { get; set; } = string.Empty;

        public bool IsEnabled { get; set; }

        public string ModuleType { get; set; } = string.Empty;

        public ModuleKind ModuleKind { get; set; } = ModuleKind.Normal;

        public int? ParentModuleId { get; set; }

        public Dictionary<string, JsonElement> Properties { get; set; } = new();
    }
}
