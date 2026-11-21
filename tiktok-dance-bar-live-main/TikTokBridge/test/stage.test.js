const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const os = require("node:os");
const path = require("node:path");
const express = require("express");
const { stageRouter } = require("../src/routes/stage");
test("stage settings validate modes and files, preserve uploads and save screen settings", async () => {
    const folder = await fs.mkdtemp(path.join(os.tmpdir(), "stage-test-"));
    const app = express();
    app.use("/stage", stageRouter(folder));
    const server = app.listen(0, "127.0.0.1");
    await new Promise((resolve) => server.once("listening", resolve));
    const url = `http://127.0.0.1:${server.address().port}/stage`;
    const save = (body) =>
        fetch(url + "/settings", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(body),
        });
    const upload = (name) =>
        fetch(url + "/upload?name=" + encodeURIComponent(name), {
            method: "POST",
            headers: { "Content-Type": "application/octet-stream" },
            body: Buffer.from([
                0, 0, 0, 20, 102, 116, 121, 112, 105, 115, 111, 109, 0, 0, 0, 0,
                105, 115, 111, 109,
            ]),
        });
    try {
        assert.equal((await upload("../bad.mp4")).status, 400);
        assert.equal((await upload("clip.mp4")).status, 201);
        assert.equal((await upload("clip.mp4")).status, 409);
        assert.equal(
            (
                await save({
                    mode: "video",
                    video: "missing.mp4",
                })
            ).status,
            400,
        );
        assert.equal(
            (
                await save({
                    mode: "video",
                    video: "clip.mp4",
                })
            ).status,
            200,
        );
        let state = await (await fetch(url)).json();
        assert.equal(state.mode, "video");
        assert.equal(state.video, "clip.mp4");
        assert.equal(state.videos.length, 1);
        assert.equal(
            (
                await save({
                    mode: "invalid",
                    video: "",
                })
            ).status,
            400,
        );
        assert.equal(
            (
                await save({
                    mode: "equalizer",
                    video: "",
                })
            ).status,
            200,
        );
        assert.equal(
            JSON.parse(
                await fs.readFile(
                    path.join(folder, "STAGE_SETTINGS.json"),
                    "utf8",
                ),
            ).mode,
            "equalizer",
        );
    } finally {
        await new Promise((resolve) => server.close(resolve));
        await fs.rm(folder, { recursive: true, force: true });
    }
});
