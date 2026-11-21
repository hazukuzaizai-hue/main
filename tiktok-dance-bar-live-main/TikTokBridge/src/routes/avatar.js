const { execFile } = require("node:child_process");
const { promisify } = require("node:util");

const run = promisify(execFile);
const maxBytes = 2 * 1024 * 1024;
const cdnHost =
    /(?:^|\.)(?:tiktokcdn(?:-[a-z0-9]+)?\.com|tiktokcdn\.us|tiktokcdn\.eu|tiktokv\.com|ibytedtos\.com|byteoversea\.com)$/i;

async function serveAvatar(req, res) {
    let url;
    try {
        url = new URL(typeof req.query.url === "string" ? req.query.url : "");
    } catch {
        return res.sendStatus(400);
    }
    const giftIcon =
        url.hostname === "tiktokgame.vn" &&
        /^\/gift\/uploads\/[^/]+\.(?:png|webp|jpe?g|gif)$/i.test(
            decodeURIComponentSafe(url.pathname),
        ) &&
        !url.search;
    if (
        url.protocol !== "https:" ||
        url.port ||
        url.username ||
        url.password ||
        (!cdnHost.test(url.hostname) && !giftIcon)
    )
        return res.sendStatus(400);

    try {
        const source = await fetch(url, {
            redirect: "error",
            signal: AbortSignal.timeout(5000),
        });
        if (!source.ok || !source.body) return res.sendStatus(502);
        const chunks = [];
        let size = 0;
        for await (const chunk of source.body) {
            size += chunk.length;
            if (size > maxBytes) return res.sendStatus(413);
            chunks.push(chunk);
        }
        const input = Buffer.concat(chunks, size);
        if (
            input
                .subarray(0, 8)
                .equals(Buffer.from([137, 80, 78, 71, 13, 10, 26, 10]))
        )
            return res.type("png").send(input);
        if (input[0] === 255 && input[1] === 216 && input[2] === 255)
            return res.type("jpg").send(input);
        const webp =
            input.toString("ascii", 0, 4) === "RIFF" &&
            input.toString("ascii", 8, 12) === "WEBP";
        const gif = /^GIF8[79]a$/.test(input.toString("ascii", 0, 6));
        if (!webp && !gif) return res.sendStatus(415);

        const conversion = run(
            process.env.FFMPEG_PATH || "ffmpeg",
            [
                "-loglevel",
                "error",
                "-f",
                webp ? "webp_pipe" : "gif",
                "-i",
                "pipe:0",
                "-frames:v",
                "1",
                "-vf",
                "scale=256:256:force_original_aspect_ratio=decrease",
                "-threads",
                "1",
                "-f",
                "image2pipe",
                "-c:v",
                "png",
                "pipe:1",
            ],
            {
                encoding: "buffer",
                timeout: 5000,
                maxBuffer: maxBytes,
                windowsHide: true,
            },
        );
        conversion.child.stdin.on("error", () => {});
        conversion.child.stdin.end(input);
        const { stdout } = await conversion;
        if (!stdout.length) return res.sendStatus(502);
        return res.type("png").send(stdout);
    } catch {
        return res.sendStatus(502);
    }
}

function decodeURIComponentSafe(pathname) {
    try {
        return decodeURIComponent(pathname);
    } catch {
        return "";
    }
}

module.exports = { serveAvatar };
