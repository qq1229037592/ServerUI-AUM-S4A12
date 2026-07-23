/*
 * ==================================================================
 *     主窗口类 (MainForm.cs) — ServerS4A12 GUI 管理器 v1.917
 * ==================================================================
 *
 * 【功能概览】
 *   本文件是整个管理器的核心，包含：
 *     1. UI 布局           — 所有按钮、标签、列表、日志区域的创建和排列
 *     2. 按钮事件处理      — 开始游戏 / 停止 / 重启 / 更新 / 存档管理等
 *     3. 存档管理逻辑      — 切换 / 备份 / 导入 / 导出 / 拖拽换挡
 *     4. 服务端更新编排    — 调用 UpdateService 执行增量/全量更新
 *     5. 窗口自适应缩放    — 字体和控件随窗口大小等比缩放
 *     6. 日志系统          — 带时间戳 + 颜色标注的实时日志输出
 *
 * 【新手修改指南】
 *   ┌──────────────────────────────────────────────────────────────┐
 *   │ 想改什么？                          → 去哪个位置？           │
 *   ├──────────────────────────────────────────────────────────────┤
 *   │ 改窗口标题                           → 构造函数 Text 属性    │
 *   │ 改默认窗口大小                       → 构造函数 Size 属性    │
 *   │ 改版本号                             → VER 常量              │
 *   │ 改按钮颜色                           → 配色方案中的常量       │
 *   │ 改字体大小                           → B() / L() 工厂方法     │
 *   │ 加新按钮                             → Build() 方法中对应区域 │
 *   │ 改更新脚本参数                       → RI() / RF() 方法       │
 *   │ 改存档备份数量上限                   → MB 常量               │
 *   │ 修改 DX 补丁文件列表                 → Cd() 中的 files[]     │
 *   │ 改启动后隐藏窗口的延迟               → Play() 中的 10000ms   │
 *   └──────────────────────────────────────────────────────────────┘
 *
 * 【代码组织】
 *   行   1- 74   字段声明 + 构造函数
 *   行  76-146   辅助方法 (加载图标 / 字体缩放 / 双缓冲)
 *   行 148-149   UI 工厂方法 (B = 创建按钮, L = 创建标签)
 *   行 151-303   Build() — 全部 UI 布局 (约 150 行)
 *   行 305-306   定时器初始化 (Ti)
 *   行 307-317   启动/停止核心逻辑 (Fc / Go / Play / Lg)
 *   行 319-374   存档管理 (IA / EC / SC / Am / Ao / TB / CleanRedundantDb / DoSafeSwap / SL / De / Dd)
 *   行 376-454   系统检测 (Ck — .NET SDK / Windows 版本 / 便携版检测)
 *   行 456-511   DX 补丁管理 (Cd)
 *   行 513-552   .NET SDK 安装 (IS)
 *   行 554-568   状态刷新 + 更新编排 (Rs / Rf / RA / RI / RF / OU / OD)
 * ==================================================================
 *
 * ==================================================================
     * 【v1.86 UI 重设计说明】
 * ==================================================================
     * 本次更新采用控制台仪表盘式布局：统一圆角卡片、标题强调线、
     * 分层状态栏和紧凑操作区。保留原有深色科技蓝色卡与全部功能。
 *
 * 【设计要点】
 *   1. 更柔和的深色背景 (#1e1e2e)，减轻视觉疲劳
 *   2. 卡片式区块设计，使用浅色边框区分不同功能区域
 *   3. 统一使用圆角扁平按钮，带悬停高亮效果
 *   4. 配色柔和且富有层次感：
 *      - 主色: 紫灰色系背景，蓝紫色强调
 *      - 成功: 柔和的绿色 (#a6e3a1)
 *      - 错误: 粉红色 (#f38ba8)
 *      - 警告: 橙黄色 (#fab387)
 *   5. 存档信息栏改为 FlowLayoutPanel，消除控件宽度耦合
 *   6. 【刷新】按钮更名为【刷新存档】，功能不变
 *   7. 所有新增 UI 代码均配有详细中文注释
 *
 * 【如果你觉得颜色不好看，想自己改】
 *   找到下面"配色方案"那一块，把所有 Color.FromArgb(...)
 *   的值改成你喜欢的颜色即可，不用担心改错。
 * ==================================================================
 */

using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using ServerUI.Services;

namespace ServerUI;

public partial class MainForm : Form
{
    // =================================================================
    // 配色方案 — 修改这里可以全局改变整个界面的颜色
    // Color.FromArgb(R, G, B) — R/G/B 取值范围 0-255
    //
    // 【v1.85-1 配色说明】
    // 以下颜色由用户指定，采用了科技蓝 + 深色主题的搭配：
    // =================================================================
    // Bg  = 主背景色 (#1E1E24 深邃极客黑灰)   Card = 区域卡片背景 (#26262E)
    // Txt = 主文字色 (#F0F0F5 接近纯白)        Txt2 = 次要文字 (#9E9EB0 淡灰)
    // Ac  = 科技蓝浅 (#007ACC 通用/更新按钮)   Ad  = 科技蓝深 (#0056B3 强调)
    // Gn  = 饱满森林绿 (#28A745 开始游戏)       Rd  = 警示红 (#DC3545 停止)
    // Or  = 活力橙 (#FD7E14 重启/警告)          Cy  = 青色 (日志日期行)
    static readonly Color Bg   = Color.FromArgb(30, 30, 36);  // 主背景: 深邃极客黑灰
    static readonly Color Card = Color.FromArgb(38, 38, 46);  // 区域卡片背景
    static readonly Color Txt  = Color.FromArgb(240, 240, 245); // 主文字: 接近纯白的灰白
    static readonly Color Txt2 = Color.FromArgb(158, 158, 176); // 次要文字: 淡淡的灰色
    static readonly Color Ac   = Color.FromArgb(0, 122, 204);   // 科技蓝浅 (#007ACC)
    static readonly Color Ad   = Color.FromArgb(0, 86, 179);    // 科技蓝深 (#0056B3)
    static readonly Color Gn   = Color.FromArgb(40, 167, 69);   // 饱满森林绿 (#28A745)
    static readonly Color Rd   = Color.FromArgb(220, 53, 69);   // 警示红 (#DC3545)
    static readonly Color Or   = Color.FromArgb(253, 126, 20);  // 活力橙 (#FD7E14)
    static readonly Color Cy   = Color.FromArgb(0, 190, 190);   // 青色 (日志日期行)

    // MB = 备份存档保留的最大数量 (当前 10 个)
    // 超过此数量时，最旧的备份会被自动删除
    // 想保留更多备份? 改大这个值即可，比如改成 20
    const int MB = 10;

    // VER = 当前工具版本号 — 显示在窗口标题和启动日志中
    // 每次发版时只需修改这一个值
    // 【v1.916】存档操作自动停服/重启
    const string VER = "1.917";

    // ===== 路径计算 =====
    // _bd = EXE 所在目录 (BaseDirectory)
    // _ad = AUM管理组件目录 (如果 EXE 在根目录，则 _ad = _bd\AUM管理组件)
    // _gr = 游戏根目录 (_ad 的上级目录)
    readonly string _bd = AppDomain.CurrentDomain.BaseDirectory;
    readonly string _ad;
    readonly string _gr;

    // ===== 服务实例 (Single Responsibility) =====
    // 每个 Service 只负责一类操作，MainForm 只管 UI 和协调
    // 想修改具体逻辑? 去对应的 Service 文件，不要在 MainForm 里改
    readonly ServerService  _sv = new();  // 服务端进程管理
    readonly ArchiveService _ar = new();  // 存档文件管理
    readonly UpdateService  _up = new();  // 更新脚本调用
    readonly SelfUpdateService _au = new(); // AUM管理器自更新
    readonly MirrorUploadService _mu = new(); // 镜像上传服务

    // ===== UI 控件字段 =====
    // 命名规则: lb=Label, bt=Button, cb=CheckBox, lv=ListView, rt=RichTextBox
    // 顶栏
    Label lbSt, lbVe, lbPv, lbLu, lbCu, lbBk, lbDr, lbSd;
    // 主要按钮 (开始/停止/重启/增量/全量)
    Button btPlay, btStop, btRe, btIn, btFu, btVL, btPv;
    // 顶栏按钮 (更新AUM)
    Button btAu;
    // 存档管理按钮 (7 个)
    Button btOD, btOB, btMD, btSC, btIm, btEx, btUd;
    // 日志工具栏按钮 (复制/清空) + 顶部安装 SDK 按钮 + GM 工具按钮
    Button btCp, btCl, btSdk, btGm;
    // 复选框 (DX11/DX12/去水印/清理冗余DB/跳过更新日志)
    CheckBox cbDx, cbDt, cbDw, cbCl, cbSkipLog, cbMirror;
    // 存档列表
    ListView lv;
    // 日志文本框
    RichTextBox rt;
    // 更新进度条
    ProgressBar pb;
    Label lbPg;

    // ===== 定时器 =====
    // _st = 状态刷新定时器 (每 2 秒检查一次服务端状态)
    // _pt = 进度条定时器 (更新时每隔 200ms 推进进度)
    // _ct = 启动确认定时器 (启动 3 秒后检查 bat 是否存活)
    Timer _st, _pt, _ct;

    // ===== 状态变量 =====
    int _pv;                              // 进度条当前值 (0-100)
    int _stepTarget;                      // 当前步骤的目标百分比 (由 OU 分析 [N/5] 设定)
    bool _sa = true;                      // 排序方向: true=正序, false=倒序
    bool _cdBusy;                         // DX 复选框互斥处理中的互斥锁
    bool _orphanLogged;                   // 孤儿进程告警只触发一次 (避免日志刷屏)
    bool _hasSdk;                         // .NET 10 SDK 是否可用 (影响自更新功能)
    bool _mirrorOk;                       // 镜像上传令牌是否有效
    readonly StringBuilder _logBuilder = new();  // 累积全部运行日志，用于退出时生成文件


    /*
     * 构造函数 — 程序启动时执行一次，完成所有初始化工作
     *
     * 执行顺序 (不能随意调换):
     *   1. 计算目录路径 (_ad / _gr)
     *   2. 加载窗口图标
     *   3. 设置窗口属性 (大小 / 位置 / 标题 / 背景色)
     *   4. 注册拖放和关闭事件
     *   5. Build() — 创建所有 UI 控件
     *   6. Ti()  — 初始化定时器
     *   7. 启用双缓冲 (减少闪烁)
     *   8. 捕获基准字体 (用于缩放)
     *   9. 设置窗口缩放和 DPI 事件
     *  10. 窗口加载后执行: 缩放字体 / 系统检测 / 刷新存档列表
     */
    public MainForm()
    {
        // 如果 EXE 在 AUM管理组件 的上级目录 (如 开始游戏-ServerUI.exe)，
        // 则 _ad = EXE目录\AUM管理组件，否则 _ad = EXE 所在目录
        _ad = Directory.Exists(Path.Combine(_bd, "AUM管理组件"))
            ? Path.Combine(_bd, "AUM管理组件")
            : _bd;
        _gr = Directory.GetParent(_ad)?.FullName ?? _ad;

        LoadWindowIcon();

        // 窗口基本属性
        AutoScaleMode = AutoScaleMode.Dpi;
        AutoScaleDimensions = new SizeF(96F, 96F);
        // This dashboard needs enough vertical room for the server controls and all DX options.
        MinimumSize = new Size(1000, 700);
        Size = new Size(1200, 820);           // 默认启动大小
        StartPosition = FormStartPosition.CenterScreen;  // 居中显示
        Text = "ServerS4A12 v" + VER;        // 窗口标题
        BackColor = Bg;
        Font = new Font("Microsoft YaHei", 10f);  // 默认字体

        // 拖放支持 — 让用户可以拖 .db 文件到窗口换挡
        AllowDrop = true;
        DragEnter += De;    // 拖入时检查是否是 .db 文件
        DragDrop += Dd;     // 放下时执行换挡操作
        FormClosing += Fc;  // 关闭前确认并清理进程

        // 创建界面 + 启动定时器
        Build();
        Ti();

        // 双缓冲 — 减少控件重绘闪烁
        DoubleBuffered = true;
        EnableDoubleBuffer(this);

        // Keep text at its designed DPI-aware size. Shrinking it during resize caused clipped labels.
        Load += async (s, e) => { Ck(); Rf(); CheckDnfExists(); await CheckBasicNetwork(); await CheckAUMUpdate(); };
    }

    // =================================================================
    // 辅助方法
    // =================================================================

    /*
     * 加载窗口图标
     * 优先级: 内嵌资源 (app.ico) → EXE 关联图标
     * 内嵌资源通过 .csproj 中的 <EmbeddedResource> 配置
     * 如果都失败，窗口将使用默认的 Windows 图标
     */
    void LoadWindowIcon()
    {
        try
        {
            // 尝试从嵌入资源加载 (ServerUI.csproj 中配置了 图标EXE.ico 作为 app.ico)
            using var s = System.Reflection.Assembly.GetExecutingAssembly()
                .GetManifestResourceStream("app.ico");
            if (s != null) { Icon = new Icon(s); return; }
        }
        catch { }
        try
        {
            // 兜底: 取 EXE 自身的图标
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                var ic = Icon.ExtractAssociatedIcon(exe);
                if (ic != null) Icon = ic;
            }
        }
        catch { }
    }

    /*
     * 递归启用双缓冲
     * 作用: 减少 TableLayoutPanel / Panel / GroupBox 的重绘闪烁
     * DoubleBuffered 是 protected 属性，只能用反射设置
     * 大部分控件不需要双缓冲，只为容器类控件启用
     */
    static void EnableDoubleBuffer(Control root)
    {
        var prop = typeof(Control).GetProperty("DoubleBuffered",
            System.Reflection.BindingFlags.Instance
            | System.Reflection.BindingFlags.NonPublic);
        foreach (Control c in root.Controls)
        {
            // v1.85-1: 新增 FlowLayoutPanel 支持 (存档信息栏使用)
            if (c is TableLayoutPanel || c is Panel || c is GroupBox || c is FlowLayoutPanel)
                try { prop?.SetValue(c, true); } catch { }
            EnableDoubleBuffer(c);
        }
    }

    /*
     * 按钮工厂方法 — 统一创建按钮 (v1.85-1 美化版)
     *
     * 【新特性】
     *   - 按钮带悬停高亮效果 (鼠标移上去颜色变亮)
     *   - 统一使用圆角扁平风格
     *   - 背景色更柔和，符合现代 UI 审美
     *
     * 参数:
     *   t   — 按钮文字
     *   bg  — 背景色 (使用 Bg/Ac/Gn/Rd/Or 等常量)
     *   fs  — 字体大小 (默认 10)
     *   bd  — 是否加粗 (默认 false)
     *
     * 【新手自己改样式】
     *   - 想改悬停颜色? 改下面的 hoverBg 变量
     *   - 想改按钮圆角? WinForms 原生不支持圆角，可搜索
     *     "WinForms 圆角按钮" 自行扩展
     *   - 想改所有按钮字体? 改 "Microsoft YaHei"
     *   - 想给按钮加图标? 设置 b.Image 属性
     */
    Button B(string t, Color bg, int fs = 10, bool bd = false)
    {
        var b = new Button
        {
            Text = t,
            FlatStyle = FlatStyle.Flat,
            BackColor = bg,
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei", fs,
                bd ? FontStyle.Bold : FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleCenter,
            Margin = new Padding(4),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(80, 80, 100) }
        };
        b.MinimumSize = new Size(60, 30);

        // ★ 悬停高亮效果 ★
        // 鼠标移入按钮时，让背景色稍微变亮（人机交互反馈）
        // 鼠标移出时恢复原来的颜色
        // 这样用户就知道"这个按钮可以点"，提升使用体验
        b.MouseEnter += (s, e) =>
        {
            // 计算更亮的颜色：把 RGB 每个分量往白色方向推一点
            int r = Math.Min(255, bg.R + 30);
            int g = Math.Min(255, bg.G + 30);
            int bl = Math.Min(255, bg.B + 30);
            b.BackColor = Color.FromArgb(r, g, bl);
        };
        b.MouseLeave += (s, e) =>
        {
            b.BackColor = bg; // 恢复原始颜色
        };

        return b;
    }

    // Reuses the existing color palette while giving each functional area a clear card boundary.
    Panel Section(string title)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            Padding = new Padding(12, 31, 12, 12),
            Margin = new Padding(5)
        };
        card.Resize += (s, e) => card.Invalidate();
        card.Paint += (s, e) =>
        {
            var bounds = card.ClientRectangle;
            if (bounds.Width < 2 || bounds.Height < 2) return;
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using var border = new Pen(Color.FromArgb(78, 78, 90));
            using var path = RoundedPath(new Rectangle(0, 0, bounds.Width - 1, bounds.Height - 1), 10);
            e.Graphics.DrawPath(border, path);
            using var accent = new SolidBrush(Ac);
            e.Graphics.FillRectangle(accent, 12, 23, Math.Min(42, Math.Max(0, bounds.Width - 24)), 2);
        };

        var heading = L(title, Txt);
        heading.Font = new Font("Microsoft YaHei", 9f, FontStyle.Bold);
        heading.Location = new Point(12, 7);
        heading.Anchor = AnchorStyles.Top | AnchorStyles.Left;
        card.Controls.Add(heading);
        return card;
    }

    static GraphicsPath RoundedPath(Rectangle bounds, int radius)
    {
        var path = new GraphicsPath();
        int diameter = Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height));
        if (diameter <= 1) { path.AddRectangle(bounds); return path; }
        var arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
        path.AddArc(arc, 180, 90);
        arc.X = bounds.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = bounds.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = bounds.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    /*
     * 标签工厂方法 — 统一创建标签
     *
     * 参数:
     *   t — 标签文字
     *   c — 文字颜色
     *
     * 所有标签背景透明，自动适应文字宽度
     */
    Label L(string t, Color c) => new Label
    {
        Text = t,
        ForeColor = c,
        AutoSize = true,
        TextAlign = ContentAlignment.MiddleLeft,
        BackColor = Color.Transparent
    };

    // =================================================================
    // UI 布局 (Build)
    // =================================================================
    // 整体结构:
    //   root (4 行)
    //     r0 — 顶栏 (运行状态 / 版本 / .NET SDK / 安装SDK)
    //     r1 (2 列)
    //       left (3 行) — 开始游戏 / 快速操作 / 更新管理
    //       ga   (4 行) — 存档管理 (信息栏 / 按钮栏 / 列表 / 拖拽区)
    //     lp — 日志区域 (RichTextBox + 进度条 + 清空/复制按钮)
    //     r3 — 底部链接栏 (GM工具 / 仓库链接)
    // =================================================================

    void Build()
    {
        // ============================================================
        // ★ root 根布局 ★ — 整个窗口最外层的容器
        // 使用 TableLayoutPanel 分 4 行排列所有区域
        // 行结构: 顶栏 | 主区域(左右两列) | 日志区域 | 底部链接栏
        // 修改建议: 想调整各区域高度比例? 改 RowStyles 中的百分比即可
        // ============================================================
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 4,
            BackColor = Bg,
            Padding = new Padding(12) // Wider gutter keeps the dashboard readable when resized.
        };
        // 行: 顶栏 48px / 主区域 65% / 日志 35% / 底栏 34px
        // v1.85-1 调整了比例，让主区域稍微大一点
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 65F));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 35F));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34F));
        Controls.Add(root);

        // ============================================================
        // ★ r0 顶栏 ★ — 显示运行状态、版本信息、.NET SDK 状态
        // 5 列: 运行状态 | 版本信息 | .NET SDK 状态 | 更新AUM | 安装SDK
        // v1.86-7: 新增【更新AUM】按钮
        // ============================================================
        var r0 = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 5, RowCount = 1,
            BackColor = Card,
            Padding = new Padding(16, 5, 16, 5),
            Margin = new Padding(5, 0, 5, 7)
        };
        r0.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18F));
        r0.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        r0.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 27F));
        r0.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));
        r0.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 15F));

        lbSt = L("[O] 未运行", Rd);
        lbSt.Font = new Font(lbSt.Font, FontStyle.Bold);
        lbVe = L("| 版本: --", Txt2);
        lbSd = L(".NET SDK: ...", Txt2);
        lbSd.TextAlign = ContentAlignment.MiddleRight;

        // 安装 SDK 按钮 — 仅在检测到没有 .NET SDK 时才需要点击
        btSdk = B("安装NET.10 SDK", Or, 7);
        btSdk.Dock = DockStyle.Fill;
        btSdk.Click += async (s, e) =>
        {
            Lg(">>> 开始安装 .NET 10 SDK...", Color.CornflowerBlue);
            await IS();
        };

        // 更新AUM 按钮 — 检测并安装 AUM 管理器自身的新版本
        btAu = B("更新AUM", Rd, 7);
        btAu.Dock = DockStyle.Fill;
        btAu.Click += async (s, e) =>
        {
            Lg(">>> 正在检测 AUM 管理器更新...", Color.CornflowerBlue);
            await CheckAndUpdateAUM();
        };

        r0.Controls.Add(lbSt, 0, 0);
        r0.Controls.Add(lbVe, 1, 0);
        r0.Controls.Add(lbSd, 2, 0);
        r0.Controls.Add(btAu, 3, 0);
        r0.Controls.Add(btSdk, 4, 0);
        root.Controls.Add(r0, 0, 0);

        // ============================================================
        // ★ r1 主区域 ★ — 左右分栏布局
        // 左侧 38%: 控制面板 (开始游戏 / 快速操作 / 更新管理)
        // 右侧 62%: 存档管理 (存档列表 + 操作按钮)
        // v1.85-1 优化: 左侧略收窄，让存档列表有更多空间
        // ============================================================
        var r1 = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2, RowCount = 1,
            BackColor = Bg
        };
        r1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38F));
        r1.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62F));
        root.Controls.Add(r1, 0, 1);

        // ============================================================
        // ★ left 左侧面板 ★ — 3 行结构
        // 行1: 开始游戏 (34%) — 启动/停止/重启 + DX 补丁选择
        // 行2: 快速操作 (33%) — PVF状态 + 打开目录 + GM工具
        // 行3: 更新管理 (33%) — 增量/全量更新 + 查看日志
        // ============================================================
        var left = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 3,
            BackColor = Bg
        };
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 42F));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 29F));
        left.RowStyles.Add(new RowStyle(SizeType.Percent, 29F));

        // ============================================================
        // ★ 开始游戏区域 ★ — 绿色大按钮 + 停止/重启 + DX 补丁选项
        // v1.85-1 美化: GroupBox 使用边框色，内部间距更合理
        // ============================================================
        var gp = Section("开始游戏");
        var pg = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3, RowCount = 4,
            BackColor = Card
        };
        pg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        pg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        pg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25F));
        pg.RowStyles.Add(new RowStyle(SizeType.Percent, 40F));
        pg.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        pg.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));
        pg.RowStyles.Add(new RowStyle(SizeType.Percent, 20F));

        // [开始游戏] — 绿色、加粗、第一行占 50% 宽度
        // 功能: 启动服务端 → 等 5 秒 → 启动游戏客户端
        //      10 秒后自动隐藏服务端控制台窗口
        btPlay = B("开始游戏", Gn, 12, true);
        btPlay.Dock = DockStyle.Fill;
        btPlay.Click += async (s, e) =>
        {
            Lg(">>> 点击了开始游戏", Color.CornflowerBlue);
            await Play();
        };

        // [停止服务端] — 红色、加粗
        // 功能: 强制终止 bat 进程树 + 清理所有 DfoServer 进程
        btStop = B("停止服务端", Rd, 10, true);
        btStop.Dock = DockStyle.Fill;
        btStop.Click += (s, e) =>
        {
            Lg(">>> 点击了停止服务端", Color.Gold);
            // 后台线程执行停止 (避免 UI 卡顿)
            System.Threading.Tasks.Task.Run(() =>
            {
                _sv.Stop();
                Invoke(new Action(() =>
                    Lg(">>> 已终止服务端进程树", Color.Gold)));
            });
        };

        // [重启服务端] — 橙色、加粗
        // 流程: 停止 → 等 1.2 秒 → 启动 → 10 秒后隐藏控制台和DfoServer窗口
        btRe = B("重启服务端", Or, 10, true);
        btRe.Dock = DockStyle.Fill;
        btRe.Click += async (s, e) =>
        {
            Lg(">>> 点击了重启服务端", Color.CornflowerBlue);
            await System.Threading.Tasks.Task.Run(() => _sv.Stop());
            await System.Threading.Tasks.Task.Delay(1200);
            Go();
            // 10 秒后隐藏 bat 控制台窗口
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(10000);
                Invoke(new Action(() =>
                { try { _sv.HideConsoleWindow(); } catch { } }));
            });
            // 10 秒后隐藏 DfoServer.exe 窗口（确认进程已运行后）
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(10000);
                Invoke(new Action(() =>
                { try { ServerService.HideDfoServerWindow(); } catch { } }));
            });
        };

        pg.Controls.Add(btPlay, 0, 0);
        pg.Controls.Add(btStop, 1, 0);
        pg.Controls.Add(btRe, 2, 0);

        // DX11 复选框 — 勾选后自动复制 DX11 补丁文件到游戏目录
        cbDx = new CheckBox
        {
            Text = "使用 DX11 运行游戏",
            ForeColor = Txt2, BackColor = Color.Transparent,
            Font = new Font("Microsoft YaHei", 9f),
            Cursor = Cursors.Hand, Dock = DockStyle.Fill,
            CheckAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(2, 0, 0, 0),
            MinimumSize = new Size(0, 22)
        };
        cbDx.CheckedChanged += Cd;
        pg.Controls.Add(cbDx, 0, 1);
        pg.SetColumnSpan(cbDx, 2);

        // DX12 复选框 — 与 DX11 互斥 (不能同时勾选)
        cbDt = new CheckBox
        {
            Text = "使用 DX12 运行游戏",
            ForeColor = Txt2, BackColor = Color.Transparent,
            Font = new Font("Microsoft YaHei", 9f),
            Cursor = Cursors.Hand, Dock = DockStyle.Fill,
            CheckAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(2, 0, 0, 0),
            MinimumSize = new Size(0, 22)
        };
        cbDt.CheckedChanged += Cd;
        pg.Controls.Add(cbDt, 0, 2);
        pg.SetColumnSpan(cbDt, 2);

        // 去水印复选框 — 配合 DX11/DX12 使用，复制无水印版本的补丁
        cbDw = new CheckBox
        {
            Text = "去除dgVoodooCpl运行水印",
            ForeColor = Txt2, BackColor = Color.Transparent,
            Font = new Font("Microsoft YaHei", 9f),
            Cursor = Cursors.Hand, Dock = DockStyle.Fill,
            CheckAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(2, 0, 0, 0),
            MinimumSize = new Size(0, 22)
        };
        cbDw.CheckedChanged += Cd;
        pg.Controls.Add(cbDw, 0, 3);
        pg.SetColumnSpan(cbDw, 2);

        gp.Controls.Add(pg);
        left.Controls.Add(gp, 0, 0);

        // ============================================================
        // ★ 快速操作区域 ★ — PVF检测 + 打开目录 + GM工具
        // v1.85-1 美化: 统一使用边框色，增加视觉层次感
        // ============================================================
        var gq = Section("快速操作");
        var qg = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3, RowCount = 2,
            BackColor = Card
        };
        qg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44F));
        qg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));
        qg.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 28F));

        // PVF 状态 + 上次更新信息
        lbPv = L("PVF: 检测中...", Txt2);
        lbLu = L("上次更新: 尚未有log日志无法识别版本，请进行更新", Or);
        qg.Controls.Add(lbPv, 0, 0);
        qg.Controls.Add(lbLu, 0, 1);
        qg.SetColumnSpan(lbLu, 3);

        // [打开PVF目录] — 在资源管理器中打开 Script.pvf 所在目录
        // v1.85-1: 使用警示红 (#DC3545)，与【停止服务端】按钮统一色系
        btPv = B("打开PVF目录", Rd, 9);
        btPv.Dock = DockStyle.Fill;
        btPv.Click += (s, e) =>
        {
            Lg(">>> 打开PVF目录", Color.CornflowerBlue);
            var d = Path.Combine(_ad, "ServerS4A12-AUM", "dist",
                "win-x64", "Data", "Pvf");
            if (Directory.Exists(d))
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = d, UseShellExecute = true });
            else
                Lg("PVF目录不存在", Color.Gold);
        };

        // [GM工具] — 金色按钮，启动 DfoGmTool 网页管理后台
        btGm = B("GM工具", Color.FromArgb(218, 165, 32), 10, true);
        btGm.Dock = DockStyle.Fill;
        btGm.Click += (s, e) =>
        {
            Lg(">>> 点击了GM工具", Color.Gold);

            // 检查 GM 工具是否已编译
            var gmp = Path.Combine(_ad, "dfogmtool", "publish",
                "DfoGmTool.exe");
            if (!File.Exists(gmp))
            {
                Lg("GM工具尚未编译, 请先执行一次增量/全量更新", Or);
                return;
            }

            // 检查服务端数据完整性
            var sb = Path.Combine(_ad, "ServerS4A12-AUM",
                "dist", "win-x64");
            if (!File.Exists(Path.Combine(sb, "Data", "inventory.db"))
                || !File.Exists(Path.Combine(sb, "Data", "Pvf",
                    "Script.pvf")))
            {
                Lg("GM工具启动失败: 服务端数据目录(" + sb
                    + ")不完整, 请先执行一次更新", Or);
                return;
            }

            // 杀掉旧的 GM 工具进程 (避免端口冲突)
            try
            {
                foreach (var p in Process.GetProcessesByName(
                    "DfoGmTool"))
                { try { p.Kill(); } catch { } }
            }
            catch { }

            // 启动 GM 工具进程
            var psi = new ProcessStartInfo
            {
                FileName = gmp,
                Arguments = "--server-bin \"" + sb + "\"",
                WorkingDirectory = Path.GetDirectoryName(gmp),
                UseShellExecute = false,
                CreateNoWindow = true
            };
            psi.Environment["DFO_GM_SERVER_BIN"] = sb;
            Process.Start(psi);
            Lg("GM工具已启动 -- 服务端目录: " + sb, Gn);

            // 3 秒后自动打开浏览器
            System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(3000);
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = "http://localhost:5050",
                        UseShellExecute = true
                    });
                }
                catch
                {
                    Lg("浏览器未能自动打开, 请手动访问"
                        + " http://localhost:5050", Or);
                }
            });
        };
        qg.Controls.Add(btPv, 1, 0);
        qg.Controls.Add(btGm, 2, 0);
        gq.Controls.Add(qg);
        left.Controls.Add(gq, 0, 1);

        // ============================================================
        // ★ 更新管理区域 ★ — 增量更新 / 全量更新 / 查看更新日志
        // v1.85-1 美化: 统一风格
        // ============================================================
        var gu = Section("更新管理");
        var ug = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2, RowCount = 2,
            BackColor = Card
        };
        ug.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        ug.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50F));
        ug.RowStyles.Add(new RowStyle(SizeType.Percent, 55F));
        ug.RowStyles.Add(new RowStyle(SizeType.Percent, 45F));

        // [增量更新] — 蓝色、只下载最近 3 天变更的文件
        btIn = B("增量更新", Ac, 10, true);
        btFu = B("全量更新", Or, 10, true);
        btIn.Dock = DockStyle.Fill;
        btFu.Dock = DockStyle.Fill;
        btIn.Click += async (s, e) =>
        {
            Lg(">>> 点击了增量更新", Color.CornflowerBlue);
            await RI();
        };
        btFu.Click += async (s, e) =>
        {
            Lg(">>> 点击了全量更新", Color.CornflowerBlue);
            await RF();
        };
        ug.Controls.Add(btIn, 0, 0);
        ug.Controls.Add(btFu, 1, 0);

        // [查看更新日志] — 用记事本打开 更新日志.txt
        btVL = B("查看更新日志", Bg, 9);
        btVL.Dock = DockStyle.Fill;
        btVL.Click += (s, e) =>
        {
            Lg(">>> 查看更新日志", Color.CornflowerBlue);
            SL();
        };
        ug.Controls.Add(btVL, 0, 1);
        ug.SetColumnSpan(btVL, 2);
        gu.Controls.Add(ug);
        left.Controls.Add(gu, 0, 2);

        r1.Controls.Add(left, 0, 0);

        // ============================================================
        // ★ 存档管理区域 (ga) ★ — 整个右侧面板
        // 包含: 信息栏 → 按钮栏 → 存档列表 → 拖拽区
        // v1.85-1 优化: 信息栏改为 FlowLayoutPanel，解决宽度耦合问题
        // ============================================================
        var ga = Section("存档管理");
        var ag = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1, RowCount = 4,
            BackColor = Card
        };
        // 4 行高度: 信息栏 28px / 按钮栏 38px / 存档列表 (填充剩余) / 拖拽区 46px
        // v1.85-1 微调了高度，让各区域更舒适
        ag.RowStyles.Add(new RowStyle(SizeType.Absolute, 36F));
        ag.RowStyles.Add(new RowStyle(SizeType.Absolute, 42F));
        ag.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        ag.RowStyles.Add(new RowStyle(SizeType.Absolute, 48F));

        // ============================================================
        // ★ 信息栏 (ib) ★ — 显示当前存档 + 备份数 + 选项
        //
        // 【v1.85-1 重要改动】
        // 原来用的是 TableLayoutPanel (表格布局)，列宽写死导致
        // 窗口缩放时控件宽度互相挤压。现在改为 FlowLayoutPanel
        // (流式布局)，控件会自动换行，不会互相挤压。
        //
        // FlowLayoutPanel 就像 Word 里的文字排列：
        // 一行排不下就自动换到下一行，非常灵活。
        // ============================================================
        var ib = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            Padding = new Padding(2, 0, 2, 0),
            WrapContents = true,     // 允许换行
            FlowDirection = FlowDirection.LeftToRight
        };

        lbCu = L("当前: --", Txt);
        lbCu.Margin = new Padding(4, 2, 8, 2);

        lbBk = L("备份数: 0", Txt2);
        lbBk.Margin = new Padding(4, 2, 8, 2);

        // [清理冗余DB] — 勾选后自动清理 Data 目录下的冗余 inventory 文件
        cbCl = new CheckBox
        {
            Text = "清理冗余DB",
            ForeColor = Txt,
            BackColor = Color.Transparent,
            Font = new Font("Microsoft YaHei", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand, AutoSize = true,
            CheckAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(2, 0, 0, 0),
            Margin = new Padding(4, 2, 4, 2),
            MinimumSize = new Size(0, 22)
        };
        cbCl.CheckedChanged += (s, e) =>
        {
            Lg(">>> [清理冗余DB] "
                + (cbCl.Checked ? "已启用" : "已关闭"),
                cbCl.Checked ? Gn : Txt2);
            if (cbCl.Checked) CleanRedundantDb();
        };

        // ============================================================
        // 【刷新存档】按钮 — 重新加载存档列表
        //
        // v1.85-1 更新: 原名为【刷新】，现改为【刷新存档】
        // 这样用户一看就知道这个按钮是刷新存档用的，
        // 而不是刷新整个界面或者刷新日志。
        // 颜色也改为强调色 (蓝紫色)，和"储存当前存档"统一风格
        // ============================================================
        var btRf = new Button
        {
            Text = "↻ 刷新存档",
            FlatStyle = FlatStyle.Flat,
            BackColor = Gn,   // 使用森林绿 (#28A745)，与【开始游戏】按钮统一
            ForeColor = Color.White,
            Font = new Font("Microsoft YaHei", 8f, FontStyle.Bold),
            AutoSize = false,
            Margin = new Padding(4, 2, 2, 2),
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(100, 100, 130) },
            TextAlign = ContentAlignment.MiddleCenter,
            Size = new Size(104, 28),
            MinimumSize = new Size(104, 28)
        };
        // ★ 悬停高亮效果（绿色版：悬停时更亮）
        btRf.MouseEnter += (s, e) => { btRf.BackColor = Color.FromArgb(60, 200, 90); };
        btRf.MouseLeave += (s, e) => { btRf.BackColor = Gn; };
        btRf.Click += (s, e) =>
        {
            Lg(">>> 刷新存档列表", Color.CornflowerBlue);
            RA();
        };

        ib.Controls.Add(lbCu);
        ib.Controls.Add(lbBk);
        ib.Controls.Add(cbCl);
        ib.Controls.Add(btRf);
        ag.Controls.Add(ib, 0, 0);

        // ============================================================
        // ★ 按钮栏 (ab) ★ — 7 个存档操作按钮一字排开
        //
        // 从左到右:
        //   切换存档目录  |  备份存档目录  |  主存档目录
        //   储存当前存档  |  导入存档      |  导出当前  |  撤销换挡
        //
        // v1.85-1 美化: 普通操作按钮使用次背景色 (Card) 带悬停变色，
        // 重要操作"储存当前存档"保留强调色 (Ac)
        // ============================================================
        var ab = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 7, RowCount = 1,
            BackColor = Card
        };
        for (int i = 0; i < 7; i++)
            ab.ColumnStyles.Add(new ColumnStyle(
                SizeType.Percent, 100F / 7F));

        // v1.85-1: 普通按钮从纯黑色 (Bg) 改为次背景色 (Card)，
        // 看起来更有层次感，不会一片黑乎乎的
        btOD = B("打开切换库", Ac, 8); // 科技蓝浅 (#007ACC)，与【增量更新】同色
        btOB = B("打开备份库", Card, 8);
        btMD = B("打开主存档", Or, 8);                             // 活力橙，与【重启服务端】同色
        btSC = B("⭐ 储存当前", Ac, 8, true);
        btIm = B("导入存档", Card, 8);
        btEx = B("导出当前", Card, 8);
        btUd = B("↩ 撤销换挡", Card, 8);

        foreach (var b in new[] { btOD, btOB, btMD,
            btSC, btIm, btEx, btUd }) b.Dock = DockStyle.Fill;

        // 打开切换存档目录
        btOD.Click += (s, e) =>
        {
            var d = Path.Combine(_ad, "存档管理", "切换库");
            Directory.CreateDirectory(d);
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = d, UseShellExecute = true });
            Lg(">>> 打开了切换存档目录", Color.CornflowerBlue);
        };
        // 打开备份存档目录
        btOB.Click += (s, e) =>
        {
            var d = Path.Combine(_ad, "存档管理", "备份存档");
            Directory.CreateDirectory(d);
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = d, UseShellExecute = true });
            Lg(">>> 打开了备份存档目录", Color.CornflowerBlue);
        };
        // 打开主存档目录 (Data 目录)
        btMD.Click += (s, e) =>
        {
            var d = Path.Combine(_ad, "ServerS4A12-AUM",
                "dist", "win-x64", "Data");
            if (Directory.Exists(d))
            {
                Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = d, UseShellExecute = true });
                Lg(">>> 打开了主存档目录", Color.CornflowerBlue);
            }
            else Lg("主存档目录不存在", Color.Gold);
        };
        // 储存当前存档到切换库
        btSC.Click += (s, e) =>
        {
            Lg(">>> 点击了储存当前存档", Color.CornflowerBlue);
            DoArchiveOp(() => { SC(); return true; });
        };
        // 导入外部 .db 文件
        btIm.Click += (s, e) =>
        {
            Lg(">>> 点击了导入存档", Color.CornflowerBlue);
            DoArchiveOp(() => { IA(); return true; });
        };
        // 导出当前存档
        btEx.Click += (s, e) =>
        {
            Lg(">>> 点击了导出当前", Color.CornflowerBlue);
            DoArchiveOp(() => { EC(); return true; });
        };
        // 撤销上一次换挡操作
        btUd.Click += (s, e) =>
        {
            Lg(">>> 点击了撤销换挡", Color.CornflowerBlue);
            DoArchiveOp(() =>
            {
                if (!_ar.UndoSwap(_ad))
                {
                    Lg("无备份", Color.Gold);
                    return false;
                }
                LS("已撤销");
                RA();
                if (cbCl != null && cbCl.Checked) CleanRedundantDb();
                return true;
            });
        };

        ab.Controls.Add(btOD, 0, 0);
        ab.Controls.Add(btOB, 1, 0);
        ab.Controls.Add(btMD, 2, 0);
        ab.Controls.Add(btSC, 3, 0);
        ab.Controls.Add(btIm, 4, 0);
        ab.Controls.Add(btEx, 5, 0);
        ab.Controls.Add(btUd, 6, 0);
        ag.Controls.Add(ab, 0, 1);

        // ============================================================
        // ★ 存档列表 (lv) ★ — 显示切换库里所有的 .db 存档
        // 4 列: 序号 | 存档名称(右键双击改名) | 大小 | 修改时间
        //
        // 操作方式:
        //   - 左键双击 → 切换到该存档 (自动备份当前存档)
        //   - 右键双击 → 重命名存档
        //   - 点击"修改时间"列头 → 切换正序/倒序排列
        //
        // v1.85-1 美化: 列表背景色微调，更符合整体深色主题
        // ============================================================
        lv = new ListView
        {
            Dock = DockStyle.Fill,
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,     // 保留网格线（用户偏好，与1.83一致）
            BackColor = Color.FromArgb(25, 25, 40),
            ForeColor = Txt,
            Font = new Font("Microsoft YaHei", 9f),
            Scrollable = true,
            HeaderStyle = ColumnHeaderStyle.Clickable,
            BorderStyle = BorderStyle.None
        };
        lv.Columns.Add("#", 36);
        lv.Columns.Add("存档名称(右键双击改名)", -2);
        lv.Columns.Add("大小", 75);
        lv.Columns.Add("修改时间(点此排序)", 145);
        lv.MouseDown += Am;     // 处理双击 (切换 / 重命名)
        lv.ColumnClick += Ao;   // 处理点击列头 (排序)
        ag.Controls.Add(lv, 0, 2);

        // ============================================================
        // ★ 拖拽区 (dz) ★ — 拖拽 .db 文件到此处快速换挡
        //
        // 除了拖拽换挡，双击拖拽区还可以快速"储存当前存档"
        // v1.85-1 美化: 背景色改为卡片色，虚线边框效果，更明显
        // ============================================================
        var dz = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Card,
            BorderStyle = BorderStyle.FixedSingle,
            Cursor = Cursors.Hand
        };
        dz.DoubleClick += (s, e) =>
        {
            Lg(">>> 双击拖拽区，储存当前存档", Color.CornflowerBlue);
            DoArchiveOp(() => { SC(); return true; });
        };
        dz.DragEnter += (s, e) =>
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                var fs = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (fs.Length == 1 && fs[0].EndsWith(".db",
                    StringComparison.OrdinalIgnoreCase))
                    e.Effect = DragDropEffects.Copy;
            }
        };
        dz.DragDrop += (s, e) =>
        {
            var fs = (string[])e.Data.GetData(DataFormats.FileDrop);
            Lg(">>> 拖拽换挡: " + Path.GetFileName(fs[0]),
                Color.CornflowerBlue);
            DoArchiveOp(() =>
            {
                DoSwapCore(fs[0], "拖拽换挡完成");
                return true;
            });
        };
        lbDr = new Label
        {
            Text = "📁 拖拽 .db 文件到此处 = 快速替换存档",
            ForeColor = Txt2,
            AutoSize = false,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Fill,
            Font = new Font("Microsoft YaHei", 8f)
        };
        dz.Controls.Add(lbDr);
        ag.Controls.Add(dz, 0, 3);
        ga.Controls.Add(ag);
        r1.Controls.Add(ga, 1, 0);

        // ============================================================
        // ★ 日志区域 (lp) ★ — 显示所有操作日志 + 更新进度条
        //
        // 包含:
        //   - RichTextBox: 带颜色标注的实时日志
        //   - 进度条: 更新时显示，平时隐藏
        //   - 标签: 显示当前进度百分比
        //   - 工具栏: [复制日志] [清空日志] 按钮
        //
        // v1.85-1 美化: 日志背景色略微调整，按钮风格统一
        // ============================================================
        var lp = Section("运行日志");
        lp.BackColor = Color.FromArgb(18, 18, 30);

        // RichTextBox — 只读、等宽字体、深色背景
        rt = new RichTextBox
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(18, 18, 30),
            ForeColor = Txt,
            ReadOnly = true,
            WordWrap = true,
            Font = new Font("Consolas", 9f),
            BorderStyle = BorderStyle.None
        };

        // ============================================================
        // ★ 进度条 ★ — 更新时显示进度，平时自动隐藏
        // v1.85-1: 高度稍微增加，颜色使用强调色
        // ============================================================
        pb = new ProgressBar
        {
            Dock = DockStyle.Bottom, Height = 10,
            Style = ProgressBarStyle.Continuous,
            Maximum = 100, Visible = false,
            BackColor = Bg,
            ForeColor = Ac
        };

        // 进度百分比标签 — 显示 "更新进度: 35%"
        lbPg = new Label
        {
            Text = "", ForeColor = Txt2,
            AutoSize = true, BackColor = Color.Transparent,
            Visible = false
        };

        // ============================================================
        // ★ 日志工具栏 ★ — 右侧 [复制日志] [清空日志] 两个按钮
        // v1.85-1 美化: 按钮使用次背景色，带悬停效果
        // ============================================================
        var lbar = new Panel
        {
            Dock = DockStyle.Bottom, Height = 36,
            BackColor = Card,
            Margin = new Padding(5, 7, 5, 0)
        };
        // 复制运行日志按钮 — 使用森林绿 (#28A745)，与【开始游戏】同色
        btCp = new Button
        {
            Text = "复制日志", FlatStyle = FlatStyle.Flat,
            BackColor = Gn, ForeColor = Color.White,
            Font = new Font("Microsoft YaHei", 9f),
            MinimumSize = new Size(100, 28),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(60, 200, 90) }
        };
        // 清空日志按钮 — 使用警示红 (#DC3545)，与【停止服务端】同色
        btCl = new Button
        {
            Text = "清空日志", FlatStyle = FlatStyle.Flat,
            BackColor = Rd, ForeColor = Color.White,
            Font = new Font("Microsoft YaHei", 9f),
            MinimumSize = new Size(85, 28),
            Anchor = AnchorStyles.Right | AnchorStyles.Bottom,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false,
            FlatAppearance = { BorderSize = 0, MouseOverBackColor = Color.FromArgb(240, 80, 100) }
        };
        btCl.Click += (s, e) =>
        {
            Lg(">>> 清空日志", Color.CornflowerBlue);
            rt.Clear();
            _logBuilder.Clear();
        };
        btCp.Click += (s, e) =>
        {
            Lg(">>> 复制日志", Color.CornflowerBlue);
            if (rt.Text.Length > 0)
            {
                // 【v1.85-1 修复】剪贴板操作可能因其他程序占用而失败，
                // 使用 SetDataObject 内置重试（3 次 × 50ms），不阻塞 UI
                try
                {
                    Clipboard.SetDataObject(rt.Text, copy: true, retryTimes: 3, retryDelay: 50);
                }
                catch (Exception)
                {
                    Lg("复制失败: 所请求的剪贴板操作失败，请手动进行复制——按下Ctrl+A可以全选整个运行日志", Rd);
                }
            }
        };
        lbar.Controls.Add(lbPg);
        // 跳过更新日志复选框 (与【清理冗余DB】同风格)
        cbSkipLog = new CheckBox
        {
            Text = "跳过更新日志",
            ForeColor = Or, BackColor = Color.Transparent,
            Font = new Font("Microsoft YaHei", 8f, FontStyle.Bold),
            Cursor = Cursors.Hand, AutoSize = true,
            CheckAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(2, 0, 0, 0),
            MinimumSize = new Size(0, 22)
        };
        cbSkipLog.CheckedChanged += (s, e) =>
        {
            Lg(">>> [跳过更新日志] " + (cbSkipLog.Checked ? "已启用 — 下次更新不拉取提交记录" : "已关闭"),
                cbSkipLog.Checked ? Or : Txt2);
        };
        lbar.Controls.Add(cbSkipLog);
        cbSkipLog.Location = new Point(lbar.Width - 350, 6);

        // 镜像下载复选框 (v1.914: 与【跳过更新日志】同风格)
        cbMirror = new CheckBox
        {
            Text = "镜像下载",
            ForeColor = Or, BackColor = Color.Transparent,
            Font = new Font("Microsoft YaHei", 8f, FontStyle.Bold),
            Cursor = Cursors.Hand, AutoSize = true,
            CheckAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(2, 0, 0, 0),
            MinimumSize = new Size(0, 22)
        };
        cbMirror.CheckedChanged += (s, e) =>
        {
            Lg(">>> [镜像下载] " + (cbMirror.Checked ? "已启用 — 跳过GitGud直接使用镜像源" : "已关闭"),
                cbMirror.Checked ? Gn : Txt2);
        };
        lbar.Controls.Add(cbMirror);
        cbMirror.Location = new Point(lbar.Width - 460, 6);
        lbar.Controls.Add(btCp);
        lbar.Controls.Add(btCl);
        lbPg.Location = new Point(10, 10);
        // 按钮靠右排列（已调整间距，确保文字不截断）
        btCp.Location = new Point(lbar.Width - 200, 4);
        btCl.Location = new Point(lbar.Width - 95, 4);
        // 工具栏大小改变时重新计算按钮位置
        lbar.Resize += (s, e) =>
        {
            cbSkipLog.Location = new Point(lbar.Width - 350, 6);
            cbMirror.Location = new Point(lbar.Width - 460, 6);
            btCp.Location = new Point(lbar.Width - 200, 4);
            btCl.Location = new Point(lbar.Width - 95, 4);
        };

        lp.Controls.Add(rt);
        lp.Controls.Add(pb);
        lp.Controls.Add(lbar);
        root.Controls.Add(lp, 0, 2);

        // ============================================================
        // ★ 底部链接栏 (r3) ★ — GM工具链接 + 代码仓库链接 + 镜像链接
        // v1.911: 新增 GitHub/Codeberg 镜像仓库链接
        // ============================================================
        var r3 = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 3, RowCount = 1,
            BackColor = Card,
            Padding = new Padding(10, 2, 10, 2)
        };
        r3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        r3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.33F));
        r3.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 33.34F));

        // GM 工具链接 (使用金色常量 Color.Gold)
        var lg = new LinkLabel
        {
            Text = "🎮 GM工具: http://localhost:5050",
            ForeColor = Color.Gold, LinkColor = Color.Gold,
            ActiveLinkColor = Color.White,
            AutoSize = true, Anchor = AnchorStyles.Left,
            Font = new Font("Microsoft YaHei", 9f)
        };
        lg.LinkClicked += (s, e) =>
        {
            Lg(">>> 打开GM工具网页", Color.Gold);
            Process.Start(new ProcessStartInfo
            {
                FileName = "http://localhost:5050",
                UseShellExecute = true
            });
        };

        // ServerUI-AUM 仓库链接 (使用科技蓝 Ac)
        var lm = new LinkLabel
        {
            Text = "ServerUI-AUM仓库",
            ForeColor = Ac, LinkColor = Ac,
            ActiveLinkColor = Color.White,
            AutoSize = true, Anchor = AnchorStyles.None,
            Font = new Font("Microsoft YaHei", 9f)
        };
        lm.LinkClicked += (s, e) =>
        {
            Lg(">>> 打开 ServerUI-AUM 仓库", Color.CornflowerBlue);
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = "https://github.com/118coder/ServerUI-AUM-S4A12", UseShellExecute = true });
        };

        // 仓库链接 (GitGud + Codeberg, 使用强调色)
        var lr = new LinkLabel
        {
            Text = "源仓库：https://gitgud.io/rewio/86JP",
            ForeColor = Txt2, LinkColor = Txt2,
            ActiveLinkColor = Color.White,
            AutoSize = true, Anchor = AnchorStyles.Right,
            Font = new Font("Microsoft YaHei", 9f)
        };
        lr.LinkClicked += (s, e) =>
        {
            Lg(">>> 打开仓库链接", Color.CornflowerBlue);
            Process.Start(new ProcessStartInfo { FileName = "explorer.exe", Arguments = "https://gitgud.io/rewio/86JP", UseShellExecute = true });
        };

        r3.Controls.Add(lg, 0, 0);
        r3.Controls.Add(lm, 1, 0);
        r3.Controls.Add(lr, 2, 0);
        root.Controls.Add(r3, 0, 3);
    }

    // =================================================================
    // 定时器初始化 (Ti)
    // =================================================================
    // 三个定时器各司其职:
    //   _pt — 进度条动画 (200ms 间隔，推进到 95% 后停止)
    //   _ct — 启动确认   (3 秒后检查 bat+DfoServer 是否都在运行)
    //   _st — 状态刷新   (2 秒间隔，更新运行状态/版本/存档列表)
    //
    // 修改建议:
    //   - 想改变状态刷新频率? 改 _st 的 Interval (默认 2000ms)
    //   - 想改变进度条速度? 改 _pt 的 Interval (默认 200ms) 和 _pv++ 逻辑
    void Ti()
    {
        _pt = new Timer { Interval = 500 };
        _pt.Tick += (s, e) =>
        {
            if (_pv < _stepTarget && _pv < 95) { _pv++; }
            if (_pv >= 95) _pt.Stop();
            pb.Value = Math.Min(_pv, 100);
            lbPg.Text = "更新进度: " + pb.Value + "%";
        };

        _ct = new Timer { Interval = 3000 };
        _ct.Tick += (s, e) =>
        {
            if (_sv.IsRunning)
            {
                Lg(">>> 确认服务端进程已启动", Gn);
                _ct.Stop();
            }
        };

        _st = new Timer { Interval = 2000 };
        _st.Tick += (s, e) => Rs();
        _st.Start();  // 立即开始状态刷新
    }

    // =================================================================
    // 核心事件处理
    // =================================================================

    /*
     * 窗口关闭事件 (Fc)
     * 关闭前弹出确认对话框，确认后清理所有相关进程
     * 清理顺序: 终止更新进程 → 停止服务端进程树 → 杀 DfoGmTool 进程 → 停止所有定时器
     *   → 保存运行日志到文件 → 清理编译缓存 → 清理更新临时目录
     */
    void Fc(object s, FormClosingEventArgs e)
    {
        var r = MessageBox.Show(
            "退出本程序之后会自动关闭正在运行的服务端和GM工具，是否确认？",
            "确认退出", MessageBoxButtons.YesNo, MessageBoxIcon.Warning);
        if (r == DialogResult.Yes)
        {
            _up.CancelUpdate();
            Lg(">>> 正在关闭所有相关进程...", Color.Gold);
            _sv.Stop();
            try
            {
                foreach (var p in Process.GetProcessesByName("DfoGmTool"))
                { try { p.Kill(); } catch { } }
            }
            catch { }
            _st.Stop(); _pt.Stop(); _ct.Stop();
            Lg(">>> 已清理所有进程", Gn);

            SaveRunningLog();
            CleanCompileCache();
            CleanUpdateTemp();
        }
        else e.Cancel = true;  // 用户取消 → 不关闭窗口
    }

    void SaveRunningLog()
    {
        try
        {
            var logPath = Path.Combine(_ad, "运行日志.txt");
            File.WriteAllText(logPath, _logBuilder.ToString(), Encoding.UTF8);
        }
        catch (Exception ex)
        {
            Lg(">>> 保存运行日志失败: " + ex.Message, Rd);
        }
    }

    void CleanCompileCache()
    {
        var cacheDirs = new[]
        {
            Path.Combine(_ad, "ServerS4A12-AUM", "Server", "DfoServer", "obj"),
            Path.Combine(_ad, "ServerS4A12-AUM", "Server", "DfoServer", "bin"),
            Path.Combine(_ad, "dfogmtool", "obj"),
            Path.Combine(_ad, "dfogmtool", "bin")
        };

        foreach (var dir in cacheDirs)
        {
            try
            {
                if (Directory.Exists(dir))
                {
                    Directory.Delete(dir, true);
                    Lg(">>> 已清理编译缓存: " + Path.GetFileName(dir), Gn);
                }
            }
            catch (Exception ex)
            {
                Lg(">>> 清理缓存失败 (" + dir + "): " + ex.Message, Or);
            }
        }
    }

    void CleanUpdateTemp()
    {
        var pattern = "ServerS4A12-*";
        try
        {
            foreach (var dir in Directory.GetDirectories(Path.GetTempPath(), pattern))
            {
                try { Directory.Delete(dir, true); } catch { }
            }
            foreach (var dir in Directory.GetDirectories(Path.GetTempPath(), "ServerUI-AUM-update*"))
            {
                try { Directory.Delete(dir, true); } catch { }
            }
            Lg(">>> 已清理更新缓存目录", Gn);
        }
        catch (Exception ex)
        {
            Lg(">>> 清理更新缓存失败: " + ex.Message, Or);
        }
    }

    /*
     * 启动服务端 (Go)
     * 定位 start-server.bat 并启动，同时开始 3 秒后的存活检测
     */
    void Go()
    {
        _sv.Start(Path.Combine(_ad, "ServerS4A12-AUM"));
        _ct.Start();
    }

    /*
     * 开始游戏完整流程 (Play)
     * 步骤:
     *   1. 启动 start-server.bat
     *   2. 10 秒后隐藏 bat 控制台窗口
     *   3. 等 5 秒确认 DfoServer 已启动
     *   4. 确认运行后 5 秒隐藏 DfoServer.exe 窗口
     *   5. 启动游戏客户端 (本地游戏S4.bat 或 单机游戏启动.bat)
     *
     * 修改建议:
     *   - 想改等待时间? 改 Delay() 中的毫秒数
     *   - 想改游戏启动脚本? 改下面的 .bat 文件名
     *   - 想禁用自动隐藏窗口? 删除对应的 Task.Run 块
     */
    async System.Threading.Tasks.Task Play()
    {
        Lg(">>> 正在启动 start-server.bat...", Color.CornflowerBlue);
        Go();

        // 10 秒后隐藏 bat 控制台窗口 (后台任务，不阻塞)
        _ = System.Threading.Tasks.Task.Run(async () =>
        {
            await System.Threading.Tasks.Task.Delay(10000);
            Invoke(new Action(() =>
            { try { _sv.HideConsoleWindow(); } catch { } }));
        });

        // 等 5 秒让 DfoServer 启动
        await System.Threading.Tasks.Task.Delay(5000);

        // 确认服务端是否成功启动
        if (_sv.IsRunning)
        {
            Lg(">>> 服务端进程存活，正在启动游戏...", Gn);
            // 确认 DfoServer 进程运行后，5 秒后隐藏其控制台窗口
            _ = System.Threading.Tasks.Task.Run(async () =>
            {
        // v1.914: 等待15秒确保 update.ps1 的 GitGud 检测先完成（其窗口为10秒）
        await System.Threading.Tasks.Task.Delay(15000);
                Invoke(new Action(() =>
                { try { ServerService.HideDfoServerWindow(); } catch { } }));
            });
        }
        else
            Lg(">>> 警告: 服务端可能未成功启动", Or);

        // 启动游戏客户端 - 本地游戏S4.bat 现在在 AUM管理组件 内
        var bat = Path.Combine(_ad, "本地游戏S4.bat");
        if (File.Exists(bat))
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = bat,
                WorkingDirectory = _ad,
                UseShellExecute = true
            });
            Lg(">>> 已打开本地游戏S4.bat", Gn);
        }
        else
        {
            var fb = Path.Combine(_ad, "单机游戏启动.bat");
            if (File.Exists(fb))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = fb,
                    WorkingDirectory = _ad,
                    UseShellExecute = true
                });
                Lg(">>> 已打开单机游戏启动.bat", Gn);
            }
            else
                Lg(">>> 本地游戏S4.bat / 单机游戏启动.bat 未找到!",
                    Rd);
        }
    }

    /*
     * 日志输出 (Lg)
     *
     * 格式: [HH:mm:ss] 消息内容
     * 示例: [14:30:52] >>> 点击了开始游戏
     *
     * 重载:
     *   Lg(string)        — 使用默认颜色 (Txt)
     *   Lg(string, Color) — 使用指定颜色
     *   LS(string)        — 使用绿色 (Gn)，表示成功操作
     *
     * 线程安全: 如果从后台线程调用，自动 Invoke 到 UI 线程
     *
     * 修改建议:
     *   - 想改时间格式? 改 "HH:mm:ss" → "yyyy-MM-dd HH:mm:ss"
     *   - 想关闭日志? 在方法开头加 return
     *   - 想保存日志到文件? 加 File.AppendAllText
     */
    void Lg(string m) => Lg(m, Txt);
    void Lg(string m, Color c)
    {
        if (rt.InvokeRequired)
        {
            rt.Invoke(new Action(() => Lg(m, c)));
            return;
        }
        var line = "[" + DateTime.Now.ToString("HH:mm:ss") + "] " + m;
        _logBuilder.AppendLine(line);
        rt.SelectionStart = rt.TextLength;
        rt.SelectionLength = 0;
        rt.SelectionColor = Txt;
        rt.AppendText("[" + DateTime.Now.ToString("HH:mm:ss") + "] ");
        rt.SelectionColor = c;
        rt.AppendText(m + "\n");
        rt.ScrollToCaret();
    }
    void LS(string m) => Lg(m, Gn);

    // =================================================================
    // 存档操作
    // =================================================================

    /*
     * 导入存档 (IA) — 从文件对话框选择一个 .db 文件导入到切换库
     */
    void IA()
    {
        using var d = new OpenFileDialog { Filter = "DB|*.db" };
        if (d.ShowDialog() == DialogResult.OK)
        {
            var dest = Path.Combine(_ad, "存档管理", "切换库",
                Path.GetFileName(d.FileName));
            Directory.CreateDirectory(Path.GetDirectoryName(dest));
            File.Copy(d.FileName, dest, true);
            LS("已导入: " + Path.GetFileName(d.FileName));
            RA();
        }
    }

    /*
     * 导出存档 (EC) — 把当前 inventory.db 导出到切换库
     */
    void EC()
    {
        var n = Interaction.InputBox("名称:", "导出存档", "存档");
        if (!string.IsNullOrWhiteSpace(n))
        {
            _ar.Export(_ad, n);
            LS("已导出: " + n + ".db");
            RA();
        }
    }

    /*
     * 储存当前存档 (SC) — 把当前 inventory.db 以指定名称存到切换库
     * 默认名称: 当前时间的 MMDD_HHmm 格式
     */
    void SC()
    {
        var n = Interaction.InputBox("名称:", "储存当前存档",
            DateTime.Now.ToString("MMdd_HHmm"));
        if (!string.IsNullOrWhiteSpace(n))
        {
            _ar.Export(_ad, n);
            LS("已储存到切换库: " + n + ".db");
            RA();
        }
    }

    /*
     * 存档列表鼠标事件 (Am)
     *
     * 双击左键 → 切换存档 (使用 DoSafeSwap 做安全换挡)
     * 双击右键 → 重命名存档
     */
    void Am(object s, MouseEventArgs e)
    {
        var h = lv.HitTest(e.X, e.Y);
        if (h?.Item == null) return;

        var nm = h.Item.SubItems[1].Text;  // 存档文件名

        // 右键双击 → 重命名
        if (e.Button == MouseButtons.Right && e.Clicks == 2)
        {
            var nn = Interaction.InputBox("修改存档名称:", "重命名",
                nm.Replace(".db", ""));
            if (!string.IsNullOrWhiteSpace(nn)
                && nn != nm.Replace(".db", ""))
            {
                var op = Path.Combine(_ad, "存档管理", "切换库", nm);
                var nf = nn.EndsWith(".db",
                    StringComparison.OrdinalIgnoreCase)
                    ? nn : nn + ".db";
                var np = Path.Combine(_ad, "存档管理", "切换库", nf);
                if (File.Exists(op) && !File.Exists(np))
                {
                    File.Move(op, np);
                    LS("已重命名: " + nm + " -> " + nf);
                    RA();
                }
                else if (File.Exists(np))
                    Lg("名称已存在", Color.Gold);
                else
                    Lg("重命名失败", Color.Gold);
            }
            return;
        }

        // 左键双击 → 切换存档
        if (e.Button == MouseButtons.Left && e.Clicks == 2)
        {
            var path = Path.Combine(_ad, "存档管理", "切换库", nm);
            DoArchiveOp(() =>
            {
                DoSwapCore(path, "已切换到: " + nm);
                return true;
            });
        }
    }

    /*
     * 列头点击 (Ao) — 点击"修改时间"列头切换排序方向
     */
    void Ao(object s, ColumnClickEventArgs e)
    {
        if (e.Column == 3) { _sa = !_sa; RA(); }
    }

    /*
     * 清理旧备份 (TB) — 限制备份目录最多保留 MB 个备份文件
     * 超出限制时删除最旧的备份
     *
     * 修改建议: 改 MB 常量即可改变备份数量上限
     */
    void TB()
    {
        var bd = Path.Combine(_ad, "存档管理", "备份存档");
        if (!Directory.Exists(bd)) return;

        var fs = new DirectoryInfo(bd)
            .GetFiles("backup_*.db")
            .OrderByDescending(f => f.LastWriteTime)
            .ToList();

        // 删除超出限制的最旧文件
        while (fs.Count > MB)
        {
            fs[^1].Delete();           // 删除最后一个 (最旧)
            fs.RemoveAt(fs.Count - 1);
        }
    }

    /*
     * 获取服务端 Data 目录路径
     */
    string DataDir() => Path.Combine(_ad, "ServerS4A12-AUM",
        "dist", "win-x64", "Data");

    /*
     * 清理冗余 DB 文件 (CleanRedundantDb)
     *
     * 扫描 Data 目录及所有子目录，删除以下类型的冗余文件:
     *   - inventory.db-shm  (SQLite WAL 共享内存)
     *   - inventory.db-wal  (SQLite WAL 日志)
     *   - inventory-副本.db (手动备份)
     *   - 其他以 "inventory" 开头但不是 "inventory.db" 的文件
     *
     * 注意:
     *   - 不会删除 inventory.db 本身
     *   - 不会删除文件夹
     *   - 不会删除其他无关文件 (如 Script.pvf)
     *
     * 调用时机:
     *   - 勾选 [清理冗余DB] 后每次刷新存档列表
     *   - 切换/拖拽存档后
     */
    void CleanRedundantDb()
    {
        try
        {
            var dd = DataDir();
            if (!Directory.Exists(dd)) return;

            var cleaned = 0;
            foreach (var f in Directory.GetFiles(dd, "inventory*",
                SearchOption.AllDirectories))
            {
                var nm = Path.GetFileName(f);
                // 跳过真正的 inventory.db
                if (string.Equals(nm, "inventory.db",
                    StringComparison.OrdinalIgnoreCase))
                    continue;
                try { File.Delete(f); cleaned++; }
                catch { }
            }

            if (cleaned > 0)
                Lg(">>> [清理冗余DB] 已清理 " + cleaned
                    + " 个冗余文件", Gn);
        }
        catch (Exception ex)
        {
            Lg(">>> [清理冗余DB] 清理时出错: " + ex.Message, Or);
        }
    }

    /*
     * 存档操作包装器 (DoArchiveOp) — v1.916
     *
     * 当服务端运行时，自动停止 → 执行操作 → 自动重启。
     * 服务端未运行时直接执行操作，不做启停。
     */
    void DoArchiveOp(Func<bool> op)
    {
        bool wasRunning = _sv.IsRunning;
        if (wasRunning)
        {
            Lg(">>> 检测到服务端运行中，自动停止以操作存档...", Or);
            _sv.Stop();
            System.Threading.Thread.Sleep(600);
        }

        bool ok = op();

        if (wasRunning)
        {
            Lg(">>> 正在自动重启服务端...", Gn);
            System.Threading.Tasks.Task.Run(async () =>
            {
                await System.Threading.Tasks.Task.Delay(600);
                Invoke(new Action(Go));
            });
        }

        if (ok && !cbCl.Checked)
        {
            Lg(">>> 已切换存档。如果无法登录服务端或网络连接中断，请勾选【清理冗余DB】后重试。", Or);
        }
    }

    void DoSwapCore(string srcPath, string msg)
    {
        _ar.Swap(_ad, srcPath);
        LS(msg);
        RA();
        TB();
        if (cbCl != null && cbCl.Checked) CleanRedundantDb();
    }

    /*
     * 查看更新日志 (SL) — 用记事本打开 更新日志.txt
     */
    void SL()
    {
        var lf = Path.Combine(_ad, "更新日志.txt");
        if (File.Exists(lf))
            Process.Start(new ProcessStartInfo { FileName = lf, UseShellExecute = true });
        else
            MessageBox.Show(
                "暂时没有更新日志，请注意查看版本信息。",
                "更新日志", MessageBoxButtons.OK,
                MessageBoxIcon.Information);
    }

    /*
     * 拖入检测 (De) — 只接受单个 .db 文件的拖放
     */
    void De(object s, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(DataFormats.FileDrop))
        {
            var fs = (string[])e.Data.GetData(DataFormats.FileDrop);
            if (fs.Length == 1 && fs[0].EndsWith(".db",
                StringComparison.OrdinalIgnoreCase))
                e.Effect = DragDropEffects.Copy;
        }
    }

    /*
     * 拖放处理 (Dd) — 拖 .db 文件到窗口任意位置均可换挡
     * 使用了 DoSafeSwap 做安全换挡
     */
    void Dd(object s, DragEventArgs e)
    {
        var fs = (string[])e.Data.GetData(DataFormats.FileDrop);
        Lg(">>> 拖拽换挡: " + Path.GetFileName(fs[0]),
            Color.CornflowerBlue);
        DoArchiveOp(() =>
        {
            DoSwapCore(fs[0], "拖拽换挡完成");
            return true;
        });
    }

    // =================================================================
    // 系统检测 (Ck)
    // =================================================================
    /*
     * 启动时执行系统环境检测
     *
     * 检测项目:
     *   1. Windows 版本 (低于 Win10 发出警告)
     *   2. EXE 版本 (便携版/有依赖版)
     *   3. .NET SDK 可用性 (三级检测: 系统 → Program Files → 本地 dotnet-sdk)
     *
     * 修改建议:
     *   - 想修改 .NET 最低版本要求? 改 "10." 的匹配字符串
     *   - 想添加新的检测项? 在方法末尾加逻辑
     *   - 想改变 SDK 目录名? 改 "dotnet-sdk" 字符串
     */
    void Ck()
    {
        Lg("ServerUI 版本: " + VER, Color.DarkOrange);

        // ---- 检测便携版/有依赖版 ----
        // 判断逻辑: EXE > 50MB 认为是自包含(便携)版
        var exePath = Environment.ProcessPath ?? "";
        bool isPortable = !string.IsNullOrEmpty(exePath)
            && File.Exists(exePath)
            && new FileInfo(exePath).Length > 50_000_000;

        // ---- 检测 Windows 版本 ----
        var osVer = Environment.OSVersion;
        bool isWin10Plus = osVer.Platform == PlatformID.Win32NT
            && osVer.Version.Major >= 10;
        if (!isWin10Plus)
            Lg("系统版本低于 Windows 10，可能会出现兼容性问题，"
                + "建议升级到 Win10 或更高版本", Or);
        else
        {
            var winVer = osVer.Version.Build >= 22000 ? "11" : "10";
            Lg("系统版本: Windows " + winVer
                + " (Build " + osVer.Version.Build + ")", Txt2);
        }

        if (isPortable)
            Lg("本版本为便携版（无依赖版），已内置 .NET 10 运行环境",
                Gn);
        else
            Lg("本版本为有依赖版，需要系统安装 .NET 10 运行环境才能运行",
                Txt);

        // ---- 三级 .NET SDK 检测 ----
        string sdk = "未安装";
        Color c = Rd;
        bool sysOk = false, pfOk = false, localOk = false;

        // 第一级: 系统 PATH 中的 dotnet
        try
        {
            var p = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "dotnet",
                    Arguments = "--version",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            p.Start();
            var v = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit();
            if (p.ExitCode == 0 && !string.IsNullOrEmpty(v))
            {
                if (v.StartsWith("10."))
                {
                    sdk = "已就绪 v" + v; c = Gn; sysOk = true;
                }
                else
                    Lg("系统已安装 .NET v" + v
                        + "，但需要 ≥10.0 版本", Or);
            }
        }
        catch { }

        // 第二级: Program Files\dotnet (常见安装位置, x64)
        if (!sysOk)
        {
            var pfPaths = new[] {
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet", "dotnet.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet", "dotnet.exe")
            };
            foreach (var pfPath in pfPaths)
            {
                if (!File.Exists(pfPath)) continue;
                try
                {
                    var p = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = pfPath,
                            Arguments = "--version",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    p.Start();
                    var v = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();
                    if (p.ExitCode == 0 && !string.IsNullOrEmpty(v)
                        && v.StartsWith("10."))
                    {
                        sdk = "已就绪 v" + v + " (Program Files)";
                        c = Gn; pfOk = true; break;
                    }
                }
                catch { }
            }
        }

        // 第三级: 本地 dotnet-sdk 目录 (便携 SDK)
        if (!sysOk && !pfOk)
        {
            var localPath = Path.Combine(_ad, "dotnet-sdk",
                "dotnet.exe");
            if (File.Exists(localPath))
            {
                try
                {
                    var p = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = localPath,
                            Arguments = "--version",
                            UseShellExecute = false,
                            RedirectStandardOutput = true,
                            CreateNoWindow = true
                        }
                    };
                    p.Start();
                    var v = p.StandardOutput.ReadToEnd().Trim();
                    p.WaitForExit();
                    if (p.ExitCode == 0 && !string.IsNullOrEmpty(v)
                        && v.StartsWith("10."))
                    {
                        sdk = "便携SDK v" + v;
                        c = Or; localOk = true;
                    }
                }
                catch { }
                if (!localOk)
                {
                    sdk = "便携SDK (版本异常)";
                    c = Or; localOk = true;
                }
            }
        }

        lbSd.Text = ".NET SDK: [O] " + sdk;
        lbSd.ForeColor = c;

        // 输出检测结果到日志
        _hasSdk = sysOk || pfOk || localOk;
        if (sysOk || pfOk)
            Lg("检测到系统已安装 .NET 10 SDK，可用于编译服务端更新",
                Gn);
        else if (localOk)
            Lg("检测到本地便携 .NET SDK (dotnet-sdk)，可用于编译服务端更新",
                Gn);
        else if (isPortable)
        {
            Lg("未检测到 .NET 10 SDK，虽然本程序可运行，"
                + "但更新时无法编译服务端！", Rd);
            Lg("请将 dotnet-sdk 目录放入 AUM管理组件，"
                + "或手动安装 .NET 10 SDK", Rd);
        }
        else
        {
            Lg("未检测到 .NET 10 运行环境，本程序可能无法正常工作！",
                Rd);
            Lg("请安装 .NET 10.0 或改用便携版"
                + " (ServerUI-无依赖版.exe) 后重试", Rd);
        }
    }

    // =================================================================
    // DX 补丁处理 (Cd)
    // =================================================================
    /*
     * 处理 DX11/DX12 复选框变更
     *
     * 互斥逻辑: DX11 和 DX12 不能同时启用
     *
     * 文件操作: 根据勾选状态，把补丁文件复制到游戏根目录 (_gr)
     *   补丁文件: D3D9.dll, dgVoodoo.conf, dgVoodooCpl.exe
     *
     * 去水印: 如果勾选了"去除水印"，使用 "无水印" 子目录的版本
     *
     * 修改建议:
     *   - 想添加新的补丁文件? 修改 files[] 数组
     *   - 想禁用互斥逻辑? 删除 cbDx && cbDt 的判断
     *   - 想修改补丁目录名? 改 "DX11补丁" / "DX12补丁" 字符串
     */
    void Cd(object s, EventArgs e)
    {
        if (_cdBusy) return;  // 防止重入

        // DX11 和 DX12 互斥检测
        if (cbDx.Checked && cbDt.Checked)
        {
            _cdBusy = true;
            var clicked = (CheckBox)s;
            MessageBox.Show(
                "DX11 和 DX12 补丁不能同时启用，请只选择其中一个。",
                "冲突", MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            clicked.Checked = false;
            _cdBusy = false;
            return;
        }

        var files = new[] { "D3D9.dll", "dgVoodoo.conf",
            "dgVoodooCpl.exe" };
        string srcDir = null;
        string tag = "";

        // 确定补丁来源目录
        if (cbDt.Checked)
        {
            srcDir = Path.Combine(_ad, "DX12补丁");
            tag = " (DX12)";
            if (!Directory.Exists(srcDir))
            {
                Lg("DX12补丁目录不存在: " + srcDir, Or);
                return;
            }
            if (cbDw.Checked)
            {
                var wm = Path.Combine(srcDir, "无水印");
                if (Directory.Exists(wm))
                { srcDir = wm; tag = " (DX12无水印版)"; }
                else
                    Lg("DX12无水印目录不存在", Or);
            }
        }
        else if (cbDx.Checked)
        {
            srcDir = Path.Combine(_ad, "DX11补丁");
            tag = " (DX11)";
            if (!Directory.Exists(srcDir))
            {
                Lg("DX11补丁目录不存在: " + srcDir, Or);
                return;
            }
            if (cbDw.Checked)
            {
                var wm = Path.Combine(srcDir, "无水印");
                if (Directory.Exists(wm))
                { srcDir = wm; tag = " (DX11无水印版)"; }
                else
                    Lg("DX11无水印目录不存在", Or);
            }
        }
        else if (cbDw.Checked)
        {
            Lg("请先选择 DX11 或 DX12 运行模式再启用水印", Or);
            return;
        }

        // 复制补丁文件到游戏目录
        if (srcDir != null)
        {
            // 检查是否已经存在相同文件 (避免重复复制)
            var allExist = true;
            foreach (var fn in files)
            {
                var src = Path.Combine(srcDir, fn);
                var dst = Path.Combine(_gr, fn);
                if (!File.Exists(dst) || !File.Exists(src)
                    || new FileInfo(src).Length
                       != new FileInfo(dst).Length)
                { allExist = false; break; }
            }
            if (allExist)
            {
                Lg("DX补丁文件已存在于游戏目录，无需复制", Txt2);
                return;
            }

            foreach (var fn in files)
            {
                var src = Path.Combine(srcDir, fn);
                var dst = Path.Combine(_gr, fn);
                if (File.Exists(src))
                    File.Copy(src, dst, true);
            }
            Lg("DX补丁已复制到游戏目录" + tag, Gn);
        }
        else
        {
            // 两个都取消勾选 → 从游戏目录删除补丁文件
            foreach (var fn in files)
            {
                var dst = Path.Combine(_gr, fn);
                if (File.Exists(dst))
                { try { File.Delete(dst); } catch { } }
            }
            Lg("DX补丁已从游戏目录移除", Txt2);
        }
    }

    // =================================================================
    // .NET SDK 安装 (IS)
    // =================================================================
    /*
     * 通过 dotnet-install.ps1 自动下载安装 .NET 10 SDK
     * 安装位置: AUM管理组件\dotnet-sdk\
     *
     * 适用于: 有依赖版用户没有安装 .NET SDK，点击 [安装SDK] 按钮
     *
     * 注意: 下载大小约 280MB，需要稳定的网络连接
     *
     * 修改建议:
     *   - 想改安装路径? 改 sdkDir 变量
     *   - 想改 .NET 版本? 改 -Channel 10.0 参数
     *   - 想用 winget 安装? 替换 FileName 和 Arguments
     */
    async System.Threading.Tasks.Task IS()
    {
        btSdk.Enabled = false;
        btSdk.Text = "检测中...";

        var installer = Path.Combine(_ad, "dotnet-sdk",
            "dotnet-sdk-10.0.302-win-x64.exe");

        if (!File.Exists(installer))
        {
            if (_hasSdk)
                Lg(".NET 10 SDK 已就绪，但未找到安装包: " + installer, Or);
            else
                Lg("未找到 .NET 10 SDK 安装程序: " + installer, Rd);
            Lg("请自行下载 .NET 10.0 SDK (x64) 安装包放入 dotnet-sdk 目录，或运行 dotnet-install.ps1。", Rd);
            btSdk.Enabled = true;
            btSdk.Text = "安装SDK";
            return;
        }

        if (_hasSdk)
            Lg(".NET 10 SDK 已就绪，仍将打开安装包供你手动修复/覆盖安装。", Or);

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = installer,
                WorkingDirectory = Path.GetDirectoryName(installer),
                UseShellExecute = true
            };
            Process.Start(psi);
            Lg("已打开微软 .NET 10 SDK 安装程序。安装完成后请重启管理器，"
                + "再执行更新。", Gn);
        }
        catch (Exception ex)
        {
            Lg("无法启动 .NET 10 SDK 安装程序: " + ex.Message, Rd);
        }

        btSdk.Enabled = true;
        btSdk.Text = "安装SDK";
        await System.Threading.Tasks.Task.CompletedTask;
    }

    void CheckDnfExists()
    {
        var dnfPath = Path.Combine(_gr, "DNF.exe");
        if (!File.Exists(dnfPath))
            Lg("[警告] 本目录下并不存在 DNF.exe，请确认解压位置是否正确。当前目录: " + _gr, Or);
        else
            Lg("[检查] DNF.exe 已找到: " + dnfPath, Gn);
    }

    async System.Threading.Tasks.Task CheckBasicNetwork()
    {
        Lg(">>> 正在检测网络可达性 (网页检测, 无API调用)...", Color.CornflowerBlue);
        try
        {
            var basic = await _up.CheckBasicConnectivityAsync();
            foreach (var kv in basic)
            {
                var name = kv.Key;
                var ms = kv.Value.LatencyMs;
                var reachable = kv.Value.Reachable;
                string tier, msg; Color color;
                if (!reachable)
                {
                    tier = "不可达"; msg = name + " " + tier + " (超时)";
                    color = Rd;
                }
                else if (ms <= 800)
                {
                    tier = "正常"; msg = name + " " + tier + " (延迟 " + ms + " ms)";
                    color = Gn;
                }
                else if (ms <= 3000)
                {
                    tier = "较慢"; msg = name + " " + tier + " (延迟 " + ms + " ms), 建议开启科学上网";
                    color = Or;
                }
                else
                {
                    tier = "极慢"; msg = name + " " + tier + " (延迟 " + ms + " ms), 更新可能失败";
                    color = Rd;
                }
                Lg("[网络] " + msg, color);
            }
        }
        catch { Lg("[网络] 检测异常，不影响正常使用。", Txt2); }
    }

    async System.Threading.Tasks.Task<bool> CanUpdate()
    {
        Lg(">>> 更新前检查仓库连接...", Color.CornflowerBlue);
        var status = await _up.CheckRepositoryAsync();
        if (!status.Available || status.LatencyMs > 3000)
        {
            var reason = status.Available
                ? "连接延迟极高（" + status.LatencyMs + " ms）"
                : "无法连接（" + status.Detail + "）";
            Lg("[网络降级] 仓库" + reason
                + "。将自动重试并改用源码包同步；建议开启科学上网（梯子）提高成功率。", Or);
        }

        // v1.911: 更新前才验证 API 令牌和镜像源（不在启动时消耗 API 配额）
        _ = ValidateMirrorTokens();
        Lg(">>> 更新前检测镜像源: GitHub / Codeberg ...", Color.CornflowerBlue);
        try
        {
            var mirrors = await _up.CheckMirrorSourcesAsync();
            foreach (var kv in mirrors)
            {
                var ok = kv.Value.Available;
                var ms = kv.Value.LatencyMs;
                var color = ok ? Gn : Or;
                var tag = ok ? "可访问" : "不可达";
                Lg("[镜像] " + kv.Key + " " + tag + " (延迟 " + ms + " ms)", color);
            }
        }
        catch { Lg("[镜像] 检测异常，不影响正常使用。", Txt2); }

        return true;  // 始终允许继续，update.ps1 内部会做源选择
    }

    async System.Threading.Tasks.Task CheckAUMUpdate()
    {
        Lg(">>> 正在检测 AUM 管理器更新...", Color.CornflowerBlue);
        _au.OutputReceived += Lg;
        try
        {
            var hasUpdate = await _au.CheckForUpdateAsync(VER);
            if (_au.RemoteVersion == null)
            {
                // 网络失败，OutputReceived 已经输出了日志
            }
            else if (hasUpdate)
            {
                Lg("[AUM自检] 发现新版本 v" + _au.RemoteVersion + "！当前版本 v" + VER + "，请点击顶栏【更新AUM】升级。", Gn);
            }
            else if (_au.CompareVersion(_au.RemoteVersion, VER) < 0)
            {
                Lg("[AUM自检] 当前为开发版 v" + VER + "（高于仓库 v" + _au.RemoteVersion + "），无需更新。", Txt2);
            }
            else
            {
                Lg("[AUM自检] 已是最新版本 v" + VER, Txt2);
            }
        }
        finally { _au.OutputReceived -= Lg; }
    }

    async System.Threading.Tasks.Task CheckAndUpdateAUM()
    {
        if (!_hasSdk)
        {
            Lg("[AUM更新] 需要 .NET 10 SDK 才能自更新，请先点击【安装NET.10 SDK】安装。", Rd);
            return;
        }

        Lg(">>> 正在连接 GitHub 检测 AUM 管理器更新...", Color.CornflowerBlue);
        var hasUpdate = await _au.CheckForUpdateAsync(VER);

        if (!hasUpdate)
        {
            if (_au.RemoteVersion != null)
            {
                if (_au.CompareVersion(_au.RemoteVersion, VER) < 0)
                    Lg("[AUM更新] 当前已是开发版 v" + VER + "，无需降级。", Txt2);
                else
                    Lg("[AUM更新] 已是最新版本 v" + VER, Gn);
            }
            return;
        }

        Lg("[AUM更新] 发现新版本 v" + _au.RemoteVersion + "，当前 v" + VER, Gn);
        Lg("[AUM更新] 开始自动下载源码并编译...", Color.CornflowerBlue);

        _au.OutputReceived += Lg;
        _au.Completed += (ok) =>
        {
            if (ok) Lg("[AUM更新] 编译成功，即将自动重启...", Gn);
            else Lg("[AUM更新] 更新流程中断，可稍后重试。", Or);
        };

        try
        {
            await _au.RunUpdateAsync(Path.Combine(_ad,
                Directory.Exists(Path.Combine(_ad, "ServerUI"))
                    ? "ServerUI"
                    : "."));
        }
        finally
        {
            _au.OutputReceived -= Lg;
        }
    }

    async System.Threading.Tasks.Task TryMirrorUpload()
    {
        if (!_mirrorOk)
        {
            Lg("[镜像] API令牌无法生效，请更新AUM版本。", Rd);
            return;
        }

        await System.Threading.Tasks.Task.Delay(5000);
        if (await _mu.CanReachGitGud())
        {
            Lg("[镜像] 检测到可访问 GitGud，尝试同步镜像...", Txt2);
            _mu.OutputReceived += Lg;
            try
            {
                await _mu.RunUploaderAsync(VER, Environment.MachineName);
            }
            catch (Exception ex)
            {
                Lg("[镜像] 同步异常: " + ex.Message, Or);
            }
            finally { _mu.OutputReceived -= Lg; }
        }
        else
        {
            Lg("[镜像] 无法访问 GitGud，跳过镜像上传。", Txt2);
        }
    }

    async System.Threading.Tasks.Task ValidateMirrorTokens()
    {
        try
        {
            var ok = await _mu.ValidateTokensAsync();
            _mirrorOk = ok;
            if (!ok)
                Lg("[令牌检测] API令牌无法生效，请更新AUM版本。已禁用镜像上传。", Rd);
            else
                Lg("[令牌检测] API令牌正常，众包镜像可用。", Txt2);
        }
        catch
        {
            _mirrorOk = false;
            Lg("[令牌检测] API令牌无法生效，请更新AUM版本。", Rd);
        }
    }

    // =================================================================
    // 状态刷新 (Rs / Rf / RA)
    // =================================================================

    /*
     * 状态刷新 (Rs) — 每 2 秒执行一次
     *
     * 检测:
     *   - bat 进程存活 (通过持有的进程句柄)
     *   - DfoServer 进程存活 (通过进程名+路径匹配)
     *
     * 显示逻辑:
     *   bat 存活 + DfoServer 存活 → 绿 "[O] 服务端 运行中"
     *   bat 退出 + DfoServer 残留 → 红 "[O] 未运行" + 自动清理
     *   两者都不存在              → 红 "[O] 未运行"
     *
     * 孤儿进程告警: _orphanLogged 确保每次异常只记录一次 (不会刷屏)
     */
    void Rs()
    {
        var distDir = Path.Combine(_ad, "ServerS4A12-AUM",
            "dist", "win-x64");
        bool bat = _sv.IsBatRunning;
        bool dfo = ServerService.IsDfoServerRunning(distDir);

        if (bat && dfo)
        {
            // 正常: 两个进程都在
            lbSt.Text = "[O] 服务端 运行中";
            lbSt.ForeColor = Gn;
            _orphanLogged = false;
        }
        else if (!bat && dfo)
        {
            // 异常: bat 已退出但 DfoServer 还在
            lbSt.Text = "[O] 未运行";
            lbSt.ForeColor = Rd;
            if (!_orphanLogged)
            {
                _orphanLogged = true;
                System.Threading.Tasks.Task.Run(() =>
                {
                    Lg(">>> 检测到DfoServer残留进程,"
                        + " 正在自动清理...", Or);
                    ServerService.CleanOrphans();
                });
            }
        }
        else
        {
            // 正常: 两个进程都不在
            lbSt.Text = "[O] 未运行";
            lbSt.ForeColor = Rd;
            _orphanLogged = false;
        }

        // 更新 PVF 状态
        lbPv.Text = _sv.PvfExists(
            Path.Combine(_ad, "ServerS4A12-AUM"))
            ? "PVF: [O] 已加载" : "PVF: [O] 未找到";
        lbPv.ForeColor = _sv.PvfExists(
            Path.Combine(_ad, "ServerS4A12-AUM")) ? Gn : Rd;

        // 更新版本信息 (从更新日志.txt 读取最新版本)
        var vf = Path.Combine(_ad, "更新日志.txt");
        if (File.Exists(vf))
        {
            var tx = File.ReadAllText(vf,
                System.Text.Encoding.UTF8);
            var ix = tx.LastIndexOf("版本:");
            if (ix >= 0)
            {
                var en = tx.IndexOf('\n', ix);
                if (en < 0) en = Math.Min(ix + 20, tx.Length);
                lbLu.Text = "上次更新: "
                    + tx.Substring(ix, en - ix).Trim()
                        .Replace("版本:", "").Trim();
            }
            else
                lbLu.Text = "上次更新: 尚未有log日志无法识别版本，请进行更新";
        }
        else
            lbLu.Text = "上次更新: 尚未有log日志无法识别版本，请进行更新";

        lbVe.Text = "  |  版本: " + _up.GetVersion(_ad);
    }

    /*
     * 刷新一切 (Rf) — 调用 Rs() + RA() 刷新状态和存档列表
     */
    void Rf() { Rs(); RA(); }

    /*
     * 刷新存档列表 (RA)
     *
     * 操作:
     *   1. 清空当前列表
     *   2. 从切换库加载所有 .db 文件
     *   3. 按修改时间排序 (正序/倒序由 _sa 控制)
     *   4. 更新"当前"和"备份数"显示
     *   5. 如果勾选了 [清理冗余DB]，自动执行清理
     */
    void RA()
    {
        lv.Items.Clear();
        var list = _ar.List(_ad);
        var o = _sa
            ? list.OrderBy(a => a.Modified).ToList()
            : list.OrderByDescending(a => a.Modified).ToList();

        for (int i = 0; i < o.Count; i++)
        {
            var it = new ListViewItem((i + 1).ToString());
            it.SubItems.Add(o[i].Name);
            it.SubItems.Add(o[i].SizeDisplay);
            it.SubItems.Add(o[i].Modified.ToString("yyyy-MM-dd HH:mm"));
            lv.Items.Add(it);
        }

        lbCu.Text = "当前: " + _ar.CurrentInfo(_ad);
        lbBk.Text = "备份数: " + _ar.BackupCount(_ad);

        // 如果勾选了清理冗余 DB，自动执行
        if (cbCl != null && cbCl.Checked) CleanRedundantDb();
    }

    // =================================================================
    // 更新操作 (RI / RF / OU / OD)
    // =================================================================

    /*
     * 增量更新 (RI)
     *
     * 流程:
     *   1. 检测服务端是否在运行 → 自动停止
     *   2. 启动进度条
     *   3. 调用 UpdateService.RunIncremental()
     *   4. 完成后清理事件订阅
     *
     * 修改建议:
     *   - 想修改等待时间? 改 Thread.Sleep(2000)
     *   - 想屏蔽自动停服? 删除 _sv.Stop() 相关内容
     */
    async System.Threading.Tasks.Task RI()
    {
        if (!await CanUpdate()) return;
        _ = TryMirrorUpload();
        if (_sv.IsRunning)
        {
            Lg(">>> 检测到服务端正在运行，"
                + "正在自动停止以执行增量更新...", Color.Gold);
            _sv.Stop();
            System.Threading.Thread.Sleep(2000);
            Lg(">>> 服务端已停止，开始更新", Gn);
        }

        Lg(">>> 更新前清理冗余DB...", Gn);
        CleanRedundantDb();

        pb.Visible = true; lbPg.Visible = true;
        pb.Value = 0; _pv = 0; _stepTarget = 5;
        if (cbSkipLog.Checked)
            Lg(">>> [跳过更新日志] 已启用，本次不拉取仓库提交记录", Or);
        Lg(">>> 开始增量更新 <<<", Color.CornflowerBlue);
        _pt.Start();

        _up.OutputReceived += OU;
        _up.Completed += OD;
        try
        {
            await _up.RunIncremental(
                Path.Combine(_ad, "ServerS4A12-AUM"), _ad, cbSkipLog.Checked, cbMirror.Checked);
        }
        finally
        {
            // 确保无论如何都会取消订阅 (防止内存泄漏)
            _up.OutputReceived -= OU;
            _up.Completed -= OD;
            _pt.Stop();
        }
    }

    /*
     * 全量更新 (RF) — 与增量更新流程相同，加上 -FullSync 参数
     */
    async System.Threading.Tasks.Task RF()
    {
        if (!await CanUpdate()) return;
        if (_sv.IsRunning)
        {
            Lg(">>> 检测到服务端正在运行，"
                + "正在自动停止以执行全量更新...", Color.Gold);
            _sv.Stop();
            System.Threading.Thread.Sleep(2000);
            Lg(">>> 服务端已停止，开始更新", Gn);
        }

        Lg(">>> 更新前清理冗余DB...", Gn);
        CleanRedundantDb();

        pb.Visible = true; lbPg.Visible = true;
        pb.Value = 0; _pv = 0; _stepTarget = 5;
        if (cbSkipLog.Checked)
            Lg(">>> [跳过更新日志] 已启用，本次不拉取仓库提交记录", Or);
        Lg(">>> 开始全量更新 <<<", Color.CornflowerBlue);
        _pt.Start();

        _up.OutputReceived += OU;
        _up.Completed += OD;
        try
        {
            await _up.RunFull(
                Path.Combine(_ad, "ServerS4A12-AUM"), _ad, cbSkipLog.Checked, cbMirror.Checked);
        }
        finally
        {
            _up.OutputReceived -= OU;
            _up.Completed -= OD;
            _pt.Stop();
        }
    }

    /*
     * 更新输出回调 (OU) — 每收到一行 PowerShell 输出时调用
     * 日期行 (如 "--- 2026-07-15 ---") 用青色显示
     * 其他行用默认颜色
     */
    void OU(string m)
    {
        // 处理进度标记 (##PROGRESS##N)
        if (m.StartsWith("##PROGRESS##"))
        {
            var val = m.Substring("##PROGRESS##".Length);
            if (int.TryParse(val, out var pct))
            {
                if (pct > _stepTarget && pct <= 95)
                {
                    _stepTarget = pct;
                    _pv = Math.Max(_pv, pct - 5);
                }
                pb.Value = Math.Min(_pv, 95);
                lbPg.Text = "更新进度: " + pb.Value + "%";
            }
            return;
        }

        if (m.StartsWith("[FILE:CS]"))
        {
            Lg(m.Substring("[FILE:CS]".Length).TrimStart(), Gn);
            return;
        }
        if (m.StartsWith("[FILE:SUM]"))
        {
            Lg(m.Substring("[FILE:SUM]".Length).TrimStart(), Or);
            return;
        }

        if (System.Text.RegularExpressions.Regex.IsMatch(m,
            @"^--- \d{4}-\d{2}-\d{2}"))
            Lg(m, Cy);
        else Lg(m);

        var sm = System.Text.RegularExpressions.Regex.Match(m, @"\[(\d)/5\]");
        if (sm.Success && int.TryParse(sm.Groups[1].Value, out var step))
        {
            _stepTarget = step switch { 1 => 5, 2 => 25, 3 => 55, 4 => 85, 5 => 93, _ => _stepTarget };
            if (_pv < _stepTarget - 10) _pv = _stepTarget - 10;
            pb.Value = Math.Min(_pv, 95);
            lbPg.Text = "更新进度: " + pb.Value + "%";
        }
    }

    /*
     * 更新完成回调 (OD)
     *
     * 操作:
     *   1. 进度条跳到 100%
     *   2. 显示完成/失败消息
     *   3. 等待 1.5 秒让用户看到完成状态
     *   4. 隐藏进度条
     *   5. 刷新界面
     */
    void OD(bool ok)
    {
        pb.Value = 100;
        lbPg.Text = "100%";
        if (ok)
        {
            LS(">>> 更新完成！如果更新没有效果，"
                + "请尝试再次点击更新或者全量更新。<<<");
            Lg("========================================", Cy);
            Lg("  更新已完成，将在目录【\\AUM管理组件】生成一份运行日志", Color.Gold);
            Lg("========================================", Cy);
        }
        else
            Lg(">>> 更新失败，请检查网络连接或查看上方日志。<<<",
                Color.Orange);

        System.Threading.Thread.Sleep(1500);
        pb.Visible = false;
        lbPg.Visible = false;
        Rf();
    }
}
