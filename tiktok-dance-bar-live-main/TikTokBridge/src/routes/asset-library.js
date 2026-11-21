const express = require("express");
const fs = require("node:fs/promises");
const path = require("node:path");
const crypto = require("node:crypto");
const { execFile } = require("node:child_process");
const { promisify } = require("node:util");
const run = promisify(execFile);
function assetRouter(root, ffmpeg) {
    const router = express.Router();
    const folder = path.join(root, "Data", "Assets");
    const manifest = path.join(folder, "library.json");
    const defaults = { items: [], wing: "", banner: "", nextDanceNumber: 1 };
    const categories = {
        character: "characters",
        wing: "wings",
        banner: "banners",
        avatarFrame: "avatar-frames",
    };
    const numbering = {
        character: ["danceNumber", "nextDanceNumber"],
        wing: ["displayNumber", "nextWingNumber"],
        banner: ["displayNumber", "nextBannerNumber"],
    };
    let queue = Promise.resolve();
    async function builtins() {
        const resources = path.join(
            root,
            "UnityProject",
            "Assets",
            "Resources",
        );
        const items = [];
        const names = (
            await fs.readdir(path.join(resources, "Characters"), {
                withFileTypes: true,
            })
        )
            .filter((entry) => entry.isDirectory())
            .map((entry) => entry.name);
        for (const name of names) {
            const dir = path.join(resources, "Characters", name);
            const frames = await fs.readdir(dir).catch(() => []);
            const png = frames.filter((file) => file.endsWith(".png")).sort();
            if (png.length)
                items.push({
                    id: "builtin:character:" + name,
                    name: "Nhân vật gốc · " + name,
                    kind: "character",
                    enabled: true,
                    fps: 12,
                    frames: png.length,
                    preview:
                        "/builtin-assets/Characters/" + name + "/" + png[0],
                    source: "/assets/gifs/" + name + ".gif",
                });
        }
        for (const name of (
            await fs.readdir(path.join(resources, "Banners")).catch(() => [])
        ).filter((name) => name.endsWith(".png"))) {
            const kind =
                name.startsWith("title-") || name.includes("nameplate")
                    ? "banner"
                    : "wing";
            items.push({
                id: "builtin:" + kind + ":" + path.basename(name, ".png"),
                name,
                kind,
                enabled: true,
                fps: 12,
                frames: 1,
                preview: "/builtin-assets/Banners/" + name,
                source: "/builtin-assets/Banners/" + name,
            });
        }
        return items;
    }
    async function read() {
        try {
            return {
                ...defaults,
                ...JSON.parse(await fs.readFile(manifest, "utf8")),
            };
        } catch (e) {
            if (e.code !== "ENOENT") throw e;
            return { ...defaults, items: await builtins() };
        }
    }
    function mutate(fn) {
        const job = queue.then(async () => {
            await fs.mkdir(folder, { recursive: true });
            const data = await read();
            for (const [kind, [numberKey, counterKey]] of Object.entries(
                numbering,
            )) {
                const items = data.items.filter((item) => item.kind === kind);
                let next = data[counterKey] || 1;
                for (const item of items)
                    next = Math.max(next, (item[numberKey] || 0) + 1);
                for (const item of items) {
                    if (!item.deleted && !item[numberKey])
                        item[numberKey] = next++;
                }
                data[counterKey] = next;
            }
            const result = await fn(data);
            const temp = manifest + ".tmp";
            await fs.writeFile(temp, JSON.stringify(data));
            await fs.rename(temp, manifest);
            return result;
        });
        queue = job.catch(() => {});
        return job;
    }
    router.get("/", async (_req, res, next) => {
        try {
            await mutate(() => {});
            const data = await read();
            res.json({
                ...data,
                items: data.items.filter((item) => !item.deleted),
            });
        } catch (e) {
            next(e);
        }
    });
    router.post(
        "/upload",
        express.raw({ type: "application/octet-stream", limit: "20mb" }),
        async (req, res, next) => {
            const name = String(req.query.name || "");
            const kind = req.query.kind;
            if (
                !Object.hasOwn(categories, kind) ||
                !name ||
                name.length > 160 ||
                !/\.(gif|webp|png|jpe?g)$/i.test(name) ||
                !Buffer.isBuffer(req.body) ||
                !req.body.length
            )
                return res
                    .status(400)
                    .json({
                        error: "Chọn GIF, WebP, PNG hoặc JPG tối đa 20 MB.",
                    });
            const id = crypto.randomUUID();
            const category = categories[kind];
            const dir = path.join(folder, category, id);
            try {
                await fs.mkdir(dir, { recursive: true });
                const input = path.join(
                    dir,
                    "source" + path.extname(name).toLowerCase(),
                );
                await fs.writeFile(input, req.body);
                if (/\.webp$/i.test(name)) {
                    await run(
                        process.env.PYTHON_PATH || "python",
                        [
                            path.join(
                                __dirname,
                                "../../scripts/decode-webp.py",
                            ),
                            input,
                            dir,
                        ],
                        {
                            windowsHide: true,
                            timeout: 90000,
                            maxBuffer: 1048576,
                        },
                    );
                } else {
                    await run(
                        ffmpeg,
                        [
                            "-hide_banner",
                            "-loglevel",
                            "error",
                            "-threads",
                            "1",
                            ...(/\.gif$/i.test(name) ? [] : ["-loop", "1"]),
                            "-i",
                            input,
                            "-vf",
                            "fps=12,scale=512:512:force_original_aspect_ratio=decrease",
                            "-frames:v",
                            /\.gif$/i.test(name) ? "120" : "1",
                            "-threads",
                            "1",
                            path.join(dir, "frame_%03d.png"),
                        ],
                        {
                            windowsHide: true,
                            timeout: 90000,
                            maxBuffer: 1048576,
                        },
                    );
                }
                const frames = (await fs.readdir(dir))
                    .filter((n) => /^frame_\d+\.png$/.test(n))
                    .sort();
                if (!frames.length) throw new Error("No frames");
                const item = {
                    id,
                    name,
                    kind,
                    enabled: true,
                    fps: 12,
                    frames: frames.length,
                    preview:
                        "/library-files/" +
                        category +
                        "/" +
                        id +
                        "/frame_001.png",
                    source:
                        "/library-files/" +
                        category +
                        "/" +
                        id +
                        "/" +
                        path.basename(input),
                };
                await mutate((data) => {
                    const numberFields = numbering[item.kind];
                    if (numberFields) {
                        const [numberKey, counterKey] = numberFields;
                        item[numberKey] = data[counterKey]++;
                    }
                    if (item.kind === "avatarFrame") {
                        item.avatarRank = [1, 2, 3].find(
                            (rank) =>
                                !data.items.some(
                                    (entry) =>
                                        entry.kind === "avatarFrame" &&
                                        !entry.deleted &&
                                        entry.avatarRank === rank,
                                ),
                        );
                        if (!item.avatarRank) {
                            throw Object.assign(
                                new Error(
                                    "Đã đủ 3 khung avatar. Xóa khung cũ để thay ảnh.",
                                ),
                                { status: 409 },
                            );
                        }
                    }
                    data.items.push(item);
                });
                res.status(201).json(item);
            } catch (e) {
                await fs.rm(dir, { recursive: true, force: true });
                next(e);
            }
        },
    );
    router.post(
        "/update",
        express.json({ limit: "2kb" }),
        async (req, res, next) => {
            try {
                await mutate((data) => {
                    const item = data.items.find(
                        (item) => item.id === req.body?.id && !item.deleted,
                    );
                    if (!item) throw new Error("Asset not found");
                    if (typeof req.body.enabled === "boolean")
                        item.enabled = req.body.enabled;
                    if (
                        req.body.select === true &&
                        ["wing", "banner"].includes(item.kind)
                    )
                        data[item.kind] = item.id;
                    if (!item.enabled && data[item.kind] === item.id)
                        data[item.kind] = "";
                });
                res.json({ ok: true });
            } catch (e) {
                next(e);
            }
        },
    );
    router.post(
        "/delete",
        express.json({ limit: "1kb" }),
        async (req, res, next) => {
            try {
                const removed = await mutate((data) => {
                    const index = data.items.findIndex(
                        (item) => item.id === req.body?.id && !item.deleted,
                    );
                    if (index < 0) return false;
                    const item = data.items[index];
                    if (data[item.kind] === item.id) data[item.kind] = "";
                    if (item.id.startsWith("builtin:")) {
                        item.enabled = false;
                        item.deleted = true;
                    } else {
                        data.items.splice(index, 1);
                    }
                    return item;
                });
                if (!removed)
                    return res
                        .status(404)
                        .json({ error: "Asset không tồn tại." });
                if (!removed.id.startsWith("builtin:")) {
                    const category = categories[removed.kind];
                    if (
                        category &&
                        /^[0-9a-f]{8}(?:-[0-9a-f]{4}){3}-[0-9a-f]{12}$/i.test(
                            removed.id,
                        )
                    ) {
                        const parent = path.resolve(folder, category);
                        const directory = path.resolve(parent, removed.id);
                        if (path.dirname(directory) === parent) {
                            await fs
                                .rm(directory, { recursive: true, force: true })
                                .catch((error) =>
                                    console.warn(
                                        "Could not clean deleted asset files:",
                                        error.message,
                                    ),
                                );
                        }
                    }
                }
                res.json({ ok: true });
            } catch (e) {
                next(e);
            }
        },
    );
    router.post(
        "/defaults",
        express.json({ limit: "1kb" }),
        async (req, res, next) => {
            try {
                await mutate((data) => {
                    if (["wing", "banner"].includes(req.body?.kind))
                        data[req.body.kind] = "";
                });
                res.json({ ok: true });
            } catch (e) {
                next(e);
            }
        },
    );
    router.post(
        "/test",
        express.json({ limit: "2kb" }),
        async (req, res, next) => {
            try {
                const data = await read();
                const id = String(req.body?.id || "");
                if (
                    id &&
                    !data.items.some((item) => item.id === id && !item.deleted)
                )
                    return res
                        .status(400)
                        .json({ error: "Asset không tồn tại." });
                await fs.mkdir(folder, { recursive: true });
                const command = {
                    token: crypto.randomUUID(),
                    id,
                    text: String(req.body?.text || "TEST ASSET").slice(0, 40),
                };
                const temp = path.join(folder, "test.tmp");
                await fs.writeFile(temp, JSON.stringify(command));
                await fs.rename(temp, path.join(folder, "test.json"));
                res.json({ ok: true });
            } catch (e) {
                next(e);
            }
        },
    );
    router.use((error, _req, res, _next) => {
        console.error("Assets:", error.message);
        res.status(error.status || 400).json({
            error:
                error.status === 409
                    ? error.message
                    : error.status === 413
                      ? "File tối đa 20 MB."
                      : "Không xử lý được asset. Kiểm tra định dạng hoặc bộ giải mã FFmpeg/Python Pillow.",
        });
    });
    return router;
}
module.exports = { assetRouter };
