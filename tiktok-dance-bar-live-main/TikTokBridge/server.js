/* ═══════════════════════════════════════════════════════════
   TikTok Live Bridge Server
   Phát hành theo giấy phép MIT. Xem file ../LICENSE.
═══════════════════════════════════════════════════════════ */

const express = require("express");
const http = require("http");
const path = require("path");
const fs = require("fs/promises");
const WebSocket = require("ws");
const {
    getServerSettings,
    loadEnvironmentFile,
} = require("./src/config/environment");

loadEnvironmentFile();

const {
    normalizeTikFinityMessage,
} = require("./src/tiktok/normalize-tikfinity-event");
const {
    normalizeChat,
    normalizeMember,
    normalizeGift,
    normalizeLike,
    normalizeSocial,
    isPendingGiftStreak,
} = require("./src/tiktok/normalize-event");

const gameConfig = require("./config/game.json");
const giftConfig = require("./config/gifts.json");
const initialMasterConfig = require("./config/master.json");
const initialObservedGifts = require("./config/observed-gifts.json");
const vietnamGifts = require("./config/vietnam-gifts.json").gifts;
const {
    normalizeText,
    sanitizeMasterConfig,
    resolveMasterRule,
    applyRule,
    applyBuiltInChatCommand,
} = require("./src/master/rules");
const {
    sanitizeGameEvent,
    isLoopbackAddress,
    isAllowedHost,
    isAllowedOrigin,
    consumeRateLimit,
} = require("./src/security");

const app = express();
const server = http.createServer(app);

const { port: PORT, host: HOST, allowLan: ALLOW_LAN } = getServerSettings();
const wss = new WebSocket.Server({
    noServer: true,
    maxPayload: 256 * 1024,
    perMessageDeflate: false,
    clientTracking: true,
});
const masterConfigPath = path.join(__dirname, "config", "master.json");
let masterWrite = Promise.resolve();
let pendingMasterSaves = 0;
function persistMaster(config) {
    const contents = JSON.stringify(config, null, 2) + "\n";
    const write = masterWrite
        .catch(() => {})
        .then(async () => {
            const temporary = masterConfigPath + ".tmp";
            await fs.writeFile(temporary, contents, "utf8");
            await fs.rename(temporary, masterConfigPath);
        });
    masterWrite = write;
    return write;
}

const observedGiftsPath = path.join(__dirname, "config", "observed-gifts.json");
const LIVE_PROVIDER = String(
    process.env.LIVE_PROVIDER || gameConfig.liveProvider || "tiktok",
).toLowerCase();
const TIKFINITY_WS_URL = String(
    process.env.TIKFINITY_WS_URL ||
        gameConfig.tikfinityWsUrl ||
        "ws://127.0.0.1:21213/",
);

let liveConnection = null;
let connectionAttempt = 0;
let desiredUsername = null;
let reconnectTimer = null;
let reconnectFailures = 0;
let demoTimer = null;
const demoTimeouts = new Set();
let connectionStatus = {
    state: "idle",
    username: null,
    message: "Chưa kết nối",
};
let metrics = createMetrics();
let masterConfig = sanitizeMasterConfig(initialMasterConfig);
const recentEventIds = new Map();
const sessionPlayers = new Map();
const sessionPersonalHearts = new Map();
const sessionVipScores = new Map();
const observedGifts = new Map(
    (Array.isArray(initialObservedGifts) ? initialObservedGifts : [])
        .map((gift) => [String(gift.giftId || gift.giftName || ""), gift])
        .filter(([key]) => key),
);
let observedGiftSaveTimer = null;
const roomGifts = new Map();
const roomGiftCachePath = path.join(
    __dirname,
    "config",
    "room-gift-cache.json",
);
const savedRoomGifts = new Map();
try {
    const cached = require(roomGiftCachePath);
    if (Array.isArray(cached))
        for (const gift of cached) savedRoomGifts.set(gift.giftId, gift);
} catch (error) {
    if (error.code !== "MODULE_NOT_FOUND")
        console.warn("Không đọc được danh mục quà đã đồng bộ:", error.message);
}
let giftCatalogState = "saved";

if (
    !ALLOW_LAN &&
    !isLoopbackAddress(HOST) &&
    HOST.toLowerCase() !== "localhost"
) {
    throw new Error(
        "Từ chối mở Node ra mạng LAN. Chỉ đặt ALLOW_LAN=1 khi đã có lớp xác thực bên ngoài.",
    );
}

function createMetrics() {
    return {
        source: "idle",
        events: 0,
        members: 0,
        chats: 0,
        gifts: 0,
        diamonds: 0,
        likes: 0,
        startedAt: null,
    };
}

app.disable("x-powered-by");
app.use((req, res, next) => {
    if (!isAllowedHost(req.headers.host, PORT, ALLOW_LAN)) {
        return res.status(403).type("text/plain").send("Forbidden");
    }
    res.setHeader("X-Content-Type-Options", "nosniff");
    res.setHeader("X-Frame-Options", "DENY");
    res.setHeader("Referrer-Policy", "no-referrer");
    res.setHeader(
        "Permissions-Policy",
        "camera=(), microphone=(), geolocation=(), payment=()",
    );
    res.setHeader("Cross-Origin-Resource-Policy", "same-origin");
    res.setHeader(
        "Content-Security-Policy",
        [
            "default-src 'self'",
            "base-uri 'none'",
            "object-src 'none'",
            "frame-ancestors 'none'",
            "form-action 'self'",
            "script-src 'self' 'unsafe-inline'",
            "style-src 'self' 'unsafe-inline'",
            "img-src 'self' data: blob: https:",
            "media-src 'self' blob:",
            `connect-src 'self' ws://127.0.0.1:${PORT} ws://localhost:${PORT}`,
        ].join("; "),
    );
    if (req.path.startsWith("/api/") || req.path.endsWith(".html")) {
        res.setHeader("Cache-Control", "no-store");
    }
    next();
});

require("./src/routes/http").mountHttpRoutes(app, PORT, () => ({
    game: gameConfig,
    gifts: giftConfig,
    master: masterConfig,
    observedGifts: [...observedGifts.values()],
}));

function broadcast(data) {
    const payload = JSON.stringify(data);
    for (const client of wss.clients) {
        if (client.readyState !== WebSocket.OPEN) continue;
        if (client.bufferedAmount > 512 * 1024) {
            client.terminate();
            continue;
        }
        client.send(payload);
    }
}

function send(ws, data) {
    if (ws.readyState !== WebSocket.OPEN) return;
    if (ws.bufferedAmount > 512 * 1024) return ws.terminate();
    ws.send(JSON.stringify(data));
}

function setStatus(state, username, message) {
    connectionStatus = { state, username, message };
    broadcast({ type: "status", ...connectionStatus });
}

function broadcastMetrics() {
    broadcast({ type: "metrics", ...metrics });
}

function giftCatalogMessage() {
    const combined = new Map(vietnamGifts.map((gift) => [gift.giftId, gift]));
    for (const [id, gift] of savedRoomGifts) {
        const reference = combined.get(id);
        combined.set(id, {
            ...reference,
            ...gift,
            giftPictureUrl:
                gift.giftPictureUrl || reference?.giftPictureUrl || "",
        });
    }
    for (const [id, gift] of observedGifts) {
        const cached = combined.get(id);
        combined.set(id, {
            ...cached,
            ...gift,
            diamondCount: gift.diamondCount || cached?.diamondCount || 0,
            giftPictureUrl: gift.giftPictureUrl || cached?.giftPictureUrl || "",
        });
    }
    for (const [id, gift] of roomGifts) {
        const previous = combined.get(id);
        combined.set(id, {
            ...previous,
            ...gift,
            giftPictureUrl:
                gift.giftPictureUrl || previous?.giftPictureUrl || "",
        });
    }
    return {
        type: "gift_catalog",
        state: giftCatalogState,
        roomCount: roomGifts.size,
        vietnamReferenceCount: vietnamGifts.length,
        gifts: [...combined.values()].sort(
            (a, b) =>
                (Number(a.diamondCount) || 0) - (Number(b.diamondCount) || 0) ||
                String(a.giftName || "").localeCompare(
                    String(b.giftName || ""),
                ),
        ),
    };
}

async function refreshRoomGifts(connection) {
    if (giftCatalogState === "loading") return;
    giftCatalogState = "loading";
    broadcast(giftCatalogMessage());
    try {
        const gifts = await connection.fetchAvailableGifts();
        if (connection !== liveConnection) return;
        if (!Array.isArray(gifts))
            throw new Error("Danh mục phòng không hợp lệ");
        roomGifts.clear();
        for (const gift of gifts) {
            const giftId = String(
                gift?.id ?? gift?.gift_id ?? gift?.giftId ?? "",
            );
            if (!/^\d+$/.test(giftId) || !gift.name) continue;
            const image =
                gift.image?.url_list?.[0] ??
                gift.image?.urlList?.[0] ??
                gift.image?.url?.[0];
            roomGifts.set(giftId, {
                giftId,
                giftName: String(gift.name),
                diamondCount:
                    Number(gift.diamond_count ?? gift.diamondCount) || 0,
                giftPictureUrl:
                    typeof image === "string" && image.startsWith("https://")
                        ? image
                        : "",
            });
        }
        if (!roomGifts.size)
            throw new Error("TikTok trả về danh mục quà trống");
        giftCatalogState = "room";
        for (const [id, gift] of roomGifts) {
            const previous = savedRoomGifts.get(id);
            savedRoomGifts.set(id, {
                ...gift,
                giftPictureUrl:
                    gift.giftPictureUrl || previous?.giftPictureUrl || "",
            });
        }
        void fs
            .writeFile(
                roomGiftCachePath,
                JSON.stringify([...savedRoomGifts.values()], null, 2) + "\n",
                "utf8",
            )
            .catch((error) =>
                console.warn(
                    "Không lưu được danh mục quà của phòng:",
                    error.message,
                ),
            );
    } catch (error) {
        if (connection !== liveConnection) return;
        giftCatalogState = "failed";
        console.warn("Không tải được danh mục quà của phòng:", error.message);
    }
    broadcast(giftCatalogMessage());
    broadcast(viewerGuideMessage());
}

function viewerGuideMessage(config = masterConfig) {
    const labels = {
        join: "Vào sàn",
        change: "Đổi nhân vật",
        wing: "Đổi cánh",
        avatar_frame: "Khung avatar",
        camera: "Zoom",
        medal: "Cánh và huy hiệu",
        vip: "Zoom",
        vip_dance: "Quẩy VIP",
        vip_set: "Bộ VIP",
        walk: "Đi vòng",
        fireworks: "Pháo hoa",
        topdj: "Top DJ và pháo hoa",
        grow: "Phóng lớn",
        jump: "Nhảy cao",
        dance: "Nhảy theo nhạc",
    };
    const guideItems = [];
    const ruleOrder = new Map(
        config.rules.map((rule, index) => [rule.id, index]),
    );
    for (const gift of giftCatalogMessage().gifts) {
        if (!gift.giftId) continue;
        const rule = resolveMasterRule(config, { ...gift, type: "gift" });
        if (!rule) continue;
        guideItems.push({
            giftId: gift.giftId,
            giftName: gift.giftName,
            diamondCount: gift.diamondCount,
            giftPictureUrl: gift.giftPictureUrl || "",
            label: labels[rule.action],
            ruleId: rule.id,
        });
    }
    guideItems.sort(
        (first, second) =>
            ruleOrder.get(first.ruleId) - ruleOrder.get(second.ruleId),
    );
    const interactionHints = config.rules
        .filter(
            (rule) => rule.enabled && rule.source === "chat" && rule.trigger,
        )
        .map(
            (rule) =>
                `Gõ "${
                    rule.trigger
                        .split(",")
                        .find((value) => value.trim())
                        ?.trim() || rule.trigger
                }" → ${labels[rule.action]}`,
        );
    const shareRule = resolveMasterRule(config, { type: "share" });
    if (shareRule)
        interactionHints.push(`Chia sẻ → ${labels[shareRule.action]}`);
    const heartRules = config.rules.filter(
        (rule) => rule.enabled && rule.source === "like",
    );
    return { type: "viewer_guide", guideItems, interactionHints, heartRules };
}

function scheduleObservedGiftSave() {
    clearTimeout(observedGiftSaveTimer);
    observedGiftSaveTimer = setTimeout(() => {
        observedGiftSaveTimer = null;
        fs.writeFile(
            observedGiftsPath,
            `${JSON.stringify([...observedGifts.values()], null, 2)}\n`,
            "utf8",
        ).catch((error) =>
            console.error("Không thể lưu thư viện gift:", error),
        );
    }, 250);
}

function learnObservedGift(event) {
    const giftId = String(event.giftId || "").trim();
    const giftName = String(event.giftName || "Gift").trim();
    const key = giftId || giftName.toLocaleLowerCase("vi");
    if (!key) return;
    const previous = observedGifts.get(key) || {};
    const repeats = Math.max(1, Number(event.repeatCount) || 1);
    const unitDiamonds = Math.max(
        0,
        Number(event.unitDiamondCount) ||
            Math.round((Number(event.diamondCount) || 0) / repeats),
    );
    const learnedGift = {
        ...previous,
        giftId,
        giftName,
        diamondCount: unitDiamonds || Number(previous.diamondCount) || 0,
        giftPictureUrl: nonEmpty(
            event.giftPictureUrl,
            previous.giftPictureUrl || "",
        ),
        lastSeenAt: Date.now(),
    };
    observedGifts.set(key, learnedGift);
    if (observedGifts.size > 500) {
        const oldest = [...observedGifts.entries()].sort(
            (a, b) =>
                (Number(a[1].lastSeenAt) || 0) - (Number(b[1].lastSeenAt) || 0),
        );
        for (let index = 0; index < oldest.length - 500; index += 1)
            observedGifts.delete(oldest[index][0]);
    }
    let masterChanged = false;
    for (const rule of masterConfig.rules || []) {
        if (!giftId || pendingMasterSaves) break;
        if (
            !rule.enabled ||
            rule.source !== "gift" ||
            rule.giftId ||
            rule.match !== "exact"
        )
            continue;
        const aliases = String(rule.trigger || "")
            .split(",")
            .map(normalizeText)
            .filter(Boolean);
        if (!aliases.includes(normalizeText(giftName))) continue;
        if (
            giftCatalogMessage().gifts.some(
                (gift) =>
                    gift.giftId &&
                    gift.giftId !== giftId &&
                    normalizeText(gift.giftName) === normalizeText(giftName),
            )
        )
            continue;
        rule.giftId = giftId;
        rule.displayDiamonds = unitDiamonds || rule.displayDiamonds || 0;
        masterChanged = true;
        break;
    }
    if (masterChanged) {
        persistMaster(masterConfig).catch((error) =>
            console.error("Không thể tự gắn Gift ID vào Master:", error),
        );
        broadcast({ type: "master_config", master: masterConfig });
    }
    scheduleObservedGiftSave();
    broadcast(giftCatalogMessage());
    broadcast(viewerGuideMessage());
}

function isRealObservedGift(event) {
    const giftId = String(event.giftId || "");
    return (
        event.userId !== "master-test" &&
        !giftId.startsWith("demo-") &&
        !giftId.startsWith("master-")
    );
}

function resetSessionState(source = metrics.source) {
    metrics = {
        ...createMetrics(),
        source,
        startedAt: source === "idle" ? null : Date.now(),
    };
    sessionPlayers.clear();
    sessionPersonalHearts.clear();
    sessionVipScores.clear();
    recentEventIds.clear();
}

function createSnapshot() {
    pruneSessionPlayers();
    return {
        type: "snapshot",
        players: [...sessionPlayers.values()],
        vipScores: [...sessionVipScores.values()],
        roomHearts: metrics.liveHearts || 0,
    };
}

function pruneSessionPlayers(now = Date.now()) {
    const cutoff =
        now - Math.max(1000, Number(gameConfig.playerTtlMs) || 600000);
    for (const [userId, player] of sessionPlayers) {
        if ((Number(player.lastActive) || 0) < cutoff)
            sessionPlayers.delete(userId);
    }
    const maximum = Math.max(1, Number(gameConfig.maxPlayers) || 200);
    if (sessionPlayers.size <= maximum) return;
    const oldest = [...sessionPlayers.values()].sort(
        (a, b) => (Number(a.lastActive) || 0) - (Number(b.lastActive) || 0),
    );
    for (let index = 0; index < oldest.length - maximum; index += 1) {
        sessionPlayers.delete(oldest[index].userId);
    }
}

function normalizeUsername(value) {
    if (typeof value !== "string") return null;
    const username = value.trim().replace(/^@/, "");
    return /^[A-Za-z0-9._]{2,24}$/.test(username) ? username : null;
}

function nonEmpty(value, fallback = "") {
    return typeof value === "string" && value.trim() ? value.trim() : fallback;
}

function dedupeKey(event) {
    if (event.eventId) return `id:${event.eventId}`;
    return `fallback:${[
        event.type,
        event.userId,
        event.giftId,
        event.giftName,
        event.repeatCount,
        event.diamondCount,
        event.likeCount,
        event.comment,
    ]
        .map((value) => String(value ?? ""))
        .join("|")}`;
}

function isDuplicate(event) {
    const now = Date.now();
    const key = dedupeKey(event);
    const previous = recentEventIds.get(key);
    const retentionMs = event.eventId ? 10 * 60 * 1000 : 2500;
    if (previous && now - previous < retentionMs) return true;
    recentEventIds.set(key, now);
    if (recentEventIds.size > 2000) {
        for (const [id, timestamp] of recentEventIds) {
            if (now - timestamp > 10 * 60 * 1000) recentEventIds.delete(id);
        }
        while (recentEventIds.size > 2000)
            recentEventIds.delete(recentEventIds.keys().next().value);
    }
    return false;
}

function resolveGiftRule(event) {
    const exact = giftConfig.byGiftId[String(event.giftId)];
    if (exact) return exact;
    const giftName = normalizeGiftName(event.giftName);
    const named = (giftConfig.byGiftName || []).find((rule) =>
        (rule.aliases || []).some(
            (alias) => normalizeGiftName(alias) === giftName,
        ),
    );
    if (named && (!event.giftId || event.giftId.startsWith("demo-")))
        return named;
    const value = Number(event.diamondCount) || 0;
    return [...giftConfig.diamondBands]
        .sort((a, b) => b.minimum - a.minimum)
        .find((rule) => value >= rule.minimum);
}

function normalizeGiftName(value) {
    return String(value || "")
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .trim()
        .toLocaleLowerCase("vi");
}

function emitGameEvent(event) {
    if (isDuplicate(event)) return;
    const now = Date.now();
    const liveLike =
        event.type === "like" &&
        ((connectionStatus.state === "demo" &&
            String(event.eventId || "").startsWith("demo-")) ||
            (connectionStatus.state === "connected" &&
                !String(event.eventId || "").startsWith("demo-")));
    if (event.userId) {
        let hearts = sessionPersonalHearts.get(event.userId) || 0;
        if (liveLike && event.likeCount > 0) {
            hearts += event.likeCount;
            sessionPersonalHearts.set(event.userId, hearts);
        }
        event.personalHearts = hearts;
    }
    let giftRule = null;
    if (event.type === "gift") {
        giftRule =
            !event.masterRuleId &&
            String(event.eventId || "").startsWith("demo-")
                ? resolveGiftRule(event)
                : null;
        if (giftRule) {
            event.action = giftRule.action;
            event.durationMs = giftRule.durationMs;
            event.label = giftRule.label;
            event.variant = giftRule.variant;
            event.fireworkBursts = giftRule.fireworkBursts;
        }
    }
    if (event.userId && !event.spectatorOnly) {
        const previous = sessionPlayers.get(event.userId) || {};
        const playerState = {
            ...previous,
            userId: event.userId,
            uniqueId: nonEmpty(
                event.uniqueId,
                previous.uniqueId || event.userId,
            ),
            nickname: nonEmpty(
                event.nickname,
                previous.nickname || event.uniqueId || "TikTok user",
            ),
            avatar: nonEmpty(event.avatar, previous.avatar || ""),
            lastActive: now,
            personalHearts: event.personalHearts,
        };
        if (
            event.action === "wing" ||
            event.action === "medal" ||
            event.action === "vip_set"
        )
            playerState.hasGiftWing = true;
        if (event.action === "avatar_frame" || event.action === "vip_set")
            playerState.hasGiftAvatarFrame = true;
        if (event.type === "gift") {
            playerState.giftPower =
                Math.max(0, Number(previous.giftPower) || 0) +
                Math.max(0, Number(event.diamondCount) || 0);
            if (event.action === "medal" || event.action === "vip_set") {
                playerState.titleLabel = event.label || "KHÁCH VIP";
                playerState.titleVariant = event.variant || "fire";
                playerState.titleExpiresAt = 0;
            }
        }
        sessionPlayers.set(event.userId, playerState);
        pruneSessionPlayers(now);
    }
    metrics.events += 1;
    if (event.type === "member") metrics.members += 1;
    if (event.type === "chat") metrics.chats += 1;
    if (event.type === "gift") {
        metrics.gifts += 1;
        metrics.diamonds += event.diamondCount;
        const vip = sessionVipScores.get(event.userId) || {
            userId: event.userId,
            nickname: event.nickname,
            avatar: event.avatar,
            score: 0,
        };
        vip.nickname = nonEmpty(event.nickname, vip.nickname || "TikTok user");
        vip.avatar = nonEmpty(event.avatar, vip.avatar || "");
        vip.score += event.diamondCount;
        sessionVipScores.set(event.userId, vip);
    }
    if (event.type === "like") {
        metrics.likes += event.likeCount;
        if (liveLike)
            metrics.liveHearts = (metrics.liveHearts || 0) + event.likeCount;
    }
    broadcast(event);
    broadcastMetrics();
}

function processGameEvent(inputEvent) {
    if (inputEvent.type === "gift" && inputEvent.giftId) {
        const metadata =
            roomGifts.get(String(inputEvent.giftId)) ||
            observedGifts.get(String(inputEvent.giftId));
        if (metadata)
            inputEvent = {
                ...inputEvent,
                giftName:
                    inputEvent.giftName && inputEvent.giftName !== "Gift"
                        ? inputEvent.giftName
                        : metadata.giftName,
                giftPictureUrl:
                    inputEvent.giftPictureUrl || metadata.giftPictureUrl,
            };
    }
    const safeEvent = sanitizeGameEvent(inputEvent);
    if (!safeEvent) return;
    const event = applyBuiltInChatCommand(
        applyRule(safeEvent, resolveMasterRule(masterConfig, safeEvent)),
    );
    const isKnownPlayer = event.userId && sessionPlayers.has(event.userId);
    const joinsByKeyword = event.action === "join";
    if (!isKnownPlayer) {
        const isInteraction = [
            "chat",
            "gift",
            "like",
            "follow",
            "share",
        ].includes(event.type);
        const joinsByGift =
            event.type === "gift" && masterConfig.giftAlwaysJoins;
        event.spectatorOnly =
            !isInteraction ||
            !(
                masterConfig.joinMode === "all_interactions" ||
                joinsByKeyword ||
                joinsByGift
            );
    }
    event.joinedNow = Boolean(
        event.userId && !isKnownPlayer && !event.spectatorOnly,
    );
    if (event.type === "gift") {
        if (isRealObservedGift(event)) {
            learnObservedGift(event);
            broadcast({
                type: "gift_observed",
                giftId: event.giftId,
                giftName: event.giftName,
                diamondCount:
                    event.unitDiamondCount ||
                    Math.round(
                        event.diamondCount /
                            Math.max(1, event.repeatCount || 1),
                    ),
                giftPictureUrl: event.giftPictureUrl || "",
            });
        }
    }
    emitGameEvent(event);
}

function stopDemo() {
    if (demoTimer) clearInterval(demoTimer);
    demoTimer = null;
    for (const timeout of demoTimeouts) clearTimeout(timeout);
    demoTimeouts.clear();
}

function mockUser(index, manualName = null) {
    return {
        userId: manualName ? manualName : `demo-${index}`,
        uniqueId: manualName ? manualName : `dancer_${index}`,
        nickname: manualName ? manualName : `Dancer ${index}`,
        avatar: "",
    };
}

function emitDemoMember(index, manualName = null) {
    processGameEvent({
        type: "member",
        action: "join",
        joinedNow: true,
        eventId: `demo-join-${Date.now()}-${Math.random()}`,
        ...mockUser(index, manualName),
    });
}

function emitDemoAction(
    action,
    userIndex = 1,
    value = 1,
    giftName = "",
    manualName = null,
) {
    const user = mockUser(userIndex, manualName);
    const eventId = `demo-${action}-${Date.now()}-${Math.random()}`;
    if (action === "member") return emitDemoMember(userIndex, manualName);
    if (action === "dance") {
        return processGameEvent({
            type: "chat",
            eventId,
            ...user,
            comment: "dance",
        });
    }
    if (action === "jump") {
        return processGameEvent({
            type: "chat",
            eventId,
            ...user,
            comment: "jump",
        });
    }
    if (action === "walk") {
        return processGameEvent({
            type: "chat",
            eventId,
            ...user,
            comment: "đi vòng",
        });
    }
    if (action === "change") {
        return processGameEvent({
            type: "chat",
            eventId,
            ...user,
            comment: "đổi nv",
        });
    }
    if (action === "like") {
        return processGameEvent({
            type: "like",
            eventId,
            ...user,
            likeCount: value,
        });
    }
    if (action === "follow" || action === "share") {
        return processGameEvent({ type: action, eventId, ...user });
    }
    if (action === "gift") {
        const demoGiftNames = {
            1: "Rose",
            5: "Dance Pop",
            20: "Camera Star",
            50: "Fire Crown",
            100: "VIP Gift",
            200: "Super VIP",
            500: "Firework Rain",
            1000: "Party Universe",
        };
        return processGameEvent({
            type: "gift",
            eventId,
            ...user,
            giftId: `demo-${value}`,
            giftName:
                giftName ||
                demoGiftNames[value] ||
                (value >= 100
                    ? "Fireworks"
                    : value >= 10
                      ? "VIP Gift"
                      : "Rose"),
            repeatCount: 1,
            diamondCount: value,
        });
    }
}

function startDemo(count) {
    stopDemo();
    resetSessionState("demo");
    setStatus("demo", null, `Chế độ demo: ${count} người`);
    broadcast({ type: "reset" });

    const viewers = Array.from({ length: count }, (_, index) => ({
        index: index + 1,
        arrivesAt: Date.now() + Math.random() * Math.min(15000, count * 140),
        nextActionAt: 0,
        combo: null,
        joined: false,
    }));
    const shownRules = new Set();
    let sequence = 0;
    let scanStart = 0;
    let nextFeatureAt = 0;
    const featured = (rule) =>
        rule.source === "gift" ||
        rule.source === "share" ||
        [
            "camera",
            "vip",
            "vip_dance",
            "topdj",
            "medal",
            "vip_set",
            "walk",
            "join",
            "orbit",
            "fireworks",
        ].includes(rule.action);
    const randomDelay = () =>
        Math.max(
            2000,
            -Math.log(1 - Math.random()) * Math.max(10000, count * 1500),
        );

    function sendInteraction(viewer, rule, comboCount = 1) {
        const gift = giftCatalogMessage().gifts.find(
            (item) => item.giftId === rule.giftId,
        );
        const trigger =
            rule.trigger
                .split(",")
                .find((value) => value.trim())
                ?.trim() || "";
        processGameEvent({
            ...mockUser(viewer.index),
            eventId: `demo-rule-${Date.now()}-${++sequence}`,
            type: rule.source,
            comment: rule.source === "chat" ? trigger : "",
            giftId: rule.giftId,
            giftName: gift?.giftName || trigger,
            giftPictureUrl: gift?.giftPictureUrl || "",
            unitDiamondCount: gift?.diamondCount ?? rule.displayDiamonds,
            diamondCount:
                rule.source === "gift"
                    ? (gift?.diamondCount ?? rule.displayDiamonds)
                    : 0,
            repeatCount: 1,
            comboCount: rule.source === "gift" ? comboCount : 0,
            likeCount:
                rule.source === "like" ? 1 + Math.floor(Math.random() * 25) : 0,
        });
    }

    demoTimer = setInterval(() => {
        const now = Date.now();
        const rules = masterConfig.rules.filter((rule) => rule.enabled);
        const available = new Set(rules.map((rule) => rule.id));
        for (const id of shownRules)
            if (!available.has(id)) shownRules.delete(id);
        if (rules.length && rules.every((rule) => shownRules.has(rule.id)))
            shownRules.clear();
        let budget = 12;
        for (let step = 0; step < viewers.length && budget > 0; step++) {
            const viewer = viewers[(scanStart + step) % viewers.length];
            if (now < viewer.arrivesAt) continue;
            if (!viewer.joined) {
                emitDemoMember(viewer.index);
                viewer.joined = true;
                viewer.nextActionAt = now + 1500 + Math.random() * 6000;
                budget--;
                continue;
            }
            if (now < viewer.nextActionAt) continue;
            if (viewer.combo) {
                const rule = rules.find(
                    (item) =>
                        item.id === viewer.combo.ruleId &&
                        item.source === "gift",
                );
                if (rule) {
                    sendInteraction(viewer, rule, ++viewer.combo.sent);
                    budget--;
                    if (viewer.combo.sent < viewer.combo.total) {
                        viewer.nextActionAt = now + 700 + Math.random() * 400;
                        continue;
                    }
                }
                viewer.combo = null;
                nextFeatureAt = Math.max(nextFeatureAt, now + 5000);
                viewer.nextActionAt = now + randomDelay();
                continue;
            }
            if (!rules.length) continue;
            const eligible = rules.filter(
                (rule) => !featured(rule) || now >= nextFeatureAt,
            );
            if (!eligible.length) {
                viewer.nextActionAt = now + 1000;
                continue;
            }
            const unseen = eligible.filter((rule) => !shownRules.has(rule.id));
            const pool = unseen.length ? unseen : eligible;
            const rule = pool[Math.floor(Math.random() * pool.length)];
            shownRules.add(rule.id);
            sendInteraction(viewer, rule);
            if (featured(rule))
                nextFeatureAt =
                    now +
                    Math.max(
                        6500,
                        Math.min(30000, rule.durationMs || 0) + 1500,
                    );
            budget--;
            if (rule.source === "gift" && Math.random() < 0.2) {
                viewer.combo = {
                    ruleId: rule.id,
                    sent: 1,
                    total: 2 + Math.floor(Math.random() * 4),
                };
                nextFeatureAt = Math.max(
                    nextFeatureAt,
                    now + viewer.combo.total * 1100 + 5000,
                );
                viewer.nextActionAt = now + 700 + Math.random() * 400;
            } else viewer.nextActionAt = now + randomDelay();
        }
        scanStart = (scanStart + 13) % viewers.length;
    }, 200);
}

async function disconnectCurrentConnection() {
    const previous = liveConnection;
    liveConnection = null;
    if (giftCatalogState !== "saved") {
        giftCatalogState = "saved";
        roomGifts.clear();
        broadcast(giftCatalogMessage());
        broadcast(viewerGuideMessage());
    }
    if (!previous) return;
    try {
        if (typeof previous.disconnect === "function")
            await previous.disconnect();
        else if (typeof previous.close === "function") previous.close();
    } catch (error) {
        console.warn("Không thể đóng kết nối TikTok cũ:", error.message);
    }
}

function cancelReconnect() {
    if (reconnectTimer) clearTimeout(reconnectTimer);
    reconnectTimer = null;
}

function scheduleReconnect(username) {
    if (!username || desiredUsername !== username || reconnectTimer) return;
    const delayMs = Math.min(30000, 2000 * 2 ** Math.min(reconnectFailures, 4));
    reconnectFailures += 1;
    setStatus(
        "reconnecting",
        username,
        LIVE_PROVIDER === "tikfinity"
            ? `Chưa thấy TikFinity Desktop. Tự thử lại sau ${Math.ceil(delayMs / 1000)} giây...`
            : `Mất kết nối TikTok. Tự kết nối lại sau ${Math.ceil(delayMs / 1000)} giây...`,
    );
    reconnectTimer = setTimeout(() => {
        reconnectTimer = null;
        if (desiredUsername === username) {
            void connectToLiveProvider(username, {
                resetSession: false,
                isReconnect: true,
            });
        }
    }, delayMs);
}

async function connectToTikFinity(username, options = {}) {
    const resetSession = options.resetSession !== false;
    const isReconnect = options.isReconnect === true;
    stopDemo();
    cancelReconnect();
    desiredUsername = username;
    const attempt = ++connectionAttempt;
    await disconnectCurrentConnection();

    if (resetSession) {
        resetSessionState("tiktok");
        broadcast({ type: "reset" });
        broadcastMetrics();
    }
    setStatus(
        isReconnect ? "reconnecting" : "connecting",
        username,
        isReconnect
            ? "Đang kết nối lại TikFinity Desktop..."
            : "Đang kết nối TikFinity Desktop...",
    );

    const connection = new WebSocket(TIKFINITY_WS_URL);
    liveConnection = connection;
    const active = () =>
        attempt === connectionAttempt && liveConnection === connection;

    connection.on("open", () => {
        if (!active()) return;
        reconnectFailures = 0;
        setStatus(
            "connected",
            username,
            `Đã kết nối TikFinity cho @${username}`,
        );
        broadcastMetrics();
        console.log(`Đã kết nối TikFinity tại ${TIKFINITY_WS_URL}`);
    });
    connection.on("message", (payload) => {
        if (!active()) return;
        for (const event of normalizeTikFinityMessage(payload))
            processGameEvent(event);
    });
    connection.on("error", (error) => {
        console.warn(`TikFinity WebSocket: ${error.message}`);
    });
    connection.on("close", () => {
        if (!active()) return;
        liveConnection = null;
        scheduleReconnect(username);
    });
}

function connectToLiveProvider(username, options = {}) {
    return LIVE_PROVIDER === "tikfinity"
        ? connectToTikFinity(username, options)
        : connectToTikTok(username, options);
}

function attachTikTokEvents(connection) {
    const active = () => liveConnection === connection;
    const changingStreaks = new Map();
    // EventEmitter treats an unhandled `error` event as fatal. Keep the bridge
    // alive and let connect/reconnect report the connection state instead.
    connection.on("error", (error) => {
        console.warn("TikTok connection error:", error?.message || error);
    });
    connection.on(
        "member",
        (data) => active() && processGameEvent(normalizeMember(data)),
    );
    connection.on(
        "chat",
        (data) => active() && processGameEvent(normalizeChat(data)),
    );
    connection.on(
        "like",
        (data) => active() && processGameEvent(normalizeLike(data)),
    );
    connection.on(
        "follow",
        (data) => active() && processGameEvent(normalizeSocial("follow", data)),
    );
    connection.on(
        "share",
        (data) => active() && processGameEvent(normalizeSocial("share", data)),
    );
    connection.on("gift", (data) => {
        if (!active()) return;
        const event = normalizeGift(data);
        const rule = resolveMasterRule(masterConfig, event);
        if (
            event.giftType === 1 &&
            ["change", "wing", "avatar_frame", "medal", "vip_set"].includes(
                rule?.action,
            )
        ) {
            const key = event.userId + ":" + event.giftId;
            const previous = changingStreaks.get(key);
            const newStreak =
                previous?.ended &&
                (!event.repeatEnd ||
                    (event.eventId && event.eventId !== previous.eventId));
            const count =
                previous && !newStreak && event.repeatCount >= previous.count
                    ? previous.count
                    : 0;
            const delta = Math.max(0, event.repeatCount - count);
            changingStreaks.delete(key);
            changingStreaks.set(key, {
                count: event.repeatCount,
                ended: event.repeatEnd,
                eventId: event.eventId,
            });
            if (changingStreaks.size > 2000)
                changingStreaks.delete(changingStreaks.keys().next().value);
            if (delta > 0)
                processGameEvent({
                    ...event,
                    repeatCount: delta,
                    comboCount: event.repeatCount,
                    diamondCount: event.unitDiamondCount * delta,
                    eventId: event.eventId
                        ? event.eventId + ":" + event.repeatCount
                        : "",
                });
            return;
        }
        if (!isPendingGiftStreak(event)) processGameEvent(event);
    });
}

async function connectToTikTok(username, options = {}) {
    const resetSession = options.resetSession !== false;
    const isReconnect = options.isReconnect === true;
    stopDemo();
    cancelReconnect();
    desiredUsername = username;
    const attempt = ++connectionAttempt;
    await disconnectCurrentConnection();

    if (resetSession) {
        resetSessionState("tiktok");
        broadcast({ type: "reset" });
        broadcastMetrics();
    }
    setStatus(
        isReconnect ? "reconnecting" : "connecting",
        username,
        isReconnect
            ? `Đang kết nối lại @${username}...`
            : `Đang kết nối @${username}...`,
    );
    let connection;
    try {
        const { TikTokLiveConnection } = await import("tiktok-live-connector");
        if (attempt !== connectionAttempt) return;
        connection = new TikTokLiveConnection(username, {
            signApiKey: process.env.EULER_API_KEY || undefined,
            processInitialData: false,
            enableExtendedGiftInfo: false,
            webClientOptions: {
                searchParams: {
                    app_language: "vi",
                    browser_language: "vi-VN",
                    region: "VN",
                    priority_region: "VN",
                },
            },
        });
    } catch (error) {
        liveConnection = null;
        console.error(`Không thể tạo kết nối @${username}:`, error.message);
        setStatus(
            "error",
            username,
            `Không thể khởi tạo kết nối TikTok: ${error.message}`,
        );
        return;
    }
    liveConnection = connection;
    attachTikTokEvents(connection);

    connection.on("streamEnd", () => {
        if (liveConnection !== connection) return;
        liveConnection = null;
        desiredUsername = null;
        cancelReconnect();
        setStatus("ended", username, `Live @${username} đã kết thúc`);
    });
    connection.on("disconnected", () => {
        if (liveConnection !== connection) return;
        liveConnection = null;
        scheduleReconnect(username);
    });

    try {
        const state = await connection.connect();
        if (attempt !== connectionAttempt || liveConnection !== connection) {
            await connection.disconnect().catch(() => {});
            return;
        }
        reconnectFailures = 0;
        setStatus("connected", username, `Đã kết nối @${username}`);
        broadcastMetrics();
        console.log(`Đã kết nối @${username}, room ${state.roomId}`);
        void refreshRoomGifts(connection);
    } catch (error) {
        if (attempt !== connectionAttempt) return;
        liveConnection = null;
        console.error(`Không thể kết nối @${username}:`, error.message);
        if (desiredUsername === username) scheduleReconnect(username);
    }
}

const pendingTestCommands = new Map();
let nextTestCommand = 0;

function sendTestCommand(event, reply) {
    const game = [...wss.clients].find(
        (client) =>
            client.nativeClient &&
            client.registered &&
            client.role === "overlay" &&
            client.readyState === WebSocket.OPEN,
    );
    if (!game) return reply("Mở game rồi thử lại.", true);
    const testCommandId = String(++nextTestCommand);
    const timeout = setTimeout(() => {
        pendingTestCommands.delete(testCommandId);
        reply("Game chưa phản hồi. Thử lại.", true);
    }, 5000);
    pendingTestCommands.set(testCommandId, { game, reply, timeout });
    send(game, { ...event, testCommandId });
}

async function handleClientMessage(ws, message) {
    if (message.type === "ping") {
        return send(ws, { type: "pong", timestamp: Date.now() });
    }

    if (message.type === "register") {
        if (ws.registered)
            return send(ws, {
                type: "error",
                message: "Client đã đăng ký quyền.",
            });
        if (message.role !== "control" && message.role !== "overlay") {
            return send(ws, {
                type: "error",
                message: "Vai trò client không hợp lệ.",
            });
        }
        ws.role = message.role;
        ws.registered = true;
        send(ws, { type: "config", game: gameConfig, gifts: giftConfig });
        send(ws, { type: "status", ...connectionStatus });
        send(ws, { type: "metrics", ...metrics });
        if (ws.role === "control")
            send(ws, { type: "master_config", master: masterConfig });
        if (ws.role === "control") {
            send(ws, giftCatalogMessage());
            send(ws, viewerGuideMessage());
        }
        if (ws.role === "overlay") {
            send(ws, createSnapshot());
            send(ws, viewerGuideMessage());
        }
        return;
    }

    if (!ws.registered)
        return send(ws, {
            type: "error",
            message: "Client chưa đăng ký quyền.",
        });

    const controlOnly = [
        "master_save",
        "master_test",
        "master_test_spawn",
        "gift_catalog_refresh",
    ].includes(message.type);
    const operatorOnly = new Set([
        "set_username",
        "disconnect_tiktok",
        "demo_start",
        "demo_stop",
        "demo_event",
        "reset_game",
    ]).has(message.type);
    if (controlOnly && ws.role !== "control") {
        return send(ws, {
            type: "error",
            message: "Lệnh này chỉ dành cho bảng Master.",
        });
    }
    if (operatorOnly && ws.role !== "control" && !ws.nativeClient) {
        return send(ws, {
            type: "error",
            message: "Client không có quyền điều khiển.",
        });
    }

    if (message.type === "gift_catalog_refresh") {
        const reply = (message) =>
            send(ws, { type: "gift_catalog_refresh_result", message });
        if (
            connectionStatus.state !== "connected" ||
            typeof liveConnection?.fetchAvailableGifts !== "function"
        )
            return reply(
                "Kết nối TikTok LIVE trực tiếp để tải danh mục quà của phòng.",
            );
        if (giftCatalogState === "loading")
            return reply("Danh mục phòng đang được tải.");
        await refreshRoomGifts(liveConnection);
        return reply(
            giftCatalogState === "failed"
                ? "TikTok chưa trả danh mục quà. Đang hiện thư viện đã lưu."
                : "",
        );
    }

    if (message.type === "master_save") {
        if (!Array.isArray(message.master?.rules))
            return send(ws, {
                type: "master_saved",
                error: true,
                message: "Cấu hình không hợp lệ.",
            });
        const nextConfig = sanitizeMasterConfig(message.master);
        if (
            nextConfig.rules.filter(
                (rule) =>
                    rule.enabled &&
                    rule.source === "like" &&
                    rule.action === "level_up",
            ).length > 1
        )
            return send(ws, {
                type: "master_saved",
                error: true,
                message: "Chỉ bật một luật Tăng cấp cá nhân.",
            });
        const ids = new Set();
        for (const rule of nextConfig.rules) {
            if (!rule.enabled || rule.source !== "gift") continue;
            const existing = masterConfig.rules.find(
                (item) => item.id === rule.id,
            );
            const legacy =
                !rule.giftId &&
                existing?.enabled &&
                existing.source === "gift" &&
                !existing.giftId &&
                existing.trigger === rule.trigger &&
                existing.action === rule.action &&
                existing.match === rule.match;
            if (legacy) continue;
            if (!/^\d+$/.test(rule.giftId))
                return send(ws, {
                    type: "master_saved",
                    error: true,
                    message: `Chọn Gift ID TikTok cho luật “${rule.trigger || rule.id}”.`,
                });
            if (ids.has(rule.giftId))
                return send(ws, {
                    type: "master_saved",
                    error: true,
                    message: `Gift ID #${rule.giftId} đang có hai luật bật. Chỉ bật một luật cho mỗi quà.`,
                });
            ids.add(rule.giftId);
        }
        pendingMasterSaves++;
        try {
            await persistMaster(nextConfig);
        } catch (error) {
            console.error("Cannot save interaction rules:", error);
            return send(ws, {
                type: "master_saved",
                error: true,
                message: "Lưu thất bại. Bản đang sửa vẫn còn.",
            });
        } finally {
            pendingMasterSaves--;
        }
        masterConfig = nextConfig;
        broadcast({ type: "master_config", master: masterConfig });
        broadcast(viewerGuideMessage());
        return send(ws, {
            type: "master_saved",
            master: masterConfig,
            message: "Đã lưu và áp dụng.",
        });
    }

    if (message.type === "master_test_ack") {
        const pending = pendingTestCommands.get(message.testCommandId);
        if (!pending || pending.game !== ws) return;
        clearTimeout(pending.timeout);
        pendingTestCommands.delete(message.testCommandId);
        const missing = message.testActorMissing === true;
        return pending.reply(
            missing ? "Bấm Thêm nhân vật test trước." : null,
            missing,
        );
    }

    if (message.type === "master_test_spawn") {
        return sendTestCommand(
            { type: "master_test_spawn" },
            (text, error = false) =>
                send(ws, {
                    type: "master_test_spawn_result",
                    message: text || "Nhân vật test đã có mặt trong game.",
                    error,
                }),
        );
    }

    if (message.type === "master_test") {
        const config = message.master
            ? sanitizeMasterConfig(message.master)
            : masterConfig;
        const rule = config.rules.find(
            (item) => item.id === message.ruleId && item.enabled,
        );
        const result = (text, error = false) =>
            send(ws, { type: "master_test_result", message: text, error });
        if (!rule) return result("Hãy bật luật trước khi thử.", true);
        const trigger = rule.trigger
            .split(",")
            .find((value) => value.trim())
            ?.trim();
        const catalog = giftCatalogMessage().gifts;
        const catalogGift =
            typeof message.giftId === "string"
                ? catalog.find((gift) => gift.giftId === message.giftId)
                : null;
        if (message.giftId && !catalogGift)
            return result("Quà này chưa có trong thư viện.", true);
        const learned =
            catalogGift ||
            catalog.find((gift) => gift.giftId === rule.giftId && rule.giftId);
        if (
            !trigger &&
            (rule.source === "chat" || (rule.source === "gift" && !rule.giftId))
        )
            return result(
                "Nhập từ khóa, tên quà hoặc ID quà trước khi thử.",
                true,
            );
        const repeatCount =
            rule.source === "gift" ? Number(message.repeatCount ?? 1) : 1;
        if (
            !Number.isInteger(repeatCount) ||
            repeatCount < 1 ||
            repeatCount > 100
        )
            return result("Nhập số lượng từ 1 đến 100.", true);
        const sample = sanitizeGameEvent({
            type: rule.source,
            userId: "master-test",
            uniqueId: "master_test",
            nickname: "Thử tương tác",
            comment: rule.source === "chat" ? trigger : "",
            giftId: learned?.giftId || rule.giftId,
            giftName:
                rule.source === "gift"
                    ? learned?.giftName || trigger || "Quà thử"
                    : "",
            repeatCount,
            diamondCount:
                (learned?.diamondCount ?? (rule.displayDiamonds || 1)) *
                repeatCount,
        });
        const matched =
            rule.source === "like" ? rule : resolveMasterRule(config, sample);
        if (matched?.id !== rule.id)
            return result(
                "Luật khác được ưu tiên cho điều kiện này. Kiểm tra ID quà hoặc thứ tự luật.",
                true,
            );
        const preview = {
            ...applyRule(sample, matched),
            comboCount: repeatCount,
            preview: true,
            joinedNow: matched.action === "join",
        };
        return sendTestCommand(preview, (text, error) =>
            result(text || "Đã chạy luật trên nhân vật test.", error),
        );
    }

    if (message.type === "set_username") {
        const username = normalizeUsername(message.username);
        if (!username) {
            return send(ws, {
                type: "error",
                message:
                    "Username chỉ được gồm chữ, số, dấu chấm hoặc gạch dưới.",
            });
        }
        return connectToLiveProvider(username);
    }

    if (message.type === "disconnect_tiktok") {
        desiredUsername = null;
        reconnectFailures = 0;
        cancelReconnect();
        connectionAttempt += 1;
        await disconnectCurrentConnection();
        setStatus("idle", null, "Đã ngắt kết nối");
        return;
    }

    if (message.type === "demo_start") {
        desiredUsername = null;
        reconnectFailures = 0;
        cancelReconnect();
        connectionAttempt += 1;
        await disconnectCurrentConnection();
        const count = Math.min(
            gameConfig.maxPlayers,
            Math.max(1, Number(message.count) || 20),
        );
        return startDemo(count);
    }
    if (message.type === "demo_stop") {
        stopDemo();
        setStatus("idle", null, "Đã dừng demo");
        return;
    }
    if (message.type === "demo_event") {
        return emitDemoAction(
            String(message.action || "dance"),
            Math.max(1, Number(message.userIndex) || 1),
            Math.max(1, Number(message.value) || 1),
            String(message.giftName || ""),
            message.manualUsername || null,
        );
    }
    if (message.type === "reset_game") {
        const source = metrics.source;
        const startedAt = metrics.startedAt;
        resetSessionState(source);
        metrics.startedAt = startedAt;
        broadcast({ type: "reset" });
        broadcastMetrics();
        return;
    }

    send(ws, { type: "error", message: "Lệnh không được hỗ trợ." });
}

function rejectUpgrade(socket, status = "403 Forbidden") {
    socket.write(
        `HTTP/1.1 ${status}\r\nConnection: close\r\nContent-Length: 0\r\n\r\n`,
    );
    socket.destroy();
}

server.on("upgrade", (request, socket, head) => {
    const remoteAddress = request.socket.remoteAddress;
    const origin = request.headers.origin || "";
    if (
        (!ALLOW_LAN && !isLoopbackAddress(remoteAddress)) ||
        !isAllowedHost(request.headers.host, PORT, ALLOW_LAN) ||
        !isAllowedOrigin(origin, PORT)
    ) {
        return rejectUpgrade(socket);
    }
    wss.handleUpgrade(request, socket, head, (ws) =>
        wss.emit("connection", ws, request),
    );
});

wss.on("connection", (ws, request) => {
    ws.registered = false;
    ws.role = null;
    ws.nativeClient =
        !request.headers.origin &&
        isLoopbackAddress(request.socket.remoteAddress);
    ws.rateLimit = { windowStartedAt: Date.now(), count: 0 };
    ws.invalidMessages = 0;
    ws.isAlive = true;
    ws.on("pong", () => {
        ws.isAlive = true;
    });
    send(ws, { type: "status", ...connectionStatus });
    ws.on("message", (rawMessage, isBinary) => {
        if (isBinary) return ws.close(1003, "Text messages only");
        if (!consumeRateLimit(ws.rateLimit))
            return ws.close(1008, "Rate limit exceeded");
        let message;
        try {
            message = JSON.parse(rawMessage.toString());
        } catch {
            ws.invalidMessages += 1;
            if (ws.invalidMessages >= 3)
                return ws.close(1008, "Invalid messages");
            return send(ws, { type: "error", message: "Dữ liệu không hợp lệ" });
        }
        if (
            !message ||
            typeof message !== "object" ||
            Array.isArray(message) ||
            typeof message.type !== "string" ||
            message.type.length > 40
        ) {
            return send(ws, {
                type: "error",
                message: "Cấu trúc lệnh không hợp lệ.",
            });
        }
        void handleClientMessage(ws, message).catch((error) => {
            console.error(
                "Không thể xử lý lệnh WebSocket:",
                error?.message || error,
            );
            send(ws, { type: "error", message: "Không thể xử lý lệnh." });
        });
    });
    ws.on("error", (error) =>
        console.warn("WebSocket client error:", error.message),
    );
});

const heartbeatTimer = setInterval(() => {
    for (const ws of wss.clients) {
        if (!ws.isAlive) {
            ws.terminate();
            continue;
        }
        ws.isAlive = false;
        ws.ping();
    }
}, 30000);
heartbeatTimer.unref();

server.requestTimeout = 10000;
server.headersTimeout = 5000;
server.keepAliveTimeout = 5000;
server.maxHeadersCount = 64;

server.listen(PORT, HOST, () => {
    console.log(`TikTok Live Game: http://${HOST}:${PORT}`);
    console.log(`Bảng điều khiển: http://${HOST}:${PORT}/control.html`);
});

server.on("error", (err) => {
    if (err.code === "EADDRINUSE") {
        console.error(
            `\n❌ Lỗi: Cổng ${PORT} đang bị chương trình khác chiếm.`,
        );
        console.error(`   Có thể bạn đã chạy server trước đó mà chưa tắt.\n`);
        console.error(`   Cách khắc phục:`);
        console.error(`   1. Tắt cửa sổ cmd/terminal cũ đang chạy server`);
        console.error(`   2. Hoặc chạy lệnh: npx kill-port ${PORT}`);
        console.error(
            `   3. Nếu chỉ dùng Control Panel, có thể đổi PORT trong file .env.`,
        );
        console.error(`      Bản game dựng sẵn cần PORT=3000.\n`);
        process.exit(1);
    } else {
        console.error("Lỗi server:", err);
        process.exit(1);
    }
});

async function shutdown() {
    stopDemo();
    clearInterval(heartbeatTimer);
    desiredUsername = null;
    cancelReconnect();
    connectionAttempt += 1;
    await disconnectCurrentConnection();
    for (const ws of wss.clients) ws.terminate();
    wss.close();
    server.close(() => process.exit(0));
}

process.on("SIGINT", shutdown);
process.on("SIGTERM", shutdown);
