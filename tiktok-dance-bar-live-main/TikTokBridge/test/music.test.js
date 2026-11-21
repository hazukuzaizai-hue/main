const test = require("node:test");
const assert = require("node:assert/strict");
const fs = require("node:fs/promises");
const os = require("node:os");
const path = require("node:path");
const express = require("express");
const { musicRouter } = require("../src/routes/music");
test("music uploads preserve tracks, reject unsafe names and persist settings", async () => {
    const folder = await fs.mkdtemp(path.join(os.tmpdir(), "music-test-"));
    const app = express();
    app.use("/music", musicRouter(folder));
    const server = app.listen(0, "127.0.0.1");
    await new Promise((resolve) => server.once("listening", resolve));
    const url = `http://127.0.0.1:${server.address().port}/music`;
    try {
        const upload = (name) =>
            fetch(url + "/upload?name=" + encodeURIComponent(name), {
                method: "POST",
                headers: { "Content-Type": "application/octet-stream" },
                body: Buffer.from("RIFF test audio"),
            });
        assert.equal((await upload("../escape.mp3")).status, 400);
        assert.equal((await upload("script.js")).status, 400);
        assert.equal((await upload("Bài hát.wav")).status, 201);
        assert.equal((await upload("Bài hát.wav")).status, 409);
        assert.equal(
            await fs.readFile(path.join(folder, "Bài hát.wav"), "utf8"),
            "RIFF test audio",
        );
        let result = await fetch(url + "/settings", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ enabled: false, volume: 0.2 }),
        });
        assert.equal(result.status, 200);
        const state = await (await fetch(url)).json();
        assert.equal(state.enabled, false);
        assert.equal(state.volume, 0.2);
        assert.equal(state.tracks.length, 1);
        assert.equal(state.tracks[0].name, "Bài hát.wav");
        result = await fetch(url + "/settings", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: '{"enabled":true,"volume":2}',
        });
        assert.equal(result.status, 400);
        const command = (action, track) =>
            fetch(url + "/command", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ action, track }),
            });
        assert.equal((await command("play", "../bad.mp3")).status, 400);
        assert.equal((await command("play", "missing.mp3")).status, 404);
        assert.equal((await command("play", "Bài hát.wav")).status, 200);
        const saved = JSON.parse(
            await fs.readFile(path.join(folder, "MUSIC_COMMAND.json"), "utf8"),
        );
        assert.equal(saved.action, "play");
        assert.equal(saved.track, "Bài hát.wav");
        const settings = JSON.parse(
            await fs.readFile(path.join(folder, "MUSIC_SETTINGS.json"), "utf8"),
        );
        assert.equal(settings.enabled, true);
        assert.equal(settings.volume, 0.2);
        assert.equal((await command("pause")).status, 200);
        assert.notEqual(
            JSON.parse(
                await fs.readFile(
                    path.join(folder, "MUSIC_COMMAND.json"),
                    "utf8",
                ),
            ).id,
            saved.id,
        );
        assert.equal(
            (await (await fetch(url + "/status")).json()).state,
            "offline",
        );
    } finally {
        await new Promise((resolve) => server.close(resolve));
        await fs.rm(folder, { recursive: true, force: true });
    }
});
