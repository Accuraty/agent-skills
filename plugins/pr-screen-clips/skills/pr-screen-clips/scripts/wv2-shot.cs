#:package Microsoft.Web.WebView2@1.0.2903.40
#:property TargetFramework=net10.0-windows
#:property UseWindowsForms=true
#:property PublishAot=false
#:property MSBuildWarningsAsMessages=MSB3277

// wv2-shot 0.1.2: a screen clip of one part of a web page, for PR descriptions.
// Master copy: https://github.com/Accuraty/agent-skills (plugins/pr-screen-clips); projects may keep a copy, updated from there.
// Drives Edge WebView2 in an off-screen window and captures only the chosen element plus a margin
// (DevTools Page.captureScreenshot with a clip), so no full-page shot is taken.
// WINDOWS ONLY: written for Microsoft Edge WebView2, hosted in a Windows Forms window (net10.0-windows). Needs the .NET 10 SDK
// and the WebView2 Runtime (built into Windows 11 and current Windows 10); nothing else to install.
// Run it with no arguments for the usage line (dotnet run keeps --help for itself).
//
//   dotnet wv2-shot.cs <url> <css selector> <out.png> [options]
//     --margin N                  pixels around the element (default 16)
//     --hover SELECTOR            move the mouse over this element first (tooltips, hover states)
//     --click SELECTOR            click this element first (menus, dialogs)
//     --login PAGE INPUT VALUE    sign in first: open PAGE (a full URL, or a path on the same site), put VALUE in the
//                                 INPUT field, submit its form. From Git Bash pass a full URL: it rewrites "/path" arguments.
//     --dark                      prefer a dark color scheme (default light, whatever Windows uses); only for sites that
//                                 follow the system scheme (not apps whose theme is a per-user setting)
//     --size WxH                  viewport (default 1280x900)
//     --scale N                   device pixel ratio, such as 2 for sharper text (default 1)
//     --wait MS                   extra wait after loading and after hover or click (default 300)
//
// Sign-in cookies persist in %TEMP%\wv2-shot\<host-port>, so --login is needed once per site.
// Exit code 0 on success; errors go to stderr.
using System.Diagnostics;
using System.Text.Json;
using System.Windows.Forms;
using Microsoft.Web.WebView2.Core;

if (args.Length < 3 || args[0] is "-h" or "--help")
{
    Console.Error.WriteLine("usage: dotnet wv2-shot.cs <url> <css selector> <out.png> [--margin N] [--hover SEL] [--click SEL] [--login PAGE INPUT VALUE] [--dark] [--size WxH] [--scale N] [--wait MS]");
    return 2;
}
var url = new Uri(args[0]);
var (selector, output) = (args[1], Path.GetFullPath(args[2]));
string? Option(string name, int offset = 1) => Array.IndexOf(args, name) is var i and >= 0 && i + offset < args.Length ? args[i + offset] : null;
var margin = int.Parse(Option("--margin") ?? "16");
var (hover, click) = (Option("--hover"), Option("--click"));
var login = Option("--login") is { } loginPath ? (Path: loginPath, Input: Option("--login", 2)!, Value: Option("--login", 3)!) : default;
var dark = args.Contains("--dark");
var size = (Option("--size") ?? "1280x900").Split('x').Select(int.Parse).ToArray();
var scale = double.Parse(Option("--scale") ?? "1", System.Globalization.CultureInfo.InvariantCulture);
var wait = int.Parse(Option("--wait") ?? "300");
var clock = Stopwatch.StartNew();
var exitCode = 0;

var ui = new Thread(() =>
{
    // WebView2 needs a window; this one is off-screen, with no taskbar entry.
    using var form = new Form { ShowInTaskbar = false, StartPosition = FormStartPosition.Manual, Location = new(-30000, -30000), FormBorderStyle = FormBorderStyle.None, ClientSize = new(size[0], size[1]) };
    var view = new Microsoft.Web.WebView2.WinForms.WebView2 { Dock = DockStyle.Fill };
    form.Controls.Add(view);
    form.Shown += async (_, _) =>
    {
        try
        {
            var profile = Path.Combine(Path.GetTempPath(), "wv2-shot", $"{url.Host}-{url.Port}");
            var env = await CoreWebView2Environment.CreateAsync(null, profile, new CoreWebView2EnvironmentOptions("--ignore-certificate-errors"));
            await view.EnsureCoreWebView2Async(env);
            var web = view.CoreWebView2;
            web.Profile.PreferredColorScheme = dark ? CoreWebView2PreferredColorScheme.Dark : CoreWebView2PreferredColorScheme.Light;
            if (scale != 1)
                await web.CallDevToolsProtocolMethodAsync("Emulation.setDeviceMetricsOverride",
                    JsonSerializer.Serialize(new { width = size[0], height = size[1], deviceScaleFactor = scale, mobile = false }));

            // Runs an action and waits for the navigation it starts (or a plain Navigate).
            async Task Navigated(Func<Task> start)
            {
                var done = new TaskCompletionSource<bool>();
                void Completed(object? _, CoreWebView2NavigationCompletedEventArgs e) => done.TrySetResult(e.IsSuccess);
                web.NavigationCompleted += Completed;
                try
                {
                    await start();
                    if (await Task.WhenAny(done.Task, Task.Delay(30_000)) != done.Task) throw new TimeoutException("the page didn't finish loading in 30 seconds");
                }
                finally { web.NavigationCompleted -= Completed; }
            }
            // The element's box in viewport coordinates (for the mouse), then the page scroll (the clip is in page coordinates).
            async Task<double[]?> Rect(string css) => JsonSerializer.Deserialize<double[]?>(await web.ExecuteScriptAsync(
                $"(() => {{ const e = document.querySelector({JsonSerializer.Serialize(css)}); if (!e) return null; e.scrollIntoView({{ block: 'nearest' }}); const r = e.getBoundingClientRect(); return [r.x, r.y, r.width, r.height, window.scrollX, window.scrollY]; }})()"));

            if (login.Path is not null)
            {
                await Navigated(() => { web.Navigate(new Uri(url, login.Path).ToString()); return Task.CompletedTask; });
                var signInPath = new Uri(web.Source).AbsolutePath;
                await web.ExecuteScriptAsync(
                    $"(() => {{ const i = document.querySelector({JsonSerializer.Serialize(login.Input)}); i.value = {JsonSerializer.Serialize(login.Value)}; i.form.requestSubmit(); }})()");
                // The form may post normally or through script, so wait for the address to leave the sign-in page's path;
                // a rejected value usually comes back to the same path with a different query string.
                for (var tries = 0; new Uri(web.Source).AbsolutePath.Equals(signInPath, StringComparison.OrdinalIgnoreCase); tries++)
                {
                    if (tries == 150) throw new TimeoutException($"still on {web.Source} after signing in: is the value right (and unused, for a one-time code)?");
                    await Task.Delay(100);
                }
                await Task.Delay(wait);
                Console.WriteLine($"signed in: now at {web.Source}");
            }
            await Navigated(() => { web.Navigate(url.ToString()); return Task.CompletedTask; });
            await Task.Delay(wait);

            foreach (var (target, kind) in new[] { (click, "mousePressed"), (hover, "mouseMoved") })
            {
                if (target is null) continue;
                var r = await Rect(target) ?? throw new InvalidOperationException($"{target}: not on the page");
                var (x, y) = (r[0] + r[2] / 2, r[1] + r[3] / 2);
                await web.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", JsonSerializer.Serialize(new { type = "mouseMoved", x, y }));
                if (kind == "mousePressed")
                    foreach (var type in new[] { "mousePressed", "mouseReleased" })
                        await web.CallDevToolsProtocolMethodAsync("Input.dispatchMouseEvent", JsonSerializer.Serialize(new { type, x, y, button = "left", clickCount = 1 }));
                await Task.Delay(wait);
            }

            var area = await Rect(selector) ?? throw new InvalidOperationException($"{selector}: not on {web.Source} (signed out, or a different page?)");
            var clip = new
            {
                x = Math.Max(0, area[0] + area[4] - margin), y = Math.Max(0, area[1] + area[5] - margin),
                width = Math.Min(area[2] + 2 * margin, size[0]), height = area[3] + 2 * margin, scale = 1,
            };
            var shot = await web.CallDevToolsProtocolMethodAsync("Page.captureScreenshot",
                JsonSerializer.Serialize(new { format = "png", clip, captureBeyondViewport = true }));
            using var json = JsonDocument.Parse(shot);
            Directory.CreateDirectory(Path.GetDirectoryName(output)!);
            await File.WriteAllBytesAsync(output, Convert.FromBase64String(json.RootElement.GetProperty("data").GetString()!));
            Console.WriteLine($"{output}: {clip.width * scale:0}x{clip.height * scale:0} in {clock.Elapsed.TotalSeconds:0.0}s");
        }
        catch (Exception ex) { Console.Error.WriteLine($"wv2-shot: {ex.Message}"); exitCode = 1; }
        finally { form.Close(); }
    };
    Application.Run(form);
});
ui.SetApartmentState(ApartmentState.STA);
ui.Start();
ui.Join();
return exitCode;
