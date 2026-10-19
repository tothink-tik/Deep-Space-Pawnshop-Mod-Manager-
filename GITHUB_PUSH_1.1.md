# 把 1.1 源码推到单独分支（不要动 main）

1.0 还在 N 网审核，main 必须保持 1.0 的源码。1.1 用单独分支，support 可以分别看。

```bash
cd source-release

git status
git checkout -b 1.1-beta
git add .
git commit -m "1.1 Beta: modpacks and Steam build detection"
git push -u origin 1.1-beta
```

推完把分支地址填进 `dist\nexus\APPEAL_UPDATE_1.1.txt` 的 `<SOURCE_BRANCH_URL>`：

```
https://github.com/tothink-tik/Deep-Space-Pawnshop-Mod-Manager-/tree/1.1-beta
```

**不要**把 1.1 直接推到 main，也不要 force push main。等 1.0 审核结束再决定合并。
