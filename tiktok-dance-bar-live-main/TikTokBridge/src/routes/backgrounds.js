const express = require("express");
const fs = require("node:fs/promises");
const path = require("node:path");
const crypto = require("node:crypto");
function backgroundsRouter(root) {
    const router = express.Router();
    const folder = path.join(root, "images");
    const config = path.join(root, "settings.json");
    const valid = (name) =>
        typeof name === "string" &&
        name.length > 0 &&
        name.length <= 160 &&
        !/[<>:"/\\|?*\x00-\x1f]/.test(name) &&
        /\.(png|jpe?g)$/i.test(name);
    async function list() {
        await fs.mkdir(folder, { recursive: true });
        const files = (
            await fs.readdir(folder, { withFileTypes: true })
        ).filter((item) => item.isFile() && valid(item.name));
        return [
            {
                name: "__stage3d__",
                label: "Sân khấu 3D · Không dùng ảnh nền",
                url: "",
            },
            {
                name: "nenamphu.png",
                label: "Nền mặc định",
                url: "/background-default.png",
            },
            ...files.map((item) => ({
                name: item.name,
                label: item.name,
                url: "/background-files/" + encodeURIComponent(item.name),
            })),
        ];
    }
    router.get("/", async (_req, res, next) => {
        try {
            let selected = "nenamphu.png";
            try {
                selected =
                    JSON.parse(await fs.readFile(config, "utf8")).selected ||
                    selected;
            } catch (error) {
                if (error.code !== "ENOENT") throw error;
            }
            res.json({ selected, images: await list() });
        } catch (error) {
            next(error);
        }
    });
    router.post(
        "/select",
        express.json({ limit: "2kb" }),
        async (req, res, next) => {
            try {
                const images = await list();
                if (!images.some((image) => image.name === req.body?.selected))
                    return res
                        .status(400)
                        .json({ error: "Không tìm thấy ảnh nền." });
                const temp = config + "." + crypto.randomUUID() + ".tmp";
                await fs.writeFile(
                    temp,
                    JSON.stringify({ selected: req.body.selected }),
                );
                await fs.rename(temp, config);
                res.json({ selected: req.body.selected });
            } catch (error) {
                next(error);
            }
        },
    );
    router.post(
        "/upload",
        express.raw({ type: "application/octet-stream", limit: "20mb" }),
        async (req, res, next) => {
            const name = req.query.name;
            const body = req.body;
            if (
                !valid(name) ||
                name === "nenamphu.png" ||
                !Buffer.isBuffer(body)
            )
                return res.status(400).json({
                    error: "Chọn ảnh PNG hoặc JPG hợp lệ, tên khác nền mặc định.",
                });
            const png =
                body.length > 24 &&
                body
                    .subarray(0, 8)
                    .equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]));
            const jpeg =
                body.length > 4 &&
                body[0] === 255 &&
                body[1] === 216 &&
                body[2] === 255;
            if (/\.png$/i.test(name) ? !png : !jpeg)
                return res.status(400).json({
                    error: "Nội dung file không đúng định dạng PNG/JPG.",
                });
            try {
                await fs.mkdir(folder, { recursive: true });
                await fs.writeFile(path.join(folder, name), body, {
                    flag: "wx",
                });
                res.status(201).json({ name });
            } catch (error) {
                if (error.code === "EEXIST")
                    return res.status(409).json({
                        error: "Tên ảnh đã tồn tại. Hãy đổi tên file.",
                    });
                next(error);
            }
        },
    );
    router.use((error, _req, res, _next) => {
        console.error("Background:", error.message);
        res.status(error.status || 500).json({
            error:
                error.status === 413
                    ? "Ảnh tối đa 20 MB."
                    : "Không thể xử lý phông nền.",
        });
    });
    return router;
}
module.exports = { backgroundsRouter };
