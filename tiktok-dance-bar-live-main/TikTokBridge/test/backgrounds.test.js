const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const os = require("node:os");
const path = require("node:path");
const express = require("express");
const { backgroundsRouter } = require("../src/routes/backgrounds");
test("background upload validates files, preserves duplicates and saves selected map", async () => {
    const folder = await fs.mkdtemp(path.join(os.tmpdir(), "background-test-"));
    const app = express();
    app.use("/backgrounds", backgroundsRouter(folder));
    const server = app.listen(0, "127.0.0.1");
    await new Promise((resolve) => server.once("listening", resolve));
    const url = `http://127.0.0.1:${server.address().port}/backgrounds`;
    const png = Buffer.from(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+a3ioAAAAASUVORK5CYII=",
        "base64",
    );
    const upload = (name, body = png) =>
        fetch(url + "/upload?name=" + encodeURIComponent(name), {
            method: "POST",
            headers: { "Content-Type": "application/octet-stream" },
            body,
        });
    const select = (selected) =>
        fetch(url + "/select", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ selected }),
        });
    try {
        assert.equal((await upload("../bad.png")).status, 400);
        assert.equal(
            (await upload("bad.png", Buffer.from("not image"))).status,
            400,
        );
        assert.equal((await upload("Ảnh nền.png")).status, 201);
        assert.equal((await upload("Ảnh nền.png")).status, 409);
        assert.equal((await select("../bad.png")).status, 400);
        assert.equal((await select("Ảnh nền.png")).status, 200);
        const state = await (await fetch(url)).json();
        assert.equal(state.selected, "Ảnh nền.png");
        assert.equal(state.images.length, 3);
        assert.equal(
            JSON.parse(
                await fs.readFile(path.join(folder, "settings.json"), "utf8"),
            ).selected,
            "Ảnh nền.png",
        );
        assert.equal((await select("nenamphu.png")).status, 200);
        assert.equal((await select("__stage3d__")).status, 200);
    } finally {
        await new Promise((resolve) => server.close(resolve));
        await fs.rm(folder, { recursive: true, force: true });
    }
});
