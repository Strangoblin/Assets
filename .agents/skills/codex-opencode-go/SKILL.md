---
name: codex-opencode-go
description: Codex CLI/VSCode 接入 OpenCode Go 的完整配置与排障（直连方案，2026-08-17 起无需本地代理）。涉及 Codex config.toml、auth.json、模型可用性与区域限制。在任何电脑上从零搭好 Codex → OpenCode Go 链路，或排查 "Codex could not start"、stream disconnected、403 RegionError 等错误时使用。
---

# Codex ↔ OpenCode Go 接入手册（直连方案）

> 2026-08-17 起 OpenCode Go **原生支持 `/v1/responses` 端点**，Codex 可直连，不再需要 opencode-go-proxy 协议翻译。配置产物：全局 `~/.codex/config.toml` + `~/.codex/auth.json`；项目级隔离用 `CODEX_HOME`（示例：`Robot/Bot/.codex/`）。

## 架构（直连）

```
Codex (CLI/VSCode) ──responses──▶ https://opencode.ai/zen/go/v1
```

- `wire_api = "responses"`（Codex 26.803+ 唯一支持，2026-02 起 `chat`/`chat/completions` 已移除）
- OpenCode Go 直连即原生 Responses API，**无代理、无协议翻译、无"无限空白"bug**

## 两件套配置（缺一不可）

| 文件 | 位置 | 作用 |
|------|------|------|
| `config.toml` | `~/.codex/config.toml` | 模型 + provider 定义 |
| `auth.json` | `~/.codex/auth.json` | OpenAI 格式 key（`{"OPENAI_API_KEY": "sk-..."}`, chmod 600） |

```toml
# ~/.codex/config.toml — 直连模板
model = "deepseek-v4-flash"
model_provider = "opencode-go"
model_reasoning_effort = "high"
disable_response_storage = true

[model_providers.opencode-go]
name = "OpenCode Go"
base_url = "https://opencode.ai/zen/go/v1"   # ⚠️ 必须带 /v1
wire_api = "responses"
stream_idle_timeout_ms = 300000
request_max_retries = 2
stream_max_retries = 2
```

## 安装步骤（新电脑）

1. **安装 Codex CLI**：`npm install -g @openai/codex`（或 VSCode 装 `openai.chatgpt` 扩展）
2. **写配置**：创建 `~/.codex/config.toml`（用上面模板）+ `~/.codex/auth.json`（填有效 key）
3. **验证**：`codex exec "Reply with exactly: OK"` → 应输出 `OK`

## 模型可用性（2026-08 实测）

| 模型 | 状态 |
|------|------|
| `deepseek-v4-flash` | ✅ 默认（需在 opencode 工作台开启「提供商 → 启用部署在中国的模型」，2026-08-11 实测通过） |
| `deepseek-v4-pro` | ✅ 无区域限制（备选） |
| `mimo-v2.5` | ✅ 可用 |
| `gpt-5.6-luna` / `glm-5.2` / `qwen3.8-max` / `kimi-k3` / `minimax-m3` | ✅ 可用 |

切模型：改 `config.toml` 的 `model = "xxx"`。

## 项目级隔离（CODEX_HOME）

多项目用不同 key/模型时，建项目级 `.codex/` 目录：

```bash
export CODEX_HOME=/path/to/project/.codex
codex exec "..."   # 读项目级 config.toml + auth.json
```

示例：`Robot/Bot/.codex/`（独立 key + 信任目录）。

## 排障速查表

| 症状 | 根因 | 修复 |
|------|------|------|
| 扩展报 `unknown variant chat/completions, expected responses` | Codex 26.803 不支持 chat/completions | `wire_api = "responses"` |
| 上游 401 Invalid API key | auth.json key 失效 | 更新 auth.json，重新去 opencode.ai 生成 |
| 上游 403 `only available hosted in China` | 模型需中国区 opt-in | 去 opencode 工作台开启「提供商 → 启用部署在中国的模型」；不开则换 deepseek-v4-pro 等无限制模型 |
| 404 Not Found（HTML 页） | `base_url` 漏 `/v1` | 必须 `https://opencode.ai/zen/go/v1` |
| `Model metadata for X not found` warning | Codex 无该模型元数据，用 fallback | 无害，可忽略；或在 config 显式声明 `model_context_window` 等字段 |
| `Ignored unsupported project-local config keys: model_provider, model_providers` | **项目级** `.codex/config.toml` 不支持 provider 类 key（Codex 只认 user-level `~/.codex/config.toml`） | 模型/provider 只写在全局 `~/.codex/config.toml`；项目级 config 只放 trust_level 等 key |
| VSCode 面板模型选择器只有默认 gpt 模型（看不到 opencode-go 的 8 个） | `model_catalog_json` 未配置 / catalog 文件缺失（伴随 CLI 侧 `Model metadata not found` warning） | 重建 `~/.codex/model-catalogs/opencode-go.json`（每模型必含 `visibility:"list"`、`supported_reasoning_levels`、`truncation_policy` 等完整字段）+ config.toml 顶层加 `model_catalog_json = "绝对路径"` + **Reload Window**。catalog 是面板模型列表的唯一来源，与代理无关，直连也必须保留 |
| VSCode 面板模型选择器只有默认 gpt 模型（看不到 opencode-go 的 8 个） | `model_catalog_json` 未配置 / catalog 文件缺失（伴随 CLI 侧 `Model metadata not found` warning） | 重建 `~/.codex/model-catalogs/opencode-go.json`（每模型必含 `visibility:"list"`、`supported_reasoning_levels`、`truncation_policy` 等完整字段）+ config.toml 顶层加 `model_catalog_json = "绝对路径"` + **Reload Window**。catalog 是面板模型列表的唯一来源，与代理无关，直连也必须保留 |
| `Codex could not start` / webview 卡住 | 扩展启动需连 chatgpt.com / github.com，网络不通 | 检查外网连通性；重载重试 |
| VSCode 改了 config.toml 不生效 | 扩展用内存缓存 | Reload Window |
| 模型身份幻觉（自称 GPT） | 模型训练数据问题，正常现象 | 忽略，以 config.toml 的 model 为准 |

## 关键验证命令

```bash
# 1. CLI 直连
codex exec "Reply with exactly: OK"

# 2. 直测 OpenCode Go responses 端点（验 key/模型）
curl -s -X POST "https://opencode.ai/zen/go/v1/responses" \
  -H "Authorization: Bearer $OPENAI_API_KEY" -H "Content-Type: application/json" \
  -d '{"model":"deepseek-v4-flash","input":"hi","stream":false}'

# 3. 模型列表
curl -s "https://opencode.ai/zen/go/v1/models" -H "Authorization: Bearer $OPENAI_API_KEY"
```

## 历史方案（仅供参考，不再需要）

**opencode-go-proxy 协议翻译**（8-10 版方案，OpenCode Go 支持 responses 后废弃）：Codex 当时只发 `/v1/responses` 而 OpenCode Go 只有 `/v1/chat/completions`，需本地代理翻译。相关坑已随直连消失：`--chat-base-url` 漏 `/v1` 的 404、模型不在代理白名单 fallback 到 flash 的 403、CC-Switch 代理的"无限空白"流掐断。

## 与 CC-Switch 的关系

- **项目 Agent → OpenCode Go**：仍走 CC-Switch 本地代理（15721），Claude 走 Anthropic Messages 格式，需要 CC-Switch 做翻译
- **Codex → OpenCode Go**：**直连**（responses 原生格式），与 CC-Switch 完全无关
