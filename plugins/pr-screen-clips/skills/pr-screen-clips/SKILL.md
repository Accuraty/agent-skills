---
name: pr-screen-clips
description: Screen clips for pull requests: before and after images of just the changed part of a web page, uploaded into the PR body with gh --attach. Use when a PR or issue needs screenshots of a UI change.
---

# PR screen clips

A **clip** is an image of one element of a page (a heading row, a card, a form, a tooltip) plus a small margin, never the whole page unless the page layout itself changed. Clips are taken with `wv2-shot` and uploaded with `gh pr edit --attach`, so no image is ever committed to git.

`wv2-shot` is a .NET 10 file-based app: `scripts/wv2-shot.cs` in this skill's folder. If the project keeps its own copy (such as `tools/screenshots/wv2-shot.cs`), use the project's copy.

**Windows only:** `wv2-shot` is written for Microsoft Edge WebView2 and hosts it in a Windows Forms window (`net10.0-windows`). It needs Windows with the WebView2 Runtime, the .NET 10 SDK, and GitHub CLI 2.99+. On macOS or Linux, follow the same steps but take the clips with another tool that captures one element (Playwright's element screenshot, or shot-scraper `-s`).

## Steps

1. **Project setup.** Find the project's own notes on screen clips (its AGENTS.md, or a doc it points to): how to run the app locally on a spare port, and how to sign in locally. Done when you know the base URL and the sign-in page, input and value. If the project has no such notes, ask the user, then offer to add them to the project's docs.
2. **Plan the clips.** For each visible change, pick the page URL, a CSS selector that frames the change with a little context, and any hover or click needed to show it. Done when every visible change in the PR has a planned before and after pair. A change with no visible effect gets no clip; say so in the PR.
3. **After clips.** Copy `wv2-shot.cs` into a scratch folder outside the repo and run that copy for both sets, since the base commit may not have the tool. Build and run the PR branch, then take each clip into a scratch folder outside the repo, named `<thing>-after.png`. Sign in on the first one with `--login`; the cookie is kept per host and port for later runs.
4. **Before clips.** Stop the app, check out the PR's base in the same checkout (`git checkout --detach origin/<base>`, with `<base>` from `gh pr view <n> --json baseRefName`), build, run it on the same port, and take the same clips with the same selectors, named `<thing>-before.png`. Same checkout and port keep the sign-in cookie working. Then return to the PR branch and rebuild.
5. **Look at every image.** Done when each one shows the intended state: not the sign-in page, no stray focus ring or open popover, tooltips fully visible, before and after framed alike.
6. **Upload.** Write the PR body in a file in the scratch folder, with a Before | After table referring to the images by relative path, then run from the repo:

   ```
   gh pr edit <n> --body-file <folder>/body.md --attach <folder>/a-before.png --attach <folder>/a-after.png
   ```

   `gh` uploads each image to GitHub's attachment storage and rewrites the reference to its `user-attachments` URL. Done when `gh pr view <n> --json body` shows `https://github.com/user-attachments/assets/...` for every image and no `./` paths remain.
7. **Clean up.** Stop the app, delete any test data you added, and leave the checkout on the PR branch with a clean `git status`.

## wv2-shot

```
dotnet <path>/wv2-shot.cs <url> <css selector> <out.png> [--margin N] [--hover SEL] [--click SEL]
    [--login PAGE INPUT VALUE] [--dark] [--size WxH] [--scale N] [--wait MS]
```

- Clips the first element matching the selector plus `--margin` (default 16). `--scale 2` gives sharper text. Default viewport 1280x900, light color scheme.
- `--login` opens PAGE, puts VALUE in the INPUT field, submits its form, and waits to leave that page. Built for one-field sign-ins such as a one-time code.
- On a missing element it exits with 1 and names the page it was on; usually that's the sign-in page (cookie expired, or a new checkout with different keys).
- Run it with no arguments for the usage line; `dotnet run` keeps `--help` for itself. Each option is explained at the top of the file.

## Pitfalls

- Git Bash rewrites arguments starting with `/` into Windows paths: pass full URLs, or use PowerShell.
- A repo-level `Directory.Build.props` also applies to a copy of the tool kept inside a project; if it turns on lock files, the copy writes `packages.lock.json` beside itself, so give it its own folder and commit that file.
- `--dark` only affects sites that follow the system color scheme, not apps whose theme is a per-user setting.
- `gh` must run inside the repo (or with `-R owner/repo`); relative image paths resolve from the body file's folder. Without `--body-file`, `gh` appends the images to the existing body.
- Attached images in a private repo are private too: signed-out viewers get a 404.
- `gh issue edit --attach` does the same for an issue's evidence.
