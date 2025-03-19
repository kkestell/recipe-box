using System.Diagnostics;

namespace RecipeBox.Core;

public static class Browser
{
    public static void Launch(int port)
    {
        var browserPaths = new Dictionary<string, string[]>
        {
            ["Windows"] =
            [
                @"C:\Program Files\Google\Chrome\Application\chrome.exe",
                Path.Combine(Environment.GetEnvironmentVariable("ProgramFiles(x86)") ?? string.Empty,
                    @"Google\Chrome\Application\chrome.exe"),
                Path.Combine(Environment.GetEnvironmentVariable("LocalAppData") ?? string.Empty,
                    @"Google\Chrome\Application\chrome.exe"),
                @"C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe"
            ],
            ["Darwin"] =
            [
                "/Applications/Google Chrome.app/Contents/MacOS/Google Chrome",
                "/Applications/Chromium.app/Contents/MacOS/Chromium",
                "/Applications/Microsoft Edge.app/Contents/MacOS/Microsoft Edge"
            ],
            ["Linux"] =
            [
                "/usr/bin/chromium",
                "/usr/bin/google-chrome",
                "/usr/bin/google-chrome-stable",
                "/usr/bin/chromium-browser",
                "/snap/bin/chromium",
                "/snap/bin/google-chrome"
            ]
        };

        var system = Environment.OSVersion.Platform switch
        {
            PlatformID.Win32NT => "Windows",
            PlatformID.Unix => File.Exists("/System/Library/CoreServices/SystemVersion.plist") ? "Darwin" : "Linux",
            _ => "Unknown"
        };

        if (!browserPaths.TryGetValue(system, out var availablePaths)) 
            availablePaths = [];

        var browserPath = availablePaths.FirstOrDefault(File.Exists);

        if (browserPath == null) 
            return;
        
        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = browserPath,
                Arguments = $"--app=http://localhost:{port} " +
                            "--no-default-browser-check " +
                            "--no-first-run " +
                            "--disable-infobars " +
                            "--disable-translate " +
                            "--disable-notifications",
                UseShellExecute = true
            }
        };
        
        process.Start();
    }
}