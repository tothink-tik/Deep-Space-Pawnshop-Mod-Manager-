# 把 source-release 推到 GitHub（给 N 网 support 看）

这一步你自己操作，命令都在下面。仓库要 **Public**，support 才能打开源码链接。

## 0. 先确认

- 装了 Git：`git --version`
- 有 GitHub 账号
- 新建仓库时选 **Public**，并且**不要**勾选 Add README / .gitignore / license（空仓库最省事）
- 当前目录就是 `source-release`，里面有 `BUILDING.md`、`.github/workflows/build.yml` 和 `DangpuZhanggui/`
- 不要把这个目录外面的 `dist`、`packaging`、`settings.json` 加进来；`.gitignore` 已经排除 `bin/obj/settings.json`

## 1. 第一次用 Git 的话，先设身份

```bash
git config --global user.name "totrymod"
git config --global user.email "你的邮箱"
```

## 2. 在 GitHub 网页新建空仓库

- Repository name：`deep-space-pawnshop-mod-manager`
- Visibility：**Public**
- 不要勾 Add a README file、Add .gitignore、Choose a license
- Create repository

## 3. 推送

在 Git Bash 或系统终端里：

```bash
cd "D:/WorkBuddy/2026-10-18-01-14-50/source-release"

git init
git add .
git status --short          # 看一眼，不应该出现 bin/、obj/、settings.json

git commit -m "Deep Space Pawnshop Mod Manager 1.0.0 Beta source"
git branch -M main
git remote add origin https://github.com/<你的GitHub账号>/deep-space-pawnshop-mod-manager.git
git push -u origin main
```

第一次 push 会弹浏览器登录，或者让你填 GitHub 用户名 + Personal Access Token。按提示走就行。

## 4. 想用 GitHub CLI 的话

```bash
gh auth login
cd "D:/WorkBuddy/2026-10-18-01-14-50/source-release"
gh repo create deep-space-pawnshop-mod-manager --public --source=. --remote=origin --push
```

## 5. 推完检查

- 用无痕窗口打开 `https://github.com/<你的账号>/deep-space-pawnshop-mod-manager`，确认不登录也能看。
- 点 `BUILDING.md`，确认构建命令在里面。
- 点 Actions，等一次绿色构建；artifact 里应该有 exe。
- 把仓库地址填进 `dist\nexus\APPEAL_EMAIL.txt` 的 `<GITHUB_REPO_URL>`。

## 6. 常见问题

**push 被拒绝，说 remote contains work you do not have**

说明建仓库时勾了 README。先拉一下再推：

```bash
git pull --rebase origin main
git push -u origin main
```

**没有 git 命令**

装 Git for Windows，或者用 GitHub Desktop：
Add local repository → 选 `source-release` → Publish repository → 取消 Private → Publish。

**不想公开仓库**

把 `dist\nexus\DeepSpacePawnshopModManager-1.0.0-Beta-Source.zip` 作为附件发给 support，并在邮件里写
`Source archive attached; I can publish a public repository if required.`
但对方明确要链接，公开仓库最省事。

## 7. 推完之后

1. 可选：把主 zip 传 VirusTotal，拿报告链接。
2. 填 `dist\nexus\APPEAL_EMAIL.txt` 的占位符。
3. 用 N 网账号邮箱发到 `support@nexusmods.com`。
4. 不要重复上传同一个文件。
