const { execFile } = require("node:child_process");
const fs = require("node:fs/promises");
const path = require("node:path");
const { promisify } = require("node:util");
const run = promisify(execFile);
let opening = false;
async function openGame(root) {
    if (process.platform !== "win32")
        throw new Error("Chức năng này chỉ hỗ trợ Windows.");
    if (opening) return { message: "Game đang được mở…" };
    opening = true;
    try {
        const exe = path.join(root, "Build", "TIKTOK_LIVE_BAR.exe");
        await fs.access(exe);
        const script = `$ErrorActionPreference = 'Stop'; $exe = $env:TIKTOK_GAME_EXE; $running = @(Get-Process -Name TIKTOK_LIVE_BAR -ErrorAction SilentlyContinue | Where-Object { $_.Path -eq $exe }); $window = @($running | Where-Object { $_.MainWindowHandle -ne 0 }); if ($window.Count -gt 0) { Write-Output 'RUNNING'; exit }; foreach ($process in $running) { Stop-Process -Id $process.Id -Force }; $game = Start-Process -FilePath $exe -WorkingDirectory ([System.IO.Path]::GetDirectoryName($exe)) -PassThru; for ($attempt = 0; $attempt -lt 20; $attempt++) { Start-Sleep -Milliseconds 400; $game.Refresh(); if ($game.HasExited) { throw 'Game exited before opening a window' }; if ($game.MainWindowHandle -ne 0) { Write-Output 'OPENED'; exit } }; throw 'Game did not create a window'`;
        const { stdout } = await run(
            "powershell.exe",
            ["-NoProfile", "-NonInteractive", "-Command", script],
            {
                windowsHide: true,
                timeout: 15000,
                env: { ...process.env, TIKTOK_GAME_EXE: exe },
            },
        );
        return {
            message: stdout.includes("RUNNING")
                ? "Game đang chạy. Chọn cửa sổ TikTokLiveGameUnity trên thanh tác vụ."
                : "Đã mở cửa sổ game TikTokLiveGameUnity.",
        };
    } finally {
        opening = false;
    }
}
module.exports = { openGame };
