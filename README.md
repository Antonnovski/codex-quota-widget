# Codex Quota Widget

一款面向 Windows 的 Codex 额度桌面小组件。它会读取当前 Codex 登录账户的额度窗口，在桌面上显示剩余百分比、重置时间和实时倒计时。

当前界面采用 Roselia 灵感的深蓝紫色 Liquid Glass 风格，五小时额度与每周额度并排显示。

## 功能

- 显示五小时额度与每周额度的剩余百分比
- 显示北京时间（UTC+8）的重置时间和秒级倒计时
- 每分钟自动同步，也可以手动刷新
- 支持窗口拖动、始终置顶和位置记忆
- 同步失败时保留上一次成功读取的数据
- 单实例运行，避免重复打开多个小组件
- 不需要 API Key，不调用模型，不消耗额度

## 运行要求

- Windows 10/11
- [.NET 8 Desktop Runtime](https://dotnet.microsoft.com/download/dotnet/8.0)
- 已安装并登录 Codex 桌面应用或 Codex CLI

## 从源码运行

```powershell
dotnet run --project QuotaWidget.csproj
```

## 构建

```powershell
dotnet build -c Release
dotnet publish -c Release -o publish
```

生成的程序位于 `publish/CodexQuota.exe`。发布目录中的 `.dll`、`.deps.json` 和 `.runtimeconfig.json` 文件需要和程序放在同一目录。

## 数据来源与隐私

程序通过本机 `codex app-server --stdio` 连接当前 Codex 登录会话，并调用 `account/rateLimits/read` 获取额度数据。官方协议字段包括 `usedPercent`、`windowDurationMins` 和 `resetsAt`。

- 不读取或保存密码、Token、API Key
- 不向第三方服务上传数据
- 不调用模型，不产生推理请求
- 仅在本地保存窗口位置

额度接口说明见 [Codex App Server 官方文档](https://learn.chatgpt.com/docs/app-server#6-rate-limits-chatgpt)。

## 项目结构

```text
.
├── Program.cs                 # 界面、额度读取与窗口行为
├── QuotaWidget.csproj         # WPF 项目配置
├── global.json                # .NET SDK 版本配置
└── .github/workflows/build.yml
```

## 说明

这是个人制作的非官方项目，与 OpenAI、BanG Dream!、Roselia 或 Bushiroad 无隶属或授权关系。项目中的主题仅为视觉灵感表达，未包含官方图片资源。

本仓库暂未授予开源许可证。未经版权所有者许可，不得复制、修改或再发布代码。

