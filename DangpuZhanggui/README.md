# 深空当铺mod局外管理

《深空当铺：可能是偷的》（Probably Stolen）的局外模组管理器，**1.1 测试版**。作者 **totrymod**。以前叫「当铺掌柜」，3.1 起改名。布局照着 PCL2 的路子：左边导航、卡片列表、勾选启停。这一版保留科幻终端，同时提供三套界面风格（终端 / 夜班 / 石墨）；新增整合包制作/导入/安装，管理器会自动读取 Steam build，热修也能发现。背景图和背景音乐都不内置，全部由玩家自己选。

非官方工具，与 Questing Goose Studio / tinyBuild 无关。

## 直接运行

双击 `深空当铺mod局外管理.exe`。

- 需要 **.NET 10 桌面运行时**（`Microsoft.WindowsDesktop.App` 10.0.x）。没装的话去 `dotnet.microsoft.com/download/dotnet/10.0` 下 Desktop Runtime。
- 第一次打开会自动去 Steam 库里找游戏；找不到就到「设置」里指定 `Probably Stolen.exe` 所在的那一层。
- 没签名，SmartScreen 可能会弹一次「仍要运行」。

## 能干什么

**模组**
- 列出 `Mods` 里的 dll 和 Melon 子文件夹；勾选就是启用/停用（把文件名改成 `.dll.disabled`，不动文件内容，随时能改回来）。
- 把 dll、zip 或**整个文件夹**拖到窗口任意位置就是安装：拖动时整个窗口会变成投放区，显示要装的文件名；松手后跳到模组页并选中新装的那个。文件夹会递归找里面的 dll 和 zip，非模组文件忽略。
- 列表下方常驻一块虚线投放区，列表为空时整块空状态也是投放区。也可以用工具栏的「导入文件夹」。
- 同名文件会先留一份 `.old-时间戳`；直接放进游戏 `Mods` 文件夹的模组不用“导入”，刷新就能看到。
- 删除走回收站，不直接抹掉。
- 工具栏可以**批量全部启用/停用**，按名称、修改时间、启用优先、问题优先循环排序。
- 「导出列表」把全部模组导成 Markdown 或 CSV：中文名、英文名、程序集、版本、作者、开关状态、日志状态、大小、修改时间、依赖、体检问题、来源和路径。CSV 带 BOM，Excel 直接打开不乱码。
- 自动读游戏目录里的 `UserData/ModManager/mod-details.json`，把中文名和简介对上；有来源链接的会显示「打开模组页面」。

**加载器（MelonLoader）**
- 「设置」里能看到加载器装没装、版本、是否被停用、IL2CPP 程序集有没有生成。
- 一键安装 / 修复：从 GitHub 官方 release 拉 `MelonLoader.x64.zip`（约 19 MB），带进度和取消，断线自动续传。下载过一次就缓存在 `%LOCALAPPDATA%\DangpuZhanggui\loader`，之后可以离线装。
- 网络不通也行：手动下载 zip 后用「从 zip 安装」；或者用「导出当前加载器」把现有这套（`version.dll` + `MelonLoader/`，不含日志和生成好的程序集，约 35 MB）打包带走，换台机器直接装。
- 想要完全离线：把官方的 `MelonLoader.x64.zip` 放在 exe 旁边，工具会直接认它，不用联网也不用缓存。
- 「启用」开关改的是 `UserData/Loader.cfg` 里的 `disable`，等价于 `--no-mods`，不用删文件。
- 换版本时如果跨了小版本（比如 0.6 → 0.7），旧的 `MelonLoader` 目录会被改名留底；同版本修复是覆盖式，保留已经生成好的程序集。

**整合包**
- 左边「整合包」页可以把当前**已启用**的模组和可选的 `MelonPreferences.cfg` 打成一个 `.dspmp`（本质是 zip，里面有 `modpack.json` 和 `mods/`）。
- 包里有名称、版本、作者、说明、游戏版本/build、每个模组的 SHA-256；文件夹模组会整个带进去。
- 「导入整合包」会把包复制到 `%LOCALAPPDATA%\DangpuZhanggui\modpacks`；选中后能看模组清单，点「安装到游戏」。
- 安装前会先自动备份当前 `Mods` 和配置，然后清空 `Mods`、解压整合包，可选覆盖配置；游戏运行时会拦下来。
- 版本或 Steam build 不一致会提示；只安装你信任的整合包，里面的 DLL 会随游戏加载。
- 目前只打包已启用模组，不包含存档。

**游戏版本与 Steam build**
- 管理器每 10 秒自动读一次 `MelonLoader/Latest.log` 头和 Steam `appmanifest`，变化就刷新界面。
- 概览会显示 `Steam build 26017150 · 更新于 2026-10-19 20:27`。游戏自己没更新 `Application.version` 时，版本号可能还是旧字符串，但 build 变了就能发现。
- 「刷新」按钮也会立即重读。

**界面风格**
- 终端：暗青金属 + HUD 网格 + 琥珀灯。
- 夜班：2.0–3.x 的暗青 + 琥珀，不显示 HUD。
- 石墨：1.x 的中性深灰 + 冷蓝。
- 在「设置 → 界面风格」切换；每套风格有默认主题色，也可以在下面单独改。

**背景与音乐**
- 不内置任何背景图或音频。点「选择图片」或「添加音乐」，选自己的文件；工具会复制到 `%LOCALAPPDATA%\DangpuZhanggui\backgrounds` 和 `music`，随时可以清除。
- 背景可以调压暗（0–90%）和虚化（0–30），默认压暗 78%、虚化 18，保证文字可读。
- 音乐支持 mp3 / wav / wma / m4a / aac，可以加多首、上一首/下一首、循环、音量、启动时自动播放；标题栏的喇叭按钮只在有音乐时出现。

**发现（N 网搜索）**
- 填了 Nexus API key 后，左边「发现」可以直接搜这个游戏的模组：封面、作者、版本、下载量、点赞和更新时间。
- 走官方 v2 GraphQL；支持相关度、下载、点赞、最近更新四种排序，20 个一页，可以「加载更多」。
- 点卡片打开 N 网页面。管理器不自动下载文件：API 的文件下载链接对免费用户不开放，装还是走网页。
- 没有 key 时页面会提示去设置；搜索结果是实时的，封面缩略图缓存到本地。

**Nexus 封面、简介与更新**
- 在「设置 → Nexus Mods」填一个免费的 API key（从 `nexusmods.com/settings/api-keys` 页面底部生成），详情页就会按模组链接自动读取 N 网主图、作者和完整简介；简介里的 BBCode/HTML 会转成纯文本，超过 1200 字可以「展开全文」。
- 没被本地资料表收录的模组，可以在详情里点「绑定 N 网页面」，粘一个 `nexusmods.com/.../mods/数字` 的地址进去；绑定记录在 `nexus-links.json`，以后都认。
- 工具栏的 ↑ 按钮会批量检查 N 网版本，列表和详情里显示「可更新 1.01」。
- 为什么要 key：N 网页面本身对脚本返回 403，只能走官方 API。key 只存在本机 `settings.json` 里，界面上是密码框。
- 结果缓存在 `%LOCALAPPDATA%\DangpuZhanggui\cache\nexus`，7 天内不重复请求；没 key 或请求失败时，退回本地资料表里的简介，并在详情里给出提示和「去设置填 Nexus key」。
- 「测试」按钮会拉一次鼠布林（mod 66）验证 key、作者和封面。

**体检**
用 PE 元数据读，不加载、不执行任何模组代码：
- 没有 `[assembly: MelonInfo]` → 红字。这种 dll 会被 MelonLoader 在预扫描阶段静默丢掉，日志里连报错都没有。
- `MelonGame` 不是 Probably Stolen → 提醒可能不兼容。
- 只有 `Assembly-CSharp`、没有 `Il2CppInterop` → 提醒像是 Mono 构建。
- 两个文件是同一个程序集、缺前置依赖、子文件夹缺 `manifest.json` → 都会列出来。

**诊断**
只读 `Latest.log` 和模组元数据，把可能的原因、证据和可疑模组摆出来：
- 加载失败、缺前置、同一个程序集启用两次、启用却没出现在日志里的模组。
- 按异常类型聚合（TypeLoad / MissingMethod / BadImage / 空引用等），带出现次数和最后一条原文。
- Harmony 补丁冲突痕迹、`native->managed trampoline` 刷屏、缺程序集。
- 可疑模组是按钮，点一下跳到模组页并选中。
- 「纯净启动」用 `--no-mods` 启动，先确认是不是模组把游戏搞崩的。

**日志**
读 `MelonLoader/Latest.log`，最新的一行在最上面；加载成功标绿、失败和异常标红、警告标黄。可以「只看问题」、按关键字搜索、把当前日志导出成文件。列表里会显示每个模组在日志里的真实状态：日志已加载 / 日志未见 / 加载失败。

**配置**
MelonPreferences 只是模组的配置存储，不是加载条件。没有配置项的模组照常加载、照常启停，只是不会出现在配置页里，这是正常的。

直接编辑 `UserData/MelonPreferences.cfg` 里的开关和数值，存之前自动备份成 `.bak-dangpu-时间戳`。内联表（`{ ... }`）只读。右上角的搜索框会在所有分组里按 key、注释和值找。

**方案**
把当前这套启停组合存下来，之后一键切回去。只记录哪些模组开着、哪些关着，不动 `Mods` 里的文件，也不改配置内容。
- 「应用」只调整方案里记录过的模组，后来新装的不会被牵连；「严格应用」会把方案里没记录的模组也停用，适合在几套干净配置之间切。
- 每套方案会显示和当前状态的差异：「与当前一致」「3 处不同」「2 个模组已不在」。
- 游戏运行时不允许切换，避免文件被占用。

**存档**
游戏存档在 `AppData\LocalLow\Questing Goose Studio\Probably Stolen`，工具只抓当前的 `SaveFile.es3`、`saves_index.es3` 和最近改动的那个 `save_N.es3`，不打包整个目录。
- 「拍一张快照」随时存；应用方案前可以自动拍一张（开关在页面上）。
- 还原前会先自动拍一张当前状态；保留最近 20 份快照。
- 只还原这三个文件，不动其他存档槽位。

**备份**
把 `Mods` 和两个 cfg 打成 zip，放在工具自己的数据目录里。还原是覆盖式的：同名文件被盖掉，备份里没有的模组不会被删；每次还原前会再自动备份一次当前状态。

**启动**
按「设置」里的参数启动游戏：隐藏控制台、调试模式、捕获 Unity 日志、启动时不加载模组，另加自定义参数。

## 一些边界

- 子文件夹模组必须有 `manifest.json`（MelonLoader 的规则），没有会当错误提示。
- 安装多 dll 的 zip：有 manifest 就装成子文件夹，没有就把 dll 平铺进 `Mods`，非 dll 文件不展开。
- 游戏运行时不能改 dll、也不能装加载器、还原存档（文件被占用），工具会直接拦下来。
- 只读写本机文件。联网只发生在：点「安装/修复加载器」时访问 GitHub，以及填了 Nexus key 后读取模组信息/封面。
- 它不加载 mod，也不替代 MelonLoader。没有加载器时，模组只是躺在文件夹里。

## 数据放在哪

`%LOCALAPPDATA%\DangpuZhanggui`

- `settings.json`：游戏路径、启动参数、主题色、Nexus key（目录名沿用 `DangpuZhanggui`，升级不丢设置）
- `backups\`：模组备份 zip
- `backups\saves\`：存档快照 zip
- `profiles\`：保存的方案（JSON，一套一个文件）
- `nexus-links.json`：手动绑定的 N 网页面
- `cache\nexus\`：N 网模组信息与封面缓存
- `backgrounds\`：玩家自选的背景图副本
- `music\`：玩家自选的音乐副本
- `modpacks\`：本地整合包库（导入/生成的 .dspmp）
- `loader\`：缓存的 MelonLoader 安装包
- `error.log`：崩了的话看这里

## 调试参数

```
深空当铺mod局外管理.exe --dump       out.json     把扫描结果吐成 JSON
深空当铺mod局外管理.exe --shot       shots\       把十一个页面各渲染一张 PNG
深空当铺mod局外管理.exe --selftest   out.txt      启停/安装/备份/配置/诊断/存档（临时沙盒）
深空当铺mod局外管理.exe --loadertest out.txt      导出加载器 → 装进临时目录 → 验证版本和启停
深空当铺mod局外管理.exe --profiletest out.txt     方案保存/应用/严格应用 + 列表导出
深空当铺mod局外管理.exe --importtest  out.txt     文件夹导入 / 拖游戏目录的防护
深空当铺mod局外管理.exe --dlgshot    dir\        渲染信息/确认/三选三种对话框
深空当铺mod局外管理.exe --nexustest  out.txt     用设置里的 key 拉一次 mod 66（不打印 key）
```

## 从源码构建

```bash
dotnet build -c Release
dotnet publish -c Release --self-contained false -p:PublishSingleFile=true -o dist
```

工程：`.NET 10 + WPF`，单文件输出约 580 KB（框架依赖）。没有第三方 NuGet 包。
