# Card Stats Tracker

Slay the Spire 2 模组 - 追踪每张卡牌的打出次数。

## 安装

将 `CardStats.dll`、`CardStats.json` 和 `CardStats.pck`放入游戏根目录的 `mods/CardStats` 文件夹：

```
Steam/steamapps/common/Slay the Spire 2/mods/CardStats/
```

## 使用

在**牌组界面**、**牌堆界面**和**历史界面**，左下角勾选「查看打出次数」，查看卡牌打出次数统计。

## 说明

战斗中保存并退出，统计数据回退到战斗开始时。
目前不兼容快速SL mod，使用快速SL不会回退次数统计，后续版本可能会支持。

## 编译

仅使用模组无需编译，直接下载 release 文件夹中的文件即可。

如需自行编译：
1. 安装 .NET SDK 9.0
2. 修改 `CardStats.csproj` 中的 `Sts2Dir` 为你的游戏安装路径
3. 运行 `dotnet build`

## 其他

代码使用 AI 辅助生成。

## TODO

- [x] 修复漏洞
  - [x] 修复战斗外升级卡牌会清除统计的问题
  - [x] 修复战斗内升级卡牌会清除统计的问题
  - [x] 修复牌堆显示/隐藏切换异常的问题
  - [ ] 其他可能存在的漏洞
- [ ] 支持兼容快速SL mod
- [ ] 增加更多统计信息，如卡牌的抽到次数、弃牌次数、消耗次数
- [ ] 多语言支持

## 许可证

CC BY-NC 4.0
