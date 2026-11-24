# Accuraty agent skills

Agent skills and small tools Accuraty reuses across projects, packaged as a Claude Code plugin marketplace.

## Install (Claude Code)

```
/plugin marketplace add Accuraty/agent-skills
/plugin install pr-screen-clips@accuraty-agent-skills
```

Update later with `/plugin marketplace update accuraty-agent-skills`.

## Plugins

| Plugin | What it does |
| --- | --- |
| [pr-screen-clips](plugins/pr-screen-clips/skills/pr-screen-clips/SKILL.md) | **Windows only.** Before and after screen clips of just the changed part of a web page, taken with `wv2-shot` (a .NET 10 file-based app driving Edge WebView2) and uploaded into the PR body with `gh pr edit --attach`. |

### pr-screen-clips requirements

`wv2-shot.cs` is written for **Microsoft Edge WebView2**, so it **runs only on Windows**: it targets `net10.0-windows` and opens a Windows Forms window (off-screen) to host the browser.

- Windows 10 or 11 with the WebView2 Runtime (built into Windows 11 and current Windows 10).
- The .NET 10 SDK (`dotnet wv2-shot.cs` runs it as a file-based app).
- GitHub CLI 2.99+ for `gh pr edit --attach`.

On macOS or Linux the skill's steps still apply, but the clips need another tool with an element or clip screenshot, such as Playwright or shot-scraper.

## Using a tool without the plugin

Each skill's `scripts/` folder holds its tools. A project may keep a copy (for example `tools/screenshots/wv2-shot.cs`) so that every agent and person working on it has the same version; this repo is the master copy, so improve the tool here and copy it back into projects.

## Adding a skill

1. Add `plugins/<name>/.claude-plugin/plugin.json` and `plugins/<name>/skills/<name>/SKILL.md` (plus any `scripts/`).
2. List the plugin in `.claude-plugin/marketplace.json`.
3. Keep it project-neutral: project-specific details (ports, sign-in, data) belong in each project's own docs, which the skill tells the agent to read.
4. Bump the plugin's `version` in both JSON files when its behavior changes.
