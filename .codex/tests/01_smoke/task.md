# 01_smoke — 链路冒烟

## 目的
验证完整链路: codex CLI → OpenCode Go 直连（/v1/responses，无本地代理）。

## 指令
```
Reply with exactly: LINK_OK
```

## 预期
输出为 `LINK_OK`，无 404/403/401 错误。
