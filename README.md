
---

# ServerS4A12 管理工具包 

[AGPL v3.0] [.NET 10.0] [Windows 10/11] [v1.55]

---

## 项目简介

ServerS4A12 管理工具包是为 2D 横版游戏服务端模拟器 ServerS4A12 量身打造的全功能管理套件。专为非技术用户设计，让您无需了解 .NET SDK、PowerShell 脚本或服务端架构，即可轻松完成：

- 一键增量/全量更新（自动拉取最新服务端源码并编译）
- 智能存档管理（切换/改名/导入/导出/备份，支持拖拽换挡）
- DX11/DX12 渲染补丁切换（含去水印选项）
- 服务端启停控制（PVF 状态检测 + 进程管理）
- .NET SDK 自动检测与安装（三级检测链 + 一键安装）

---

## 许可证声明

本软件采用 **GNU Affero General Public License v3.0 (AGPL v3.0)** 授权。

### 您可以自由地：
- 使用、复制、分发本软件
- 修改源代码
- 将本软件用于个人或非商业目的

### 您必须遵守：
- 任何修改后的版本必须以 AGPL v3.0 或更高版本开源发布
- 通过网络提供服务时，用户有权获取完整的对应源代码
- 保留原始版权声明和许可证信息

### 商业使用特别说明：
本软件对个人和非商业用途完全免费。如有商业使用需求（包括但不限于集成到商业产品、提供商业服务、作为商业项目的一部分等），必须**另行获得书面授权并支付授权费用**。商业授权请联系项目维护者。

> 本许可证不构成法律建议。如有疑问，请咨询专业法律人士。

---

## 核心特性

| 功能模块 | 说明 |
|---------|------|
| 增量更新 | 快速同步最新代码 |
| 全量更新 | 拉取全部历史，适合首次部署或强制同步 |
| 存档管理 | 左键换挡 / 右键改名 / 拖拽导入 / 备份上限 10 个 |
| DX11/DX12 补丁 | 互斥勾选，自动复制/删除补丁文件，支持去水印 |
| 一键 SDK 安装 | 三级检测（PATH -> Program Files -> 本地），调用官方脚本 |
| 深色主题 GUI | WinForms 自适应布局，TableLayoutPanel 抗拉伸 |
| 双版本交付 | 依赖版 ~376KB / 便携版 ~110.8MB（自包含 .NET 运行时） |

---

## 目录结构

```
ServerS4A12-管理工具包-v1.55/
├── 开始游戏-ServerUI.exe          # 依赖版入口 (376KB)
├── 本地游戏S4.bat                  # 游戏启动脚本
├── 项目状态快照-v1.55.md           # 完整技术文档
├── AUM管理组件\                    # 核心组件目录
│   ├── update.ps1                  # 增量/全量更新脚本 (369行)
│   ├── save-quick.ps1              # 快速换挡脚本
│   ├── save-switch.ps1             # 存档管理脚本
│   ├── dotnet-install.ps1          # .NET SDK 官方安装器
│   ├── ServerS4A12-AUM\            # 服务端源码 (Server/Tool/Patch)
│   ├── DX11补丁\                   # DX11 渲染补丁 (含无水印版)
│   ├── DX12补丁\                   # DX12 渲染补丁 (含无水印版)
│   ├── ServerUI-无依赖版.exe       # 便携版 (110.8MB)
│   ├── ServerUI-有依赖版.exe       # 依赖版 (376KB)
│   ├── ServerUI-源码.zip           # C# 源码 (~17KB)
│   └── 存档管理\                   # 切换库 + 备份存档
└── README.md                       # 本说明文档
```

---

## 快速开始

### 系统要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10 (Build 19041+) 或 Windows 11 (Build 22000+) |
| .NET 运行时 | 自动检测，如无则一键安装（需联网） |
| 磁盘空间 | 约 500MB（含服务端源码和编译产物） |
| 网络 | 首次更新需联网拉取服务端源码 |

### 使用步骤

1. 解压 `ServerS4A12-管理工具包-v1.55.zip` 到游戏主目录
2. 双击 `开始游戏-ServerUI.exe` 启动 GUI
3. 首次使用点击「检测 SDK」-> 如未安装则点击「安装 SDK」
4. 点击「全量更新」拉取完整服务端源码并自动编译
5. 在「开始游戏」面板勾选 DX11/DX12 补丁（可选）
6. 点击「开始游戏」启动服务端并进入游戏

---

## 技术栈

| 层级 | 技术 | 版本 |
|------|------|------|
| 服务端 | C# (.NET) | net10.0 |
| 图形界面 | WinForms | net10.0-windows，自包含单文件 |
| 脚本引擎 | PowerShell | 5.1 (Windows 自带) |
| 启动器 | Batch | chcp 65001 UTF-8 |
| 仓库 API | Codeberg REST API | /commits + /compare/ |
| 数据库 | SQLite | inventory.db |
| .NET SDK | 便携安装 | 10.0.301 (通过官方脚本) |

---

## 下载

| 版本 | 大小 | 说明 |
|------|------|------|
| 管理工具包 v1.55 | ~359MB | 完整分发包（含源码 + 双版本 EXE） |

> 便携版已自包含 .NET 运行时，无需额外安装；依赖版需系统已安装 .NET 10.0 SDK。

---

## 开发与构建

### 源码结构

```
// 核心源码文件（位于 ServerUI-源码.zip）
Program.cs                     // 程序入口，STAThread
MainForm.cs                    // ~462行，UI布局 + 所有业务逻辑
Models/
  └── ArchiveEntry.cs          // 存档数据模型
Services/
  ├── ArchiveService.cs        // 存档管理 (切换/备份/导入/导出)
  ├── ServerService.cs         // 服务端启停 + PVF 检测
  └── UpdateService.cs         // 调用 update.ps1
ServerUI.csproj                // 项目配置，win-x64
```

### 本地编译

```powershell
# 依赖版 (~376KB)
dotnet publish ServerUI.csproj -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true -p:DebugType=none -p:DebugSymbols=false -o ./publish-deps

# 便携版 (~110.8MB)
dotnet publish ServerUI.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:DebugType=none -p:DebugSymbols=false -o ./publish-self
```

### 开发环境要求

- .NET 10.0 SDK
- Visual Studio 2022 或更高版本（可选）
- Windows 10/11 操作系统

---

## 贡献指南

欢迎提交 Issue 和 Pull Request。请确保：

- 代码风格与现有保持一致
- 所有 .ps1 脚本包含中文注释
- 不修改 `本地游戏S4.bat` 和存档管理核心逻辑
- 任何新增功能或修改需附带说明文档
- 遵守 AGPL v3.0 许可证要求

---

## 常见问题

### Q: 首次使用需要联网吗？
A: 首次使用需要联网下载服务端源码和 .NET SDK（如未安装）。后续增量更新只需少量网络流量。

### Q: 便携版和依赖版有什么区别？
A: 便携版自包含 .NET 运行时，约 110.8MB，适合无 .NET 环境的系统；依赖版约 376KB，需系统已安装 .NET 10.0 SDK，启动更快。

### Q: 存档保存在哪里？
A: 存档文件位于 `ServerS4A12-AUM\DfoServer\Script\Lib\inventory.db`，工具包内的「存档管理」功能可对其进行切换和备份。

### Q: 更新失败怎么办？
A: 检查网络连接，确保 Codeberg 仓库可访问。如持续失败，可尝试使用「全量更新」或检查防火墙设置。

---

## 联系方式

- 项目主页：https://github.com/qq1229037592/ServerUI-AUM/
- 问题反馈：https://github.com/qq1229037592/ServerUI-AUM/
- 商业授权咨询：请通过 Issue 联系项目维护者

---

## 致谢

- .NET 基金会提供优秀的开发框架
- Codeberg 提供代码托管服务
- ServerS4A12 社区用户的反馈与支持

---

Copyright (c) 2026 ServerS4A12 Manager Contributors

本软件使用 AGPL v3.0 许可证开源。如需商业使用，请另行获得书面授权。
