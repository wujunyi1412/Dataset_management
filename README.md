# Dataset Manager

一个用于登记、关联和追踪图像数据集的 Windows 桌面工具。当前初版负责管理数据集元数据，不负责格式转换。

## 当前功能

- 原始数据集、已处理数据集及数据集划分模块分组管理
- 训练集、测试集、验证集作为数据集划分大模块，提供“创建数据集”功能
- 登记磁盘上的数据集目录，不移动原始文件
- 派生数据集关联到一个原始数据集
- 数据集备注和修改记录
- 数据集扫描只读取图片，不在登记时读取标签
- 已处理数据集支持多组 LabelMe 标注批次，各自保存路径与规则备注
- 标注批次按 `shapes[].label` 自动统计缺陷类别和标注实例数
- 从多个“已处理数据集 + 标注批次”中选取指定数量或全部匹配对，生成带连续序号的 JSON 清单
- 按子目录统计图片格式和数量，汇总分辨率、位深与通道数
- 本地 JSON 持久化
- C# 调用 C++/OpenCV DLL 的基础链路

## 构建

```powershell
cmake -S . -B ./build -G "Visual Studio 17 2022" -A x64
cmake --build ./build --config Release
```

上面的 CMake 构建会同时编译 C++ DLL 和 WPF。仅在开发时直接运行 WPF，也可以使用：

```powershell
dotnet run --project src/DatasetManager.App/DatasetManager.App.csproj
```

Release 构建完成后，所有运行文件都集中在：

```text
D:\Dataset_management\bin\Release
```

直接双击或运行 `bin\Release\DatasetManager.App.exe` 即可。

目录库默认保存在 `%LOCALAPPDATA%\DatasetManager\catalog.json`。C++ 构建后会将自身 DLL，以及 `D:\opencv-4.5.3\opencv-4.5.3\build\install\bin` 下所需的 OpenCV DLL，复制到统一输出目录。

> C++ 构建需要 Visual Studio 2022 的“使用 C++ 的桌面开发”工作负载和 MSVC x64 编译器。如果尚未构建 C++ 组件，WPF 仍可启动，状态栏会显示 OpenCV 组件尚未构建。
