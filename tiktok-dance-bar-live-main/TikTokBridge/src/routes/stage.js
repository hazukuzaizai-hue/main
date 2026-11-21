const express = require("express");
const fs = require("node:fs/promises");
const path = require("node:path");
const crypto = require("node:crypto");
function stageRouter(folder) {
    const router = express.Router();
    const file = path.join(folder, "STAGE_SETTINGS.json");
    const defaults = {
        mode: "equalizer",
        video: "",
    };
    const valid = (name) =>
        typeof name === "string" &&
        name.length > 0 &&
        name.length <= 160 &&
        !/[<>:"/\\|?*\x00-\x1f]/.test(name) &&
        /\.mp4$/i.test(name);
    router.get("/", async (_req, res, next) => {
        try {
            await fs.mkdir(folder, { recursive: true });
            let settings = defaults;
            try {
                settings = {
                    ...defaults,
                    ...JSON.parse(await fs.readFile(file, "utf8")),
                };
            } catch (error) {
                if (error.code !== "ENOENT") throw error;
            }
            const videos = (await fs.readdir(folder, { withFileTypes: true }))
                .filter((entry) => entry.isFile() && valid(entry.name))
                .map((entry) => ({
                    name: entry.name,
                    url: "/stage-videos/" + encodeURIComponent(entry.name),
                }));
            let status = {
                state: "offline",
                message: "Game chưa cập nhật trạng thái",
            };
            try {
                const stat = await fs.stat(
                    path.join(folder, "STAGE_STATUS.json"),
                );
                if (Date.now() - stat.mtimeMs < 6000)
                    status = JSON.parse(
                        await fs.readFile(
                            path.join(folder, "STAGE_STATUS.json"),
                            "utf8",
                        ),
                    );
            } catch (error) {
                if (error.code !== "ENOENT") throw error;
            }
            res.json({
                mode: settings.mode,
                video: settings.video,
                videos,
                status,
            });
        } catch (error) {
            next(error);
        }
    });
    router.post(
        "/settings",
        express.json({ limit: "2kb" }),
        async (req, res, next) => {
            const data = req.body;
            if (!["equalizer", "video", "off"].includes(data?.mode))
                return res
                    .status(400)
                    .json({ error: "Cài đặt sân khấu không hợp lệ." });
            try {
                await fs.mkdir(folder, { recursive: true });
                if (data.mode === "video") {
                    if (!valid(data.video))
                        return res
                            .status(400)
                            .json({ error: "Hãy chọn một video MP4." });
                    const info = await fs
                        .stat(path.join(folder, data.video))
                        .catch(() => null);
                    if (!info?.isFile())
                        return res
                            .status(400)
                            .json({ error: "Video không tồn tại." });
                }
                const settings = {
                    mode: data.mode,
                    video: valid(data.video) ? data.video : "",
                };
                const temp = file + "." + crypto.randomUUID() + ".tmp";
                await fs.writeFile(temp, JSON.stringify(settings));
                await fs.rename(temp, file);
                res.json(settings);
            } catch (error) {
                next(error);
            }
        },
    );
    router.post(
        "/upload",
        express.raw({ type: "application/octet-stream", limit: "100mb" }),
        async (req, res, next) => {
            const name = req.query.name,
                body = req.body;
            if (
                !valid(name) ||
                !Buffer.isBuffer(body) ||
                body.length < 12 ||
                body.toString("ascii", 4, 8) !== "ftyp"
            )
                return res
                    .status(400)
                    .json({ error: "Chọn file MP4 hợp lệ, tối đa 100 MB." });
            try {
                await fs.mkdir(folder, { recursive: true });
                await fs.writeFile(path.join(folder, name), body, {
                    flag: "wx",
                });
                res.status(201).json({ name });
            } catch (error) {
                if (error.code === "EEXIST")
                    return res
                        .status(409)
                        .json({ error: "Video đã tồn tại. Hãy đổi tên file." });
                next(error);
            }
        },
    );
    router.use((error, _req, res, _next) => {
        console.error("Stage:", error.message);
        res.status(error.status || 500).json({
            error:
                error.status === 413
                    ? "Video tối đa 100 MB."
                    : "Không thể lưu cài đặt sân khấu.",
        });
    });
    return router;
}
module.exports = { stageRouter };
