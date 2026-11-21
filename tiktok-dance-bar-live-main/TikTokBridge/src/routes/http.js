const express = require("express");
const fs = require("node:fs/promises");
const path = require("node:path");
const projectPaths = require("../config/paths");
const { isAllowedOrigin, isLoopbackAddress } = require("../security");

function mountHttpRoutes(app, port, getConfig) {
    function requireStudioOrigin(req, res, next) {
        if (
            !isAllowedOrigin(req.headers.origin, port) ||
            req.headers["sec-fetch-site"] === "cross-site"
        ) {
            return res.status(403).json({ error: "Forbidden" });
        }
        next();
    }

    const staticOptions = {
        dotfiles: "deny",
        fallthrough: true,
        etag: true,
        index: ["index.html"],
    };
    app.get("/", (_req, res) => res.redirect("/control.html"));
    app.use(express.static(projectPaths.public, staticOptions));
    app.use(
        "/assets/gifs",
        express.static(projectPaths.characterGifs, staticOptions),
    );
    app.use(
        "/assets/banners",
        express.static(projectPaths.banners, staticOptions),
    );
    app.use(
        "/assets/boss",
        express.static(projectPaths.special, staticOptions),
    );
    app.use(
        "/assets/source",
        express.static(projectPaths.sourceVideos, staticOptions),
    );

    app.get("/api/health", (_req, res) => {
        res.json({
            status: "ok",
            appId: "tiktok-live-bridge",
            version: "1.0.0",
        });
    });

    app.get("/api/config", (_req, res) => res.json(getConfig()));
    app.get(
        "/api/avatar",
        requireStudioOrigin,
        require("./avatar").serveAvatar,
    );

    app.get("/api/gifs", async (_req, res) => {
        try {
            const files = await fs.readdir(projectPaths.characterGifs);
            res.json(
                files.filter((file) => file.toLowerCase().endsWith(".gif")),
            );
        } catch (error) {
            if (error.code === "ENOENT") return res.json([]);
            console.error("Không thể đọc thư mục GIF:", error);
            res.status(500).json({ error: "Không thể đọc danh sách GIF" });
        }
    });

    const musicFolder = projectPaths.music;
    app.use(
        "/api/music",
        requireStudioOrigin,
        require("./music").musicRouter(musicFolder),
    );
    app.use(
        "/music-files",
        express.static(musicFolder, { dotfiles: "deny", index: false }),
    );

    const backgroundsFolder = projectPaths.backgrounds;
    app.use(
        "/api/backgrounds",
        requireStudioOrigin,
        require("./backgrounds").backgroundsRouter(backgroundsFolder),
    );
    app.get("/background-default.png", (_req, res) =>
        res.sendFile(path.join(backgroundsFolder, "default.png")),
    );
    app.use(
        "/background-files",
        express.static(path.join(backgroundsFolder, "images"), {
            dotfiles: "deny",
            index: false,
        }),
    );

    const stageFolder = projectPaths.videos;
    app.use(
        "/api/stage",
        requireStudioOrigin,
        require("./stage").stageRouter(stageFolder),
    );
    app.use(
        "/stage-videos",
        express.static(stageFolder, { dotfiles: "deny", index: false }),
    );

    app.post("/api/game/open", async (req, res) => {
        if (
            !isLoopbackAddress(req.socket.remoteAddress) ||
            !isAllowedOrigin(req.headers.origin, port) ||
            req.headers["sec-fetch-site"] === "cross-site" ||
            req.headers["x-studio-action"] !== "open-game"
        )
            return res.status(403).json({ error: "Forbidden" });
        try {
            res.json(await require("./open-game").openGame(projectPaths.root));
        } catch (error) {
            console.error("Open game:", error.message);
            res.status(500).json({
                error: "Không mở được game. Kiểm tra file Build/TIKTOK_LIVE_BAR.exe hoặc chạy run.bat.",
            });
        }
    });

    app.use(
        "/api/library",
        (req, res, next) => {
            if (
                !isLoopbackAddress(req.socket.remoteAddress) ||
                !isAllowedOrigin(req.headers.origin, port) ||
                req.headers["sec-fetch-site"] === "cross-site"
            )
                return res.status(403).json({ error: "Forbidden" });
            next();
        },
        require("./asset-library").assetRouter(
            projectPaths.root,
            process.env.FFMPEG_PATH || "ffmpeg",
        ),
    );
    app.use(
        "/builtin-assets",
        express.static(projectPaths.resources, {
            dotfiles: "deny",
            index: false,
        }),
    );
    app.use(
        "/library-files",
        express.static(projectPaths.library, {
            dotfiles: "deny",
            index: false,
        }),
    );

    app.use((_req, res) =>
        res.status(404).type("text/plain").send("Not found"),
    );
}

module.exports = { mountHttpRoutes };
