const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const path = require("node:path");
const os = require("node:os");
const { promisify } = require("node:util");
const run = promisify(require("node:child_process").execFile);
const express = require("express");
const { assetRouter } = require("../src/routes/asset-library");
test("WebP import preserves static and animated frame counts and transparent alpha", async (t) => {
    const python = process.env.PYTHON_PATH || "python";
    try {
        await run(python, [
            "-c",
            'from PIL import Image, features; assert features.check("webp")',
        ]);
    } catch {
        t.skip("Python Pillow with WebP support is not installed");
        return;
    }
    const root = await fs.mkdtemp(path.join(os.tmpdir(), "asset-webp-"));
    await fs.mkdir(
        path.join(root, "UnityProject/Assets/Resources/Characters"),
        { recursive: true },
    );
    await fs.mkdir(path.join(root, "UnityProject/Assets/Resources/Banners"), {
        recursive: true,
    });
    const script =
        'from PIL import Image; import sys; from pathlib import Path; p=Path(sys.argv[1]); a=Image.new("RGBA",(32,32),(255,0,0,128)); b=Image.new("RGBA",(32,32),(0,0,255,128)); a.save(p/"animated.webp",save_all=True,append_images=[b],duration=[500,500],loop=0,lossless=True); a.save(p/"static.webp",lossless=True)';
    const app = express();
    app.use("/assets", assetRouter(root, "ffmpeg"));
    const server = app.listen(0, "127.0.0.1");
    await new Promise((resolve) => server.once("listening", resolve));
    try {
        await run(python, ["-c", script, root]);
        const url = `http://127.0.0.1:${server.address().port}/assets`;
        for (const [name, count] of [
            ["static.webp", 1],
            ["animated.webp", 12],
        ]) {
            const response = await fetch(
                url + "/upload?kind=wing&name=" + name,
                {
                    method: "POST",
                    headers: { "Content-Type": "application/octet-stream" },
                    body: await fs.readFile(path.join(root, name)),
                },
            );
            const item = await response.json();
            assert.equal(response.status, 201, JSON.stringify(item));
            assert.equal(item.frames, count);
            const dir = path.join(root, "Data/Assets/wings", item.id);
            await run(python, [
                "-c",
                'from PIL import Image; import sys; from pathlib import Path; p=Path(sys.argv[1]); frames=sorted(p.glob("frame_*.png")); assert Image.open(frames[0]).getextrema()[-1]==(128,128); assert len(frames)==1 or Image.open(frames[0]).tobytes()!=Image.open(frames[-1]).tobytes()',
                dir,
            ]);
        }
        const frame = await fs.readFile(path.join(root, "static.webp"));
        const uploadFrame = () =>
            fetch(url + "/upload?kind=avatarFrame&name=frame.webp", {
                method: "POST",
                headers: { "Content-Type": "application/octet-stream" },
                body: frame,
            });
        const ids = [];
        for (const rank of [1, 2, 3]) {
            const response = await uploadFrame();
            assert.equal(response.status, 201);
            const item = await response.json();
            assert.equal(item.avatarRank, rank);
            ids.push(item.id);
        }
        assert.equal((await uploadFrame()).status, 409);
        const deleted = await fetch(url + "/delete", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ id: ids[1] }),
        });
        assert.equal(deleted.status, 200);
        const replacement = await uploadFrame();
        assert.equal(replacement.status, 201);
        assert.equal((await replacement.json()).avatarRank, 2);
    } finally {
        await new Promise((resolve) => server.close(resolve));
        await fs.rm(root, { recursive: true, force: true });
    }
});
