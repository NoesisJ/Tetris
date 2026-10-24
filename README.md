# 俄罗斯方块（C# / .NET 10）

本项目是一个 12 列 × 16 行的控制台俄罗斯方块。游戏规则与终端输入、显示分层；包含开始菜单、暂停/继续、单人和本地双人对战、AI 观战、录制与回放、可注入计分策略、XML 最高分存档和 xUnit 测试。

## 项目位置

项目根目录为 `E:\CodeZone\c#\Tetris`，在 VS Code 中打开该文件夹，或打开其中的 `Tetris.slnx`。所有解决方案、项目引用和 VS Code 任务均使用相对路径，因此移动整个 `Tetris` 文件夹时无需逐项改写源码路径。

## 环境要求

- Windows x64
- .NET 10 SDK（开发、构建和测试）
- VS Code 可选；安装 Microsoft C# Dev Kit 扩展可获得补全和调试支持
- 交互运行需要 VS Code 集成终端或 Windows Terminal，建议窗口至少 80 列 × 23 行

## 构建、运行与测试

在项目根目录执行：

```powershell
dotnet restore Tetris.slnx
dotnet build Tetris.slnx -c Release
dotnet test Tetris.slnx -c Release
dotnet run --project src/Tetris.Console/Tetris.Console.csproj
```

## 直接运行游戏

如果只想玩游戏，不需要打开 VS Code，也不需要输入 `dotnet run`：

1. 在文件资源管理器中打开 `E:\CodeZone\c#\Tetris\artifacts\publish\win-x64`。
2. 双击 `Tetris.Console.exe` 启动游戏。
3. 单人和 AI 模式将窗口调整到至少 80 列 × 23 行；双人模式建议至少 110 列 × 23 行。启动后按 `1`、`2` 或 `3` 选择模式。
4. 对战时按 `P` 暂停/继续，按 `Esc` 返回模式菜单；按 `Q` 退出。

这是 Windows x64 自包含发布版，运行电脑无需另装 .NET Runtime。若尚未生成发布文件，先在项目根目录打开 PowerShell，执行下面的发布命令。也可以直接在项目根目录的 PowerShell 中运行已发布版本：

```powershell
.\artifacts\publish\win-x64\Tetris.Console.exe
```

生成或更新 Windows x64 独立发布目录（不要求目标机器预装 .NET Runtime）：

```powershell
dotnet publish src/Tetris.Console/Tetris.Console.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
```

发布成功后，可运行 `artifacts/publish/win-x64/Tetris.Console.exe`。

## 操作说明

| 按键 | 操作 |
|---|---|
| 按键 | 玩家一 / 单人 |
|---|---|
| A / D | 左右移动 |
| W | 顺时针旋转 |
| S | 加速下落一格 |
| 空格 | 硬降 |

双人模式下，玩家二使用 `← / →` 移动、`↑` 旋转、`↓` 加速下落、`Enter` 硬降。每次一次消除 2 行或以上时，会向对手棋盘增加等量垃圾行；垃圾行有一个随机空格，若垃圾把已有方块顶出棋盘则该玩家失败。一次消除只发送一次攻击，后续移动不会重复发送。

`M` 可切换 AI 自动玩家：单人模式由 AI 接管当前棋盘；双人模式由 AI 接管玩家二；菜单中的 `3` 可直接进入 AI 观战演示。AI 评估旋转和落点，考虑高度、空洞、棋面凹凸和消行数。

每局记录确定性随机种子以及带时间戳的玩家操作、自动下落和 AI 回合。按 `F5` 将当前记录保存为 JSON 到 `%LOCALAPPDATA%\Tetris\replays`；按 `F6` 加载最近一局并回放，`F8` 暂停/继续回放，`+/-` 调整速度（0.25× 至 4×）。存档目录不可写或文件损坏时，游戏继续运行并显示提示。

通用控制：`1` 单人、`2` 双人、`3` AI 观战；`P` 暂停/继续，`R` 重开当前模式，`Esc` 返回模式菜单，`Q` 退出。

分数规则为每次消除 `100 × 行数` 分；分数与消行数量均以十进制显示。核心引擎通过 `IScoringRule` 注入计分策略，默认 `StandardScoringRule` 实现作业要求的规则；新增策略时实现接口并在创建 `GameEngine` 时传入，无需修改引擎。每累计消除 10 行升一级，下落间隔从 800 毫秒开始，每级缩短 70 毫秒，最短 100 毫秒。AI 会模拟不同旋转与落点，综合棋面高度、空洞、凹凸度和消行数评分。

最高分 XML 文件保存在 `%LOCALAPPDATA%\Tetris\highscore.xml`。缺失或损坏时从 0 分启动；无法写入时会提示，但本局仍可继续。

## 工程结构

- `src/Tetris.Core/Domain`：方块、棋盘和可复制的游戏状态。
- `src/Tetris.Core/Game`：随机方块袋、下落、碰撞、消行、计分和等级。
- `src/Tetris.Core/AI`：自动玩家的落点评估。
- `src/Tetris.Core/Persistence`：XML 高分存档和失败恢复。
- `src/Tetris.Console`：开始/暂停/结束流程、双人攻击规则、AI 控制、回放录制/调度/JSON 存储、输入适配、帧格式化和终端渲染。
- `tests/Tetris.Tests`：核心逻辑、存档、AI、控制器、输入映射和界面格式化测试。

## 测试覆盖

运行 `dotnet test Tetris.slnx`。测试覆盖棋盘边界与占用碰撞、垃圾行位移和溢出、七种方块旋转、棋盘和游戏状态复制、默认及注入计分规则、行消除与计分、等级加速、出生阻塞、随机方块袋、AI 合法落点、双人独立操作和多行攻击、菜单画面清理、回放文件读写/事件调度/种子重演、XML 异常恢复、输入映射和终端画面格式。终端尺寸变化、实际键盘和发布版运行还需在目标终端中做人工验收。

## 开发记录和声明

Git 提交应随开发过程逐步产生，并覆盖至少 4 个不同日期；不要伪造提交时间。研究报告和演示视频需在实际运行、测量与验收后补齐。AI 使用声明草稿见 [`AI使用声明.md`](AI使用声明.md)，提交前请按自己的实际审阅和使用情况确认修改。
