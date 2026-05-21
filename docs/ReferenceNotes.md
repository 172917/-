# Reference Notes

## 从旧视觉软件参考的内容

- 插件扫描服务的基本思路：扫描插件目录、反射识别模块类型、读取 `CategoryAttribute` 和 `DisplayNameAttribute`。
- 左侧功能栏的交互思路：按分类生成 `Expander`，模块列表支持拖拽。
- 流程栏的交互思路：Drop 时区分新增模块和流程内移动，新增时生成唯一模块名并加入项目模块列表。
- 项目保存和加载的思路：保存模块顺序、模块名称、插件名称和模块参数。

## 从旧运动调试软件参考的内容

- 运动卡应抽象为统一接口。
- 轴、IO、点位、坐标和运动参数应作为独立配置模型。
- 固高、ACS 等真实控制卡应通过适配器接入，不能被流程模块直接依赖。

## 新项目重新设计的内容

- 新建独立 `MotionStudio.sln`，不引用旧项目。
- 使用 `MotionModuleBase` 作为异步流程模块基类。
- 使用 `MotionProcessEngine` 统一处理顺序执行、安全检查、停止、急停和失败停轴。
- 使用 `System.Text.Json` 和 DTO 保存项目，避免保存 UI 控件、线程、运动卡实例和运行态对象。
- 使用 `IMotionCard` 抽象运动卡，第一版提供 `SimMotionCard`。
- WPF UI 基于 HandyControl 资源体系和控件体系重新实现。

## 未直接复用的旧代码

- 未复用旧视觉软件的 Window、UserControl、Project、ModuleObjBase。
- 未复用旧运动调试软件的 WinForms 调试界面。
- 未引用旧项目中的全局静态对象、厂商实现类或旧命名空间。
- 未把旧项目加入新 Solution。
