# Dataset Manager 学习指南（小白版）

> 本文档专为编程初学者准备，特别是还没学过 C# 的同学。我们会从最基础的概念讲起，一步步带你理解这个项目。

---

## 目录

1. [这个项目是做什么的？](#1-这个项目是做什么的)
2. [技术栈全景图](#2-技术栈全景图)
3. [学前准备](#3-学前准备)
4. [第一阶段：C# 基础（2-3周）](#4-第一阶段c-基础2-3周)
5. [第二阶段：WPF 与 MVVM 入门（2周）](#5-第二阶段wpf-与-mvvm-入门2周)
6. [第三阶段：项目架构深度解析（2-3周）](#6-第三阶段项目架构深度解析2-3周)
7. [第四阶段：C++/OpenCV 互操作（1-2周）](#7-第四阶段copencv-互操作1-2周)
8. [第五阶段：动手实践](#8-第五阶段动手实践)
9. [项目代码逐文件导读](#9-项目代码逐文件导读)
10. [常见问题与学习资源](#10-常见问题与学习资源)

---

## 1. 这个项目是做什么的？

### 一句话介绍

**Dataset Manager（数据集管理器）** 是一个运行在 Windows 上的桌面软件，用来帮助深度学习/计算机视觉工程师管理他们的图片数据集。

### 为什么需要它？

做深度学习时，你会有很多数据集：
- 原始图片（刚从相机采集的）
- 标注后的图片（用 LabelMe 工具画了框的）
- 训练集、测试集、验证集
- 训练出来的模型权重文件（.pt、.onnx 等）

这些文件散落在硬盘各处，时间久了就忘了哪个是哪个。这个工具帮你**登记和追踪**这些数据集，记住它们的位置、备注、来源关系，但**不会修改或移动你的原始文件**。

### 核心功能清单

| 功能 | 说明 |
|------|------|
| 数据集登记 | 记录数据集名称、路径、备注、修改历史 |
| 派生关系 | 一个"已处理数据集"可以关联到它的"原始数据集" |
| 标注批次管理 | 一个数据集可以有多批 LabelMe 标注，自动统计类别和数量 |
| 创建划分数据集 | 从多个来源选取图片+标签配对，生成训练/测试/验证集 |
| YOLO 格式转换 | 把 LabelMe JSON 标注转成 YOLO TXT 格式 |
| 重复标签检查 | 对比两个标签目录，找出重复文件 |
| 权重记录 | 登记 .pt、.onnx 等模型权重文件 |
| 图片统计 | 用 OpenCV 读取图片分辨率、位深、通道数 |

---

## 2. 技术栈全景图

在开始学习之前，先了解这个项目用了哪些技术：

```
┌─────────────────────────────────────────────────────┐
│                  用户看到的界面                       │
│  WPF (Windows Presentation Foundation) + XAML       │
├─────────────────────────────────────────────────────┤
│                  界面逻辑层                          │
│  C# ViewModels (MVVM 模式)                          │
├─────────────────────────────────────────────────────┤
│                  业务逻辑层                          │
│  C# Services (扫描、转换、持久化)                     │
├─────────────────────────────────────────────────────┤
│                  数据模型层                          │
│  C# Models (DatasetRecord 等)                       │
├─────────────────────────────────────────────────────┤
│                  互操作层                            │
│  P/Invoke (C# 调用 C++ DLL)                         │
├─────────────────────────────────────────────────────┤
│                  原生层                              │
│  C++ + OpenCV (读取图片元数据)                       │
└─────────────────────────────────────────────────────┘
```

### 各技术的作用

| 技术 | 是什么 | 在项目中的作用 |
|------|--------|---------------|
| **C#** | 微软开发的面向对象编程语言 | 编写大部分业务逻辑和界面 |
| **.NET 8** | C# 的运行平台和类库 | 提供文件操作、JSON 序列化、集合等基础功能 |
| **WPF** | Windows 桌面 UI 框架 | 构建窗口、按钮、列表等界面元素 |
| **XAML** | WPF 的界面标记语言 | 声明式地定义界面长什么样 |
| **MVVM** | 一种 UI 设计模式 | 把界面和逻辑分离，代码更清晰 |
| **C++** | 系统级编程语言 | 编写高性能的图片读取模块 |
| **OpenCV** | 计算机视觉库 | 读取图片尺寸、位深、通道数 |
| **CMake** | 跨平台构建系统 | 编译 C++ DLL |
| **P/Invoke** | .NET 的原生互操作机制 | 让 C# 能调用 C++ 编译的 DLL |

---

## 3. 学前准备

### 3.1 需要安装的软件

1. **Visual Studio 2022**（社区版免费）
   - 安装时勾选：".NET 桌面开发" 和 "使用 C++ 的桌面开发"
   - 下载地址：https://visualstudio.microsoft.com/

2. **.NET 8 SDK**
   - Visual Studio 2022 安装时通常会自带
   - 验证：打开命令提示符输入 `dotnet --version`

3. **CMake**
   - 下载：https://cmake.org/download/
   - 安装时勾选"Add CMake to system PATH"

4. **OpenCV 4.5.3**（注意版本）
   - 项目假设安装在 `D:\opencv-4.5.3\opencv-4.5.3\build\install`
   - 如果安装路径不同，需要修改 [build_release.bat](file:///d:/Dataset_management/build_release.bat)

5. **Git**（可选，用于版本控制）
   - 下载：https://git-scm.com/

### 3.2 验证环境

打开命令提示符（CMD）或 PowerShell，依次运行：

```powershell
dotnet --version       # 应显示 8.x.x
cmake --version        # 应显示 3.20 以上
```

### 3.3 编译并运行项目

在项目根目录双击 `build_release.bat`，成功后运行：
```
D:\Dataset_management\bin\Release\DatasetManager.App.exe
```

如果能看到程序窗口，说明环境配置成功！

---

## 4. 第一阶段：C# 基础（2-3周）

> 目标：掌握 C# 语法核心，能看懂项目中 80% 的代码。

### 第1天：C# 是什么？Hello World

**学习内容：**
- C# 和 .NET 的关系
- 创建第一个控制台程序
- `Main` 函数是什么

**动手：**
```csharp
// Program.cs
using System;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, Dataset Manager!");
        Console.WriteLine("当前时间：" + DateTime.Now);
    }
}
```

**项目对应：** 找到 [App.xaml.cs](file:///d:/Dataset_management/src/DatasetManager.App/App.xaml.cs)，看看 WPF 程序的入口长什么样。

### 第2-3天：变量、类型与运算符

**必须掌握的类型：**
- `int`（整数）、`double`（小数）、`bool`（布尔）
- `string`（字符串）
- `DateTime` / `DateTimeOffset`（时间）
- `Guid`（全局唯一标识符，项目里用来给数据集生成 ID）

**项目中的例子：**
```csharp
// 在 DatasetRecord.cs 中
public Guid Id { get; set; } = Guid.NewGuid();        // 自动生成唯一ID
public string Name { get; set; } = string.Empty;       // 数据集名称
public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.Now;  // 创建时间
```

### 第4-5天：类、对象与属性

C# 是面向对象语言，**类（class）** 是最核心的概念。

**项目中的例子 - 数据集记录类：**
```csharp
// DatasetRecord.cs
public sealed class DatasetRecord
{
    // 属性：像变量，但可以控制读写权限
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public DatasetType Type { get; set; }
    public string RootPath { get; set; } = string.Empty;
}
```

**关键概念：**
- `class`：定义一个类
- `sealed`：密封类，不能被继承（项目里大量使用）
- `{ get; set; }`：自动属性，C# 的语法糖
- `public`：公开的，可以从外部访问
- `private`：私有的，只能在类内部使用

### 第6-7天：集合与泛型

项目里到处都是集合，必须掌握：

| 类型 | 用途 | 项目中的例子 |
|------|------|-------------|
| `List<T>` | 动态数组 | `List<DatasetChangeEntry> ChangeHistory` |
| `Dictionary<K,V>` | 键值对 | `Dictionary<string, int> ClassCounts` |
| `ObservableCollection<T>` | 可观察集合（UI会自动更新） | `ObservableCollection<DatasetItemViewModel>` |
| `IEnumerable<T>` | 可枚举接口 | 方法参数和返回值常用 |

**例子：**
```csharp
var changeHistory = new List<DatasetChangeEntry>();
changeHistory.Add(new DatasetChangeEntry { Description = "创建数据集" });

var classCounts = new Dictionary<string, int>();
classCounts["scratch"] = 15;  // "scratch"类别有15个标注
classCounts["dent"] = 8;      // "dent"类别有8个标注
```

### 第8-9天：异步编程 async/await

项目里有很多文件操作和图片扫描，用了异步编程避免界面卡顿。

**核心概念：**
- `async`：标记一个方法是异步的
- `await`：等待异步操作完成，期间界面不会卡住
- `Task`：表示一个正在进行的操作

**项目中的例子：**
```csharp
// MainViewModel.cs
private async Task LoadAsync()
{
    // await 等待文件读取完成，期间UI可以响应
    var records = await _repository.LoadAsync();
    _models.AddRange(records);
}
```

**小白理解：** 想象你在等外卖。同步方式是你站在门口一直等，什么都不干；异步方式是你下单后继续看电视，外卖到了（await）再去拿。

### 第10-12天：接口、继承与多态

**接口（interface）** 定义"能做什么"，不关心"怎么做"。

**项目中的例子：**
```csharp
// IDatasetRepository.cs - 接口定义
public interface IDatasetRepository
{
    Task<List<DatasetRecord>> LoadAsync();
    Task SaveAsync(List<DatasetRecord> records);
}

// JsonDatasetRepository.cs - 用JSON文件实现这个接口
public class JsonDatasetRepository : IDatasetRepository
{
    public async Task<List<DatasetRecord>> LoadAsync() { /* 读JSON文件 */ }
    public async Task SaveAsync(List<DatasetRecord> records) { /* 写JSON文件 */ }
}
```

**为什么要用接口？** 项目文档里说了，以后可能把 JSON 存储换成 SQLite 数据库。到时候只要写一个 `SqliteDatasetRepository` 实现同样的接口，其他代码不用改！

### 第13-14天：C# 现代语法特性

项目用了很多 C# 8+ 的新语法：

| 语法 | 含义 | 例子 |
|------|------|------|
| `??` | 空合并运算符 | `ParentName ?? "未知"` |
| `?.` | null 条件运算符 | `x?.Name`（x为null就不访问Name） |
| `is` | 模式匹配 | `x is DatasetType.Raw` |
| `switch` 表达式 | 新版 switch | `type switch { ... }` |
| `using` 声明 | 简化资源释放 | 见文件操作代码 |
| 目标类型 `new()` | 省略类型 | `List<int> x = new();` |
| `required` 成员 | 必须初始化的属性 | C# 11 新特性 |

**switch 表达式例子（MainViewModel.cs）：**
```csharp
public string PageTitle => _filterType switch
{
    DatasetType.Training => "训练集",
    DatasetType.Test => "测试集",
    DatasetType.Validation => "验证集",
    _ => "数据集目录"   // _ 表示默认情况
};
```

### 第一阶段推荐资源

- **视频**：B站搜索"C# 入门教程"，找播放量高的系列
- **书籍**：《C# 12 in a Nutshell》（比较厚，当字典查）
- **官方文档**：https://learn.microsoft.com/zh-cn/dotnet/csharp/

---

## 5. 第二阶段：WPF 与 MVVM 入门（2周）

> 目标：理解 WPF 界面如何与 C# 代码交互。

### 5.1 WPF 基础概念

WPF（Windows Presentation Foundation）是微软的桌面 UI 框架。它的特点是：
- 用 **XAML**（一种 XML）描述界面
- 用 **C#** 写行为逻辑
- 强大的**数据绑定**系统

### 5.2 XAML 快速入门

XAML 用标签定义界面元素。看项目的 [MainWindow.xaml](file:///d:/Dataset_management/src/DatasetManager.App/MainWindow.xaml)：

```xml
<Window ...>           <!-- 最外层是窗口 -->
    <Grid>             <!-- Grid是布局容器，像表格 -->
        <Grid.RowDefinitions>
            <RowDefinition Height="*"/>    <!-- 第一行占满剩余空间 -->
            <RowDefinition Height="30"/>   <!-- 第二行固定30像素 -->
        </Grid.RowDefinitions>
        
        <!-- 按钮：内容是"添加"，点击时执行AddCommand -->
        <Button Content="添加" Command="{Binding AddCommand}"/>
        
        <!-- 文本：显示PageTitle属性的值 -->
        <TextBlock Text="{Binding PageTitle}"/>
    </Grid>
</Window>
```

**核心布局容器：**
- `Grid`：表格布局，可以定义行列
- `StackPanel`：水平或垂直堆叠
- `DockPanel`：停靠布局（上下左右）
- `Border`：给子元素加边框和背景
- `ScrollViewer`：可滚动区域

### 5.3 数据绑定（Data Binding）

数据绑定是 WPF 最强大的特性：**界面元素的属性自动和 C# 对象的属性关联**。

```xml
<!-- TextBox的内容和SearchText属性双向绑定 -->
<TextBox Text="{Binding SearchText, UpdateSourceTrigger=PropertyChanged}"/>
```

- 当用户在 TextBox 输入时，`SearchText` 自动更新
- 当 C# 代码修改 `SearchText` 时，TextBox 显示自动更新

**`{Binding ...}` 语法：**
- `Path=属性名`：绑定到哪个属性（可省略 `Path=`）
- `Mode=OneWay`：只从数据源到界面（默认）
- `Mode=TwoWay`：双向同步
- `UpdateSourceTrigger=PropertyChanged`：属性变化立即同步

### 5.4 MVVM 模式详解

MVVM = **Model-View-ViewModel**，是 WPF 最常用的设计模式。

```
┌──────────┐    绑定    ┌──────────────┐    调用    ┌─────────┐
│   View   │ ◄────────► │  ViewModel   │ ────────► │  Model  │
│  (XAML)  │            │   (C#)       │           │  (C#)   │
└──────────┘            └──────────────┘           └─────────┘
   界面长什么样          界面逻辑和状态              业务数据
   按钮、列表            命令、属性、验证            数据集记录
```

在本项目中的对应：

| MVVM 概念 | 项目中的位置 | 作用 |
|-----------|-------------|------|
| **Model** | [DatasetManager.Core/Models/](file:///d:/Dataset_management/src/DatasetManager.Core/Models) | 数据结构：DatasetRecord 等 |
| **View** | [DatasetManager.App/Views/](file:///d:/Dataset_management/src/DatasetManager.App/Views) + XAML | 界面：窗口、控件 |
| **ViewModel** | [DatasetManager.App/ViewModels/](file:///d:/Dataset_management/src/DatasetManager.App/ViewModels) | 界面逻辑：MainViewModel 等 |

### 5.5 ObservableObject - 数据绑定的基础

看 [ObservableObject.cs](file:///d:/Dataset_management/src/DatasetManager.App/ViewModels/ObservableObject.cs)：

```csharp
public abstract class ObservableObject : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    protected bool SetProperty<T>(ref T field, T value, 
        [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) 
            return false;
        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        return true;
    }
}
```

**小白讲解：**
- `INotifyPropertyChanged` 是 WPF 内置接口
- 当属性变化时，触发 `PropertyChanged` 事件
- WPF 的数据绑定系统监听到这个事件，就会更新界面
- `SetProperty` 是封装好的辅助方法：值变了才触发事件

### 5.6 RelayCommand - 把按钮点击变成属性

WPF 中按钮通过 `ICommand` 绑定，而不是传统的事件处理。

看 [RelayCommand.cs](file:///d:/Dataset_management/src/DatasetManager.App/ViewModels/RelayCommand.cs)：

```csharp
public sealed class RelayCommand(Action execute, Func<bool>? canExecute = null) : ICommand
{
    public bool CanExecute(object? parameter) => canExecute?.Invoke() ?? true;
    public void Execute(object? parameter) => execute();
}
```

**使用方式（MainViewModel.cs）：**
```csharp
// 定义命令
public RelayCommand AddCommand { get; }

// 在构造函数中初始化
AddCommand = new RelayCommand(AddDataset);

// 实际执行的方法
private void AddDataset() { /* 添加数据集的逻辑 */ }
```

XAML 中绑定：
```xml
<Button Content="添加" Command="{Binding AddCommand}"/>
```

`AsyncRelayCommand` 是异步版本，防止执行耗时操作时界面卡死。

---

## 6. 第三阶段：项目架构深度解析（2-3周）

> 目标：理解整个项目的代码组织和数据流。

### 6.1 解决方案分层

```
src/
├── DatasetManager.Core/          ← 核心层（不依赖WPF）
│   ├── Models/                   ← 数据模型
│   │   ├── DatasetRecord.cs      ← 数据集记录
│   │   ├── DatasetType.cs        ← 数据集类型枚举
│   │   ├── WeightRecord.cs       ← 权重记录
│   │   └── YoloOperationRecord.cs← YOLO操作记录
│   └── Services/                 ← 业务服务
│       ├── IDatasetRepository.cs      ← 仓储接口
│       ├── JsonDatasetRepository.cs   ← JSON实现
│       ├── DatasetScanner.cs          ← 图片目录扫描
│       ├── LabelMeAnnotationScanner.cs← LabelMe标注扫描
│       ├── LabelMeToYoloConverter.cs  ← 格式转换
│       └── ...
│
├── DatasetManager.App/           ← WPF应用层
│   ├── ViewModels/               ← 视图模型
│   │   ├── MainViewModel.cs      ← 主窗口VM（最核心）
│   │   ├── DatasetItemViewModel.cs
│   │   ├── ObservableObject.cs
│   │   └── RelayCommand.cs
│   ├── Views/                    ← 窗口和用户控件
│   │   ├── DatasetEditorWindow.xaml
│   │   ├── AnnotationEditorWindow.xaml
│   │   ├── YoloProcessingView.xaml
│   │   └── ...
│   ├── App.xaml                  ← 应用程序入口
│   └── MainWindow.xaml           ← 主窗口
│
└── DatasetManager.Native/        ← C++互操作层
    └── NativeImageService.cs     ← P/Invoke封装
```

**分层原则：**
- **Core 层**不依赖 WPF，可以单元测试
- **App 层**引用 Core 层，处理界面
- **Native 层**封装 C++ DLL 调用

### 6.2 核心数据流：添加一个数据集

以"添加原始数据集"为例，走一遍完整流程：

```
1. 用户点击"＋ 添加数据集"按钮
       ↓
2. Button 触发 AddCommand (RelayCommand)
       ↓
3. MainViewModel.AddDataset() 执行
       ↓
4. 弹出 DatasetEditorWindow 对话框
       ↓
5. 用户填写名称、路径，点击确定
       ↓
6. 创建 DatasetRecord 对象
   ├── Id = Guid.NewGuid()
   ├── Name = 用户输入
   ├── Type = DatasetType.Raw
   └── ChangeHistory 添加一条记录
       ↓
7. 加入内存集合 _models
       ↓
8. RebuildItems() 刷新界面列表
       ↓
9. SaveAndScanAsync() 异步执行：
   ├── _repository.SaveAsync() → 写入 catalog.json
   └── _scanner.ScanAsync() → 扫描图片目录
       ↓
10. 扫描完成，更新 Statistics，再次保存
       ↓
11. 界面自动刷新显示统计信息
```

### 6.3 关键类导读

#### DatasetRecord（数据集记录）

[文件链接](file:///d:/Dataset_management/src/DatasetManager.Core/Models/DatasetRecord.cs)

这是项目中最重要的模型类，每个属性的含义：

| 属性 | 类型 | 含义 |
|------|------|------|
| `Id` | `Guid` | 全局唯一ID |
| `Name` | `string` | 数据集名称（如 `Corona_2026_0516_aaa`） |
| `Type` | `DatasetType` | 类型：Raw/Processed/Created/Training/Test/Validation |
| `RootPath` | `string` | 数据集根目录的绝对路径 |
| `ParentDatasetId` | `Guid?` | 来源数据集ID（可空，原始数据集没有父级） |
| `Notes` | `string` | 用户备注 |
| `CreatedAt` | `DateTimeOffset` | 创建时间 |
| `UpdatedAt` | `DateTimeOffset` | 最后更新时间 |
| `ChangeHistory` | `List<DatasetChangeEntry>` | 修改历史记录 |
| `AnnotationSets` | `List<AnnotationSetRecord>` | 标注批次列表 |
| `Statistics` | `DatasetStatistics` | 图片统计信息 |

#### MainViewModel（主窗口视图模型）

[文件链接](file:///d:/Dataset_management/src/DatasetManager.App/ViewModels/MainViewModel.cs)

这是最大、最核心的类，500+行。它负责：
- 持有所有数据集的内存集合 `_models`
- 处理左侧导航切换（全部/原始/已处理/YOLO/权重）
- 处理增删改查命令
- 管理搜索和筛选
- 协调扫描和保存

**建议阅读顺序：**
1. 先看字段和构造函数（1-48行）
2. 再看属性（SelectedDataset、SearchText 等）
3. 然后看命令方法（AddDataset、EditDataset、RemoveDatasetAsync）
4. 最后看辅助方法（RebuildItems、FilterDataset）

#### JsonDatasetRepository（JSON 持久化）

[文件链接](file:///d:/Dataset_management/src/DatasetManager.Core/Services/JsonDatasetRepository.cs)

负责把数据集列表保存到 JSON 文件，以及从文件加载。数据存储在：
```
%LOCALAPPDATA%\DatasetManager\catalog.json
```

使用 `System.Text.Json` 进行序列化，零外部依赖。

#### DatasetScanner（目录扫描器）

[文件链接](file:///d:/Dataset_management/src/DatasetManager.Core/Services/DatasetScanner.cs)

递归扫描目录下的图片文件，统计：
- 图片总数
- 各子目录的图片格式和数量
- 分辨率、位深、通道数（通过 OpenCV）

### 6.4 数据集类型系统

查看 [DatasetType.cs](file:///d:/Dataset_management/src/DatasetManager.Core/Models/DatasetType.cs)：

```csharp
public enum DatasetType
{
    Raw,         // 原始数据集（只有图片）
    Processed,   // 已处理数据集（图片+LabelMe标注）
    Created,     // 工具复制创建的数据集
    Training,    // 训练集清单
    Test,        // 测试集清单
    Validation   // 验证集清单
}
```

**数据集之间的关系：**
```
原始数据集 (Raw)
    ↑ 关联自
已处理数据集 (Processed) ← 可以有多个标注批次
    ↑ 选取配对组成
训练集/测试集/验证集 (Training/Test/Validation) ← JSON清单
    ↑ 读取清单复制文件
创建数据集 (Created) ← 实际复制出的独立目录
```

---

## 7. 第四阶段：C++/OpenCV 互操作（1-2周）

> 目标：理解 C# 如何调用 C++ 代码。

### 7.1 为什么需要 C++？

C# 完全可以读图片尺寸，但项目用 C++ + OpenCV 有几个考虑：
1. OpenCV 在 C++ 生态更成熟
2. 以后可能扩展更复杂的图像处理
3. 演示 C# 和 C++ 混合编程的架构

### 7.2 C ABI - 两种语言的桥梁

C# 和 C++ 不能直接互相调用，需要通过 **C ABI**（应用程序二进制接口）。

看 C++ 头文件 [dataset_manager_native.h](file:///d:/Dataset_management/native/include/dataset_manager_native.h)：

```c
extern "C"  // 告诉编译器用C链接方式，不要C++名称修饰
{
    // 获取版本字符串
    DM_API const char* dm_get_version() noexcept;
    
    // 获取图片信息
    DM_API int dm_get_image_info(const wchar_t* path, DmImageInfo* result) noexcept;
}
```

**`extern "C"` 的作用：** C++ 编译时会修改函数名（叫 name mangling），`extern "C"` 保持函数名不变，这样 C# 才能找到。

### 7.3 P/Invoke - C# 调用 DLL

看 [NativeImageService.cs](file:///d:/Dataset_management/src/DatasetManager.Native/NativeImageService.cs)：

```csharp
private static class NativeMethods
{
    private const string LibraryName = "DatasetManager.OpenCvNative.dll";

    // 声明要调用的C函数
    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl)]
    internal static extern IntPtr dm_get_version();

    [DllImport(LibraryName, CallingConvention = CallingConvention.Cdecl, 
        CharSet = CharSet.Unicode)]
    internal static extern int dm_get_image_info(string path, out ImageInfo info);
}

// C# 侧的数据结构，内存布局必须和C++一致
[StructLayout(LayoutKind.Sequential)]
public struct ImageInfo
{
    public int Width;
    public int Height;
    public int BitDepth;
    public int Channels;
}
```

**关键点：**
- `[DllImport]` 标记要从 DLL 导入的函数
- `CallingConvention.Cdecl` 对应 C 的调用约定
- `[StructLayout(LayoutKind.Sequential)]` 保证结构体字段顺序和 C++ 一致
- `string` 对应 C 的 `wchar_t*`（Unicode 字符串）
- `out ImageInfo` 对应 C 的指针参数

### 7.4 C++ 实现

[dataset_manager_native.cpp](file:///d:/Dataset_management/native/src/dataset_manager_native.cpp) 使用 OpenCV 读取图片：

```cpp
int dm_get_image_info(const wchar_t* path, DmImageInfo* result) noexcept
{
    try
    {
        cv::Mat img = cv::imread(/* 将wchar_t*转为cv::String */);
        if (img.empty()) return 1;  // 读取失败
        
        result->width = img.cols;
        result->height = img.rows;
        result->channels = img.channels();
        result->bit_depth = img.elemSize1() * 8;
        return 0;  // 成功
    }
    catch (...) { return 2; }  // 异常
}
```

### 7.5 构建流程

[CMakeLists.txt](file:///d:/Dataset_management/CMakeLists.txt) 是总构建入口：

1. CMake 先编译 `native/` 下的 C++ 代码，生成 `DatasetManager.OpenCvNative.dll`
2. 然后调用 `dotnet build` 编译 C# WPF 项目
3. 把 DLL 和 OpenCV 依赖复制到统一输出目录 `bin/Release/`

---

## 8. 第五阶段：动手实践

### 8.1 入门任务：修改界面文字

1. 打开 [MainWindow.xaml](file:///d:/Dataset_management/src/DatasetManager.App/MainWindow.xaml)
2. 找到左侧导航的"数据集管理"标题
3. 改成你喜欢的文字，比如"我的数据集管家"
4. 重新运行 `dotnet run --project src/DatasetManager.App`，看效果

### 8.2 小任务：给数据集加一个新字段

给 `DatasetRecord` 添加一个 `Priority`（优先级）字段：

1. 在 [DatasetRecord.cs](file:///d:/Dataset_management/src/DatasetManager.Core/Models/DatasetRecord.cs) 添加属性
2. 在 [DatasetEditorWindow.xaml](file:///d:/Dataset_management/src/DatasetManager.App/Views/DatasetEditorWindow.xaml) 添加输入框
3. 在 MainViewModel 中确保保存和加载正常

### 8.3 中等任务：添加排序功能

在数据集列表添加按名称/日期排序的功能：
1. 在 MainViewModel 添加 `SortOption` 属性
2. 修改 `ApplyDatasetSort()` 方法
3. 在 XAML 添加 ComboBox 让用户选择

### 8.4 进阶任务：实现数据集导出

添加一个功能，把当前数据集列表导出为 CSV 文件：
1. 在 Core 层写一个 `CsvExportService`
2. 在 App 层添加导出命令和按钮
3. 使用 `SaveFileDialog` 让用户选择保存位置

---

## 9. 项目代码逐文件导读

按学习优先级排序：

### 必读文件（理解核心）

| 顺序 | 文件 | 知识点 |
|------|------|--------|
| 1 | [ObservableObject.cs](file:///d:/Dataset_management/src/DatasetManager.App/ViewModels/ObservableObject.cs) | INotifyPropertyChanged、MVVM 基础 |
| 2 | [RelayCommand.cs](file:///d:/Dataset_management/src/DatasetManager.App/ViewModels/RelayCommand.cs) | ICommand、异步命令 |
| 3 | [DatasetType.cs](file:///d:/Dataset_management/src/DatasetManager.Core/Models/DatasetType.cs) | 枚举 |
| 4 | [DatasetRecord.cs](file:///d:/Dataset_management/src/DatasetManager.Core/Models/DatasetRecord.cs) | 模型设计、属性、集合 |
| 5 | [MainViewModel.cs](file:///d:/Dataset_management/src/DatasetManager.App/ViewModels/MainViewModel.cs) | 完整的 ViewModel 示例 |
| 6 | [MainWindow.xaml](file:///d:/Dataset_management/src/DatasetManager.App/MainWindow.xaml) | XAML 布局、数据绑定 |
| 7 | [IDatasetRepository.cs](file:///d:/Dataset_management/src/DatasetManager.Core/Services/IDatasetRepository.cs) | 接口设计 |
| 8 | [JsonDatasetRepository.cs](file:///d:/Dataset_management/src/DatasetManager.Core/Services/JsonDatasetRepository.cs) | JSON 序列化 |

### 进阶阅读（特定功能）

| 文件 | 功能 |
|------|------|
| [DatasetScanner.cs](file:///d:/Dataset_management/src/DatasetManager.Core/Services/DatasetScanner.cs) | 递归目录扫描 |
| [LabelMeAnnotationScanner.cs](file:///d:/Dataset_management/src/DatasetManager.Core/Services/LabelMeAnnotationScanner.cs) | 解析 LabelMe JSON |
| [LabelMeToYoloConverter.cs](file:///d:/Dataset_management/src/DatasetManager.Core/Services/LabelMeToYoloConverter.cs) | 坐标转换算法 |
| [CompositeDatasetService.cs](file:///d:/Dataset_management/src/DatasetManager.Core/Services/CompositeDatasetService.cs) | 多来源数据合并 |
| [NativeImageService.cs](file:///d:/Dataset_management/src/DatasetManager.Native/NativeImageService.cs) | P/Invoke |

### C++ 部分

| 文件 | 内容 |
|------|------|
| [dataset_manager_native.h](file:///d:/Dataset_management/native/include/dataset_manager_native.h) | C ABI 接口定义 |
| [dataset_manager_native.cpp](file:///d:/Dataset_management/native/src/dataset_manager_native.cpp) | OpenCV 实现 |
| [CMakeLists.txt](file:///d:/Dataset_management/native/CMakeLists.txt) | CMake 构建配置 |

---

## 10. 常见问题与学习资源

### 10.1 新手常见疑问

**Q: WPF 和 WinForms 有什么区别？**
A: WPF 使用 XAML 声明式界面和强大的数据绑定，更适合 MVVM 模式；WinForms 是更老的拖放式技术。新项目推荐 WPF。

**Q: 为什么用 .NET 8 而不是 .NET Framework？**
A: .NET 8 是微软最新的跨平台版本（虽然 WPF 仍只支持 Windows），性能更好，语法更新。

**Q: MVVM 一定要用吗？**
A: 不是必须，但在 WPF 中使用 MVVM 代码更清晰、可测试。这个项目是 MVVM 的标准实践。

**Q: C# 能调用 C++，那 C++ 能调用 C# 吗？**
A: 可以但更复杂（需要 C++/CLI 或 COM）。本项目只需要 C# 调 C++，用 P/Invoke 最简单。

**Q: 为什么用 JSON 不用数据库？**
A: 项目文档说了，初版保持零依赖。数据量不大时 JSON 足够，以后可以通过替换 `IDatasetRepository` 实现换成 SQLite。

**Q: XAML 中的 `StaticResource` 和 `DynamicResource` 有什么区别？**
A: StaticResource 在编译时查找，DynamicResource 在运行时查找。项目中颜色画刷用 StaticResource。

### 10.2 调试技巧

1. **在关键位置加断点**：点击行号左边，按 F5 调试运行
2. **查看变量**：鼠标悬停在变量上，或使用"局部变量"窗口
3. **输出日志**：使用 `Debug.WriteLine("信息")`
4. **查看 JSON 数据**：在资源管理器输入 `%LOCALAPPDATA%\DatasetManager`

### 10.3 推荐学习资源

**C# 基础**
- 微软官方教程：https://learn.microsoft.com/zh-cn/dotnet/csharp/tour-of-csharp/
- B站：".NET 官方入门教程"

**WPF/MVVM**
- 微软 WPF 文档：https://learn.microsoft.com/zh-cn/dotnet/desktop/wpf/
- 《WPF 编程》（Charles Petzold 著）

**C++/OpenCV**
- OpenCV 官方教程：https://docs.opencv.org/4.5.3/
- B站："OpenCV 4 入门教程"

**设计模式**
- 《Head First 设计模式》（中文版）
- 特别关注：观察者模式（对应 INotifyPropertyChanged）、命令模式（对应 ICommand）、策略模式（对应接口）

### 10.4 学习路线总结图

```
Week 1-2: C# 基础
    │
    ▼
Week 3: WPF 基础 + XAML
    │
    ▼
Week 4: MVVM 模式 + 数据绑定
    │
    ▼
Week 5-6: 阅读项目代码
    ├── Models 层
    ├── Services 层
    ├── ViewModels 层
    └── Views 层
    │
    ▼
Week 7: C++/OpenCV/P-Invoke
    │
    ▼
Week 8+: 动手改代码、加功能
```

---

## 最后的话

这个项目虽然不大，但涵盖了一个真实 Windows 桌面应用的方方面面：
- 清晰的分层架构
- MVVM 设计模式
- 文件持久化
- 异步编程
- 多语言互操作
- 实际业务逻辑

不要试图一次看懂所有代码。**先跑起来，再从界面操作反推代码**，遇到不懂的语法就查文档。跟着上面的路线，2个月左右你应该能独立修改和扩展这个项目了。

祝你学习顺利！🚀
