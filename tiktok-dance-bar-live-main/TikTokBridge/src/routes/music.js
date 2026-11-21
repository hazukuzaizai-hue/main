const express = require("express");
const fs = require("node:fs/promises");
const path = require("node:path");
const crypto = require("node:crypto");

function musicRouter(folder) {
    const router = express.Router();
    const settingsFile = path.join(folder, "MUSIC_SETTINGS.json");
    const extensions = new Set([".mp3", ".wav", ".ogg"]);
    router.get("/", async (_req, res, next) => {
        try {
            await fs.mkdir(folder, { recursive: true });
            let settings = { enabled: true, volume: 0.35 };
            try {
                settings = {
                    ...settings,
                    ...JSON.parse(await fs.readFile(settingsFile, "utf8")),
                };
            } catch (error) {
                if (error.code !== "ENOENT") throw error;
            }
            const tracks = [];
            for (const entry of await fs.readdir(folder, {
                withFileTypes: true,
            })) {
                if (
                    !entry.isFile() ||
                    !extensions.has(path.extname(entry.name).toLowerCase())
                )
                    continue;
                const info = await fs.stat(path.join(folder, entry.name));
                tracks.push({
                    name: entry.name,
                    size: info.size,
                    url: "/music-files/" + encodeURIComponent(entry.name),
                });
            }
            tracks.sort((a, b) => a.name.localeCompare(b.name));
            res.json({ ...settings, tracks });
        } catch (error) {
            next(error);
        }
    });
    router.post(
        "/settings",
        express.json({ limit: "2kb" }),
        async (req, res, next) => {
            if (
                typeof req.body?.enabled !== "boolean" ||
                typeof req.body?.volume !== "number" ||
                !Number.isFinite(req.body.volume) ||
                req.body.volume < 0 ||
                req.body.volume > 1
            )
                return res
                    .status(400)
                    .json({ error: "Cài đặt âm lượng không hợp lệ." });
            try {
                await fs.mkdir(folder, { recursive: true });
                const settings = {
                    enabled: req.body.enabled,
                    volume: req.body.volume,
                };
                const temp = settingsFile + "." + crypto.randomUUID() + ".tmp";
                await fs.writeFile(temp, JSON.stringify(settings));
                await fs.rename(temp, settingsFile);
                res.json(settings);
            } catch (error) {
                next(error);
            }
        },
    );
    router.get("/status", async (_req, res, next) => {
        try {
            const file = path.join(folder, "MUSIC_STATUS.json");
            const stat = await fs.stat(file);
            if (Date.now() - stat.mtimeMs > 6000)
                return res.json({ state: "offline", track: "" });
            res.json(JSON.parse(await fs.readFile(file, "utf8")));
        } catch (error) {
            if (error.code === "ENOENT")
                return res.json({ state: "offline", track: "" });
            next(error);
        }
    });
    router.post(
        "/command",
        express.json({ limit: "2kb" }),
        async (req, res, next) => {
            const action = req.body?.action;
            if (!["play", "pause", "resume", "next"].includes(action))
                return res
                    .status(400)
                    .json({ error: "Lệnh nhạc không hợp lệ." });
            const name = String(req.body?.track || "");
            try {
                if (action === "play") {
                    if (
                        path.basename(name) !== name ||
                        /[\\/]/.test(name) ||
                        !extensions.has(path.extname(name).toLowerCase())
                    )
                        return res
                            .status(400)
                            .json({ error: "Bài hát không hợp lệ." });
                    const info = await fs
                        .stat(path.join(folder, name))
                        .catch(() => null);
                    if (!info?.isFile())
                        return res
                            .status(404)
                            .json({ error: "Không tìm thấy bài hát." });
                }
                await fs.mkdir(folder, { recursive: true });
                if (action !== "pause") {
                    let settings = { enabled: true, volume: 0.35 };
                    try {
                        settings = {
                            ...settings,
                            ...JSON.parse(
                                await fs.readFile(settingsFile, "utf8"),
                            ),
                        };
                    } catch (error) {
                        if (error.code !== "ENOENT") throw error;
                    }
                    settings.enabled = true;
                    const settingsTemp =
                        settingsFile + "." + crypto.randomUUID() + ".tmp";
                    await fs.writeFile(settingsTemp, JSON.stringify(settings));
                    await fs.rename(settingsTemp, settingsFile);
                }
                const command = {
                    id: crypto.randomUUID(),
                    action,
                    track: name,
                };
                const file = path.join(folder, "MUSIC_COMMAND.json");
                const temp = file + "." + command.id + ".tmp";
                await fs.writeFile(temp, JSON.stringify(command));
                await fs.rename(temp, file);
                res.json({ ok: true });
            } catch (error) {
                next(error);
            }
        },
    );
    router.post(
        "/upload",
        express.raw({ type: "application/octet-stream", limit: "50mb" }),
        async (req, res, next) => {
            const name = String(req.query.name || "");
            if (
                !name ||
                name.length > 160 ||
                /[<>:"/\\|?*\x00-\x1f]/.test(name) ||
                name.endsWith(".") ||
                name.endsWith(" ") ||
                !extensions.has(path.extname(name).toLowerCase()) ||
                !Buffer.isBuffer(req.body) ||
                !req.body.length
            )
                return res
                    .status(400)
                    .json({ error: "Chọn file MP3, WAV hoặc OGG hợp lệ." });
            try {
                await fs.mkdir(folder, { recursive: true });
                // Exclusive creation preserves any existing track with the same name.
                await fs.writeFile(path.join(folder, name), req.body, {
                    flag: "wx",
                });
                res.status(201).json({ name });
            } catch (error) {
                if (error.code === "EEXIST")
                    return res.status(409).json({
                        error: "Tên bài hát đã tồn tại. Hãy đổi tên file rồi tải lại.",
                    });
                next(error);
            }
        },
    );
    router.use((error, _req, res, _next) => {
        console.error("Music:", error.message);
        res.status(error.status || 500).json({
            error:
                error.status === 413
                    ? "Mỗi file tối đa 50 MB."
                    : "Không thể xử lý âm nhạc. Vui lòng thử lại.",
        });
    });
    return router;
}
module.exports = { musicRouter };
