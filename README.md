# ServerS4A12 管理工具包

[AGPL v3.0] [.NET 10.0] [Windows 10/11]

---

## 项目简介

为 ServerS4A12 服务端模拟器打造的全功能管理套件，专为非技术用户设计，无需了解 .NET SDK、PowerShell 或服务端架构，即可轻松完成：

- **同步更新**：一键增量/全量更新，自动拉取最新源码并编译
- **存档管理**：切换/改名/导入/导出/备份，支持拖拽换挡
- **DX11/DX12渲染**：一键切换补丁，提升游戏流畅度，支持去水印
- **GM工具**：集成官方同步更新版本
- **服务端控制**：启停管理 + PVF状态检测
- **环境检测**：自动检测并安装 .NET SDK

---

## 许可证

采用 **AGPL v3.0** 许可。

- 个人及非商业用途完全免费
- 修改后的版本须以 AGPL v3.0 开源发布
- 商业使用需获得书面授权

---

## 核心特性

| 功能 | 说明 |
|------|------|
| 增量更新 | 快速同步最新代码 |
| 全量更新 | 首次部署或强制同步 |
| 存档管理 | 左键换挡 / 右键改名 / 拖拽导入 / 备份上限10个 |
| DX11/DX12补丁 | 互斥勾选，自动复制/删除补丁文件，支持去水印 |
| 一键安装SDK | 三级检测（PATH → Program Files → 本地），调用官方脚本 |
| 深色主题GUI | WinForms自适应布局，抗拉伸 |
| 双版本交付 | 依赖版 ~376KB / 便携版 ~110.8MB（自包含.NET运行时） |

---

## 目录结构

```
ServerS4A12-管理工具包/
├── 开始游戏-ServerUI.exe          # 依赖版入口
├── 本地游戏S4.bat                 # 游戏启动脚本
├── AUM管理组件\
│   ├── update.ps1                 # 增量/全量更新脚本
│   ├── save-quick.ps1             # 快速换挡脚本
│   ├── save-switch.ps1            # 存档管理脚本
│   ├── dotnet-install.ps1         # .NET SDK官方安装器
│   ├── ServerS4A12-AUM\           # 服务端源码
│   ├── dfogmtool\                 # GM工具源码
│   ├── DX11补丁\                  # DX11渲染补丁
│   ├── DX12补丁\                  # DX12渲染补丁
│   ├── ServerUI-无依赖版.exe      # 便携版
│   ├── ServerUI-有依赖版.exe      # 依赖版
│   ├── ServerUI-源码.zip          # C#源码
│   └── 存档管理\                  # 切换库 + 备份存档
└── README.md
```

---

## 快速开始

### 系统要求

| 项目 | 要求 |
|------|------|
| 操作系统 | Windows 10 (Build 19041+) / Windows 11 |
| .NET运行时 | 自动检测安装（需联网） |
| 磁盘空间 | 约2GB |
| 网络 | 首次更新需联网拉取源码 |

### 使用步骤

1. 解压到游戏主目录
2. 双击 `开始游戏-ServerUI.exe`
3. 点击「检测SDK」→ 如未安装则「安装SDK」
4. 点击「全量更新」拉取完整源码并编译
5. 可选勾选 DX11/DX12 补丁
6. 点击「开始游戏」启动服务端并进入游戏

---

## 技术栈

| 层级 | 技术 |
|------|------|
| 服务端 | C# (.NET 10.0) |
| 图形界面 | WinForms (.NET 10.0-windows) |
| 脚本引擎 | PowerShell 5.1 |
| 启动器 | Batch (UTF-8) |
| 仓库API | Codeberg REST API |
| 数据库 | SQLite |

---

## 开发与构建

### 源码结构

```
Program.cs                     // 程序入口
MainForm.cs                    // UI布局 + 业务逻辑
Models/ArchiveEntry.cs         // 存档数据模型
Services/
  ├── ArchiveService.cs        // 存档管理
  ├── ServerService.cs         // 服务端启停
  └── UpdateService.cs         // 调用更新脚本
```

### 本地编译

```powershell
# 依赖版 (~376KB)
dotnet publish -c Release -r win-x64 --no-self-contained -p:PublishSingleFile=true -o ./publish-deps

# 便携版 (~110.8MB)
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -o ./publish-self
```

---

## 常见问题

**Q: 首次使用需要联网吗？**  
A: 需要，用于下载服务端源码和 .NET SDK。

**Q: 便携版和依赖版区别？**  
A: 便携版自包含.NET运行时（~110MB），适合无环境系统；依赖版（~376KB）需系统已安装.NET 10.0 SDK。

**Q: 更新失败怎么办？**  
A: 检查网络和防火墙，确保 Codeberg 可访问，或尝试「全量更新」。

---

## 许可证声明

本软件采用 **AGPL v3.0** 开源。

- 个人和非商业用途免费
- 商业使用需获得书面授权

---

Copyright (c) 2026 ServerS4A12 Manager Contributors
