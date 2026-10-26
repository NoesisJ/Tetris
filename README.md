# 俄罗斯方块

使用 C# 和 .NET 10 开发的控制台俄罗斯方块，棋盘为 12 列 × 16 行。项目提供单人、双人本地对战和 AI 模式，并包含 XML 最高分存档、JSON 录制回放和 xUnit 单元测试。

## 功能

- 单人游戏：移动、旋转、加速下落、硬降、消行、计分、等级加速、暂停和游戏结束判定。
- 双人本地对战：两块棋盘并排显示、各自操作；一次消除两行或以上时向对手发送垃圾行。
- AI 玩家：使用棋面高度、空洞、凹凸度和消行数评估落点；AI 每次只调整方向或横移一步，方块按正常重力下落。
- 录制与回放：记录随机种子、玩家操作、自动下落和 AI 回合，可保存、重放、暂停和调整播放速度。
- 可扩展计分：通过 `IScoringRule` 注入规则，默认每消除一行得 100 分。
- 高分持久化：最高分存入本机 XML 文件，文件缺失或损坏时从 0 分启动。
- 历史成绩与排行榜：每局结束后将成绩写入 SQLite，菜单按 `4` 查看前 10 名。

## 环境要求

- Windows x64
- .NET 10 SDK
- VS Code 可选；建议安装 Microsoft C# Dev Kit
- 交互运行请使用 VS Code 集成终端或 Windows Terminal。单人模式建议至少 80 列 × 23 行，双人模式至少 110 列 × 23 行。

## 构建、测试和运行

在仓库根目录打开 PowerShell：

```powershell
dotnet restore Tetris.slnx
dotnet build Tetris.slnx -c Release
dotnet test Tetris.slnx -c Release
dotnet run --project src/Tetris.Console/Tetris.Console.csproj
```

## 操作

开始菜单按 `1` 进入单人、`2` 进入双人、`3` 进入 AI 观战。玩家一使用 WASD，玩家二使用方向键；两名玩家均可按硬降键将当前方块直接落下。

| 按键 | 操作 |
| --- | --- |
| `A` / `D` | 玩家一左右移动 |
| `W` | 玩家一顺时针旋转 |
| `S` | 玩家一加速下落一格 |
| `Space` | 玩家一硬降 |
| `←` / `→` | 玩家二左右移动 |
| `↑` | 玩家二顺时针旋转 |
| `↓` | 玩家二加速下落一格 |
| `Enter` | 玩家二硬降 |
| `P` | 暂停 / 继续 |
| `M` | 切换 AI（仅单人/双人游戏可用；AI 观战模式禁止人工接管） |
| `R` | 重开当前模式 |
| `Esc` | 返回主菜单 |
| `Q` | 退出游戏 |
| `4`（主菜单） | 查看历史排行榜 |
| `F5` | 保存当前对局回放 |
| `F6` | 播放最近保存的回放 |
| `F8` | 暂停 / 继续回放 |
| `+` / `-` | 提高 / 降低回放速度（0.25× 至 4×） |
| `F9` | AI 观战模式切换普通 / 2 倍速 |

双人对局中，一次消除两行或以上会向对方棋盘增加等量垃圾行。垃圾行会留一个随机空格；若垃圾行将已有方块顶出棋盘，该玩家失败。

## 游戏规则与存档

- 每次消除 `k` 行得 `100 × k` 分。分数和消行数以十进制显示。
- 每累计消除 10 行升一级。下落间隔从 800 毫秒开始，每级缩短 70 毫秒，最短 100 毫秒。
- 最高分 XML：`%LOCALAPPDATA%\Tetris\highscore.xml`
- 成绩历史 SQLite：`%LOCALAPPDATA%\Tetris\scores.db`
- 回放 JSON：`%LOCALAPPDATA%\Tetris\replays`

每局结束后写入玩家、分数、消行数、模式和结束时间；排行榜按分数从高到低排列，同分时优先显示较早完成的记录。双人局会分别保存双方成绩。数据库不可写或损坏时会提示用户，但游戏仍可继续；XML 存档不可写时也会提示，无法读取或损坏的回放文件会被跳过。

## 发布 Windows 独立运行版

在仓库根目录执行：

```powershell
dotnet publish src/Tetris.Console/Tetris.Console.csproj -c Release -r win-x64 --self-contained true -o artifacts/publish/win-x64
```

发布后运行 `artifacts/publish/win-x64/Tetris.Console.exe`。该版本已包含 .NET 运行时，目标电脑无需另装 .NET Runtime。

## 项目结构

```text
src/
  Tetris.Core/       游戏领域模型、棋盘、游戏引擎、随机方块、计分策略和 SQLite 成绩仓储
  Tetris.Console/    控制台输入与渲染、游戏控制器、AI 集成和回放
tests/
  Tetris.Tests/      xUnit 单元测试
```

游戏逻辑位于 `Tetris.Core`，与控制台渲染和输入分离。`GameState.Clone()` 可创建独立状态副本；可通过实现 `IScoringRule` 并在创建 `GameEngine` 时注入，扩展计分方式而不修改引擎。

## 测试

运行 `dotnet test Tetris.slnx -c Release`。测试覆盖棋盘边界与碰撞、方块旋转、状态复制、消行和计分、等级加速、AI 落点、双人棋盘隔离和攻击、回放读写与重演、XML 存档异常恢复、输入映射及界面格式化。

## 开发记录与 AI 使用声明

本仓库提交历史记录开发过程。项目使用 AI 辅助需求分析、代码实现和测试设计，具体说明见 [AI 使用声明](AI使用声明.md)。研究报告和演示视频按课程要求另行提交。

## 提交材料与验收辅助文件

- 研究报告源稿：[研究报告_俄罗斯方块.md](研究报告_俄罗斯方块.md)
- 研究报告文档：[研究报告_俄罗斯方块.docx](研究报告_俄罗斯方块.docx)
- 验收清单：[docs/验收清单.md](docs/验收清单.md)
- 数据库查看说明：[docs/数据库查看说明.md](docs/数据库查看说明.md)
- 演示视频脚本：[docs/演示视频脚本.md](docs/演示视频脚本.md)
- 测试与发布记录：[docs/测试与发布记录_2026-10-26.md](docs/测试与发布记录_2026-10-26.md)
- 提交前检查清单：[docs/提交前检查清单.md](docs/提交前检查清单.md)
