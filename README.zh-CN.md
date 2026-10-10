# ImguiCs

**[Dear ImGui](https://github.com/ocornut/imgui) 的纯托管 C# 重写,与 C++ 原版逐位(bit-exact)验证一致。**

[English](README.md) | [简体中文](README.zh-CN.md)

---

## 项目简介

ImguiCs 不是绑定(binding)。它将 Dear ImGui 的 C++ 源码逐文件、逐函数翻译为符合 C#
习惯的代码:核心框架、全部标准控件、表格、docking 分支的停靠系统与多视口、完整的
`ShowDemoWindow`,以及字体图集与文本输入所依赖的 stb_truetype / stb_rect_pack /
stb_textedit。

整个库**不含任何原生代码,没有一行 P/Invoke**,可以在任何装有 .NET 运行时的平台上以
无头(headless)方式运行。其渲染边界与上游完全一致:`ImDrawList` 的顶点/索引缓冲,
加上字体图集纹理。

| | |
| --- | --- |
| 语言 | C#(纯托管,单一 `ImGui` 命名空间) |
| 目标框架 | .NET 8 / .NET 10 |
| 规模 | 70 个源文件、约 6 万行,文件划分与 C++ 源文件一一对应 |
| 上游基线(master) | `v1.92.9b-44-g96b6eb772`(版本号 `1.93.0 WIP`),锁定为 `Cpp/imgui` 子模块 |
| 上游基线(docking) | `v1.92.9b-docking-52-g7e1b65d26`,分支 `features/drawlist_193`,锁定为 `Cpp/imgui-docking` 子模块 |
| 许可证 | MIT(衍生作品,保留上游版权声明) |

## 特性一览

- **核心** —— NewFrame/EndFrame 管线、窗口与剪裁、ID/哈希、键盘与手柄导航、焦点系统、
  `.ini` 设置序列化、样式与颜色系统。
- **控件** —— `imgui_widgets.cpp` 全部内容:按钮、滑条、拖拽输入、`InputText`
  (stb_textedit 完整移植,含撤销栈与 IME 支持)、树、选项卡、列表框、菜单、弹出层、
  颜色选择器等。
- **表格** —— `imgui_tables.cpp` 完整移植:排序、冻结行列、行剪裁、边框、尺寸策略。
- **Docking 与多视口** —— docking 分支的停靠树、标签栏、分割条、dock-node 宿主窗口
  与平台视口逻辑。
- **DrawList** —— 全部图元、抗锯齿填充与折线、贝塞尔曲线,以及 `ImTriangulator`
  凹多边形三角剖分。
- **字体与图集** —— stb_truetype / stb_rect_pack 的完整 C# 移植,图集构建产物与 C++
  **逐字节一致**;默认内嵌 ProggyClean 字体。
- **文本与格式化** —— UTF-8 编解码,以及与 C `printf` 语义逐位兼容的格式化实现
  (`ImFmt`)。
- **Demo 与调试工具** —— `ShowDemoWindow`、Debug Log、Stack Tool、Metrics/Debugger、
  Debug Nodes 全部可用。

## 正确性验证

本仓库的核心是一套**逐位一致(bit-exact)的黄金数据(golden data)闭环**:

1. `Cpp/golden_dump.cpp` 在固定条件下运行 **33 个确定性场景**(1280×720 显示尺寸、
   `dt = 1/60`、禁用 `.ini`、固定鼠标位置、预烘焙图集),把每一帧的全部顶点、索引与
   绘制命令以 IEEE-754 位模式的十六进制逐行转储,写入
   `CSharp/ImguiCs.Tests/GoldenData/`。
2. C# 侧以完全相同的设置与输出格式重演同一批场景
   (`CSharp/ImguiCs.Tests/GoldenTest.cs`)。
3. xUnit 测试要求 C# 输出与提交在仓库中的 C++ 黄金文件**逐行、逐位一致**——任何一个
   float 的尾数偏差都会导致构建失败。

围绕这条主线还有多组交叉验证:

| 验证对象 | C++ 侧 | C# 侧 |
| --- | --- | --- |
| 字体图集光栅化 | `Cpp/atlas_dump.cpp` | `CSharp/AtlasProbe`(纹理逐字节比对) |
| 停靠交互行为 | `Cpp/dock_click_probe.cpp` | `CSharp/DockProbe`(分阶段 PASS/FAIL:标签点击/关闭、中央停靠、`.ini` 往返、分割条拖拽等) |
| Demo 窗口几何 | `Cpp/demo_probe.cpp` | 黄金场景 `demo`(结构 + 全部顶点/索引字节的 FNV-1a 哈希) |
| 每帧内存与耗时 | `Cpp/perf_probe.cpp`(`--wrap=malloc` 计数) | `CSharp/PerfProbe`(`GC.GetTotalAllocatedBytes`) |
| 公共 API 覆盖 | `Cpp/api_coverage.py`(扫描 `imgui.h`) | 对照 `cpp_api.txt` / `cs_api.txt` |

> **浮点约定。** C++ 参考构建使用 `-DIMGUI_EXACT_RSQRT`(精确 `1/sqrtf`),因为
> `rsqrtss` 的结果因 CPU 厂商而异;C# 侧与该参考行为保持一致。见 `Cpp/build_golden.sh`。

## 仓库结构

```
CSharp/
  ImguiCs.slnx        解决方案(库 + 测试 + AtlasProbe + DockProbe)
  ImguiCs/            移植本体——纯托管类库,net8.0 / net10.0
  ImguiCs.Tests/      xUnit 测试:33 个黄金场景 + 单元测试
                      (场景定义在 GoldenTest.cs)
  GoldenRunner/       控制台版黄金数据比对器——逐场景输出 PASS/FAIL,便于调试
  AtlasProbe/         字体图集字节级比对(C# 侧)
  DockProbe/          停靠行为分阶段测试(C# 侧)
  PerfProbe/          每帧托管分配与耗时统计(C# 侧)
  tools/              代码生成与一次性移植脚本
                      (extract_enums.py、patch_docking_begin.py)
Cpp/
  imgui/              上游 master 子模块(参考基线)
  imgui-docking/      上游 docking 分支子模块
  golden_dump.cpp     黄金数据生成器(33 个确定性场景)
  build_golden.sh     编译 golden_dump 并重新生成全部黄金文件
  build_perf.sh       编译并运行 C++ 性能探针
  atlas_dump.cpp / dock_click_probe.cpp / demo_probe.cpp / perf_probe.cpp
                      图集 / 停靠 / Demo / 性能对照探针
  ref_docking.cpp / ref_viewports.cpp
                      移植过程中使用的上游 docking/viewport 参考代码摘录
  difftool.py         把黄金文件差异解码为可读 float 的工具
  patch_trace.py      追踪 C++ 绘制内部调用的临时插桩脚本
  api_coverage.py     公共 API 覆盖率统计
  dock_*.diff, docking_full.diff
                      docking 分支相对 master 基线的变更记录(审查辅助)
cpp_api.txt, cs_api.txt   C++ / C# 公共 API 清单(覆盖率比对产物)
```

## 快速开始

**前置要求**

- .NET 10 SDK(库本身多目标 `net8.0`/`net10.0`;测试与探针目标 `net10.0`)
- 支持子模块的 Git
- 可选:g++(Windows 上使用 MSYS2/MinGW)——仅在需要重新生成 C++ 黄金数据时
- 可选:Python 3——仅在需要运行 `CSharp/tools/` 与 `Cpp/` 下的脚本时

**克隆与测试**

```bash
git clone --recurse-submodules <本仓库地址>
cd Imgui-porting/CSharp

# 完整测试:33 个黄金场景 + 单元测试
dotnet test ImguiCs.Tests

# 控制台逐场景黄金比对
dotnet run --project GoldenRunner

# 停靠行为分阶段测试
dotnet run --project DockProbe

# 字体图集字节比对
dotnet run --project AtlasProbe
```

**重新生成黄金数据**(C++ 侧,会覆盖 `CSharp/ImguiCs.Tests/GoldenData/` 下的期望文件):

```bash
cd Cpp
bash build_golden.sh
```

## 范围与限制

- **不含渲染与平台后端。** 库的输出边界是 `ImDrawList` 的顶点/索引数据与图集纹理。
  要在应用中显示界面,需自行编写渲染/输入层消费这些数据(或等待后续的后端移植)。
- **行为一致,而非源码级兼容。** 目标是相同输入产生完全相同的可见行为与绘制输出。
  内部数据结构按 C# 习惯做了等价改写;公共 API 的名称与签名遵循上游约定,但使用
  C# 类型(`ref` 参数、数组、Span 等)。

## 开发说明

本仓库的 C# 代码由 **ZCode**(AI 编码智能体,基于 **GLM 5.3 Flash** 模型)以 Dear
ImGui 的 C++ 源码为唯一参照逐行重写完成。完整的移植——包括黄金数据验证体系,以及把
每个场景调试到逐位一致所需的全部往返——历时**两天**,累计消耗约 **6 亿 token**。

## 许可证

基于 [MIT 许可证](LICENSE) 发布。作为 Dear ImGui 的衍生作品,版权声明同时保留上游
作者(Omar Cornut 及 Dear ImGui 贡献者)与移植作者的信息。
