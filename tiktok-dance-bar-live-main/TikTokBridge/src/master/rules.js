const crypto = require("node:crypto");

const SOURCES = new Set(["chat", "gift", "share", "like"]);
const HEART_ACTIONS = new Set(["party", "fireworks", "orbit", "level_up"]);
const ACTIONS = new Set([
    "join",
    "jump",
    "dance",
    "camera",
    "change",
    "walk",
    "grow",
    "medal",
    "vip",
    "vip_dance",
    "vip_set",
    "topdj",
    "fireworks",
    "wing",
    "avatar_frame",
]);

function normalizeText(value) {
    return String(value || "")
        .normalize("NFD")
        .replace(/[\u0300-\u036f]/g, "")
        .trim()
        .toLocaleLowerCase("vi")
        .replace(/\u0111/g, "d");
}

function splitTriggers(value) {
    return String(value || "")
        .split(",")
        .map(normalizeText)
        .filter(Boolean)
        .slice(0, 20);
}

function sanitizeRule(input) {
    input = input && typeof input === "object" ? input : {};
    return {
        id:
            String(input.id || "")
                .replace(/[^a-zA-Z0-9_-]/g, "")
                .slice(0, 64) || crypto.randomUUID(),
        enabled: input.enabled !== false,
        source: SOURCES.has(input.source) ? input.source : "gift",
        trigger: String(input.trigger || "")
            .trim()
            .slice(0, 300),
        giftId: String(input.giftId || "")
            .trim()
            .slice(0, 80),
        match: input.match === "contains" ? "contains" : "exact",
        action:
            input.source === "like"
                ? HEART_ACTIONS.has(input.action)
                    ? input.action
                    : "party"
                : input.action === "vip"
                  ? "camera"
                  : ACTIONS.has(input.action)
                    ? input.action
                    : "dance",
        heartGoal:
            input.source === "like"
                ? Math.round(
                      Math.max(
                          1,
                          Math.min(100000000, Number(input.heartGoal) || 1000),
                      ),
                  )
                : 0,
        displayDiamonds: Math.max(
            0,
            Math.min(1000000, Number(input.displayDiamonds) || 0),
        ),
        zoomDurationMs: Math.round(
            Math.max(
                1000,
                Math.min(12000, Number(input.zoomDurationMs) || 3500),
            ),
        ),
        durationMs: Math.round(
            Math.max(0, Math.min(300000, Number(input.durationMs) || 0)),
        ),
        label: String(input.label || "")
            .trim()
            .slice(0, 80),
        variant: ["aura", "neon", "fire", "royal"].includes(input.variant)
            ? input.variant
            : "",
        fireworkBursts: Math.round(
            Math.max(0, Math.min(12, Number(input.fireworkBursts) || 0)),
        ),
    };
}

function sanitizeMasterConfig(input) {
    input = input && typeof input === "object" ? input : {};
    return {
        joinMode:
            input.joinMode === "all_interactions"
                ? "all_interactions"
                : "keyword_only",
        giftAlwaysJoins: input.giftAlwaysJoins !== false,
        rules: Array.isArray(input.rules)
            ? input.rules.slice(0, 100).map(sanitizeRule)
            : [],
    };
}

function matchesRule(rule, event) {
    if (!rule.enabled || rule.source !== event.type) return false;
    if (event.type === "share") return true;
    if (event.type === "like") return false;
    if (event.type === "gift" && (rule.giftId || rule.match !== "exact"))
        return false;
    const value = normalizeText(
        rule.source === "chat" ? event.comment : event.giftName,
    );
    if (!value) return false;
    return splitTriggers(rule.trigger).some((trigger) =>
        rule.match === "contains" ? value.includes(trigger) : value === trigger,
    );
}

function resolveMasterRule(config, event) {
    const rules = config.rules || [];
    if (event.type === "gift" && event.giftId) {
        const byId = rules.find(
            (rule) =>
                rule.enabled &&
                rule.source === "gift" &&
                rule.giftId === String(event.giftId),
        );
        if (byId) return byId;
    }
    return rules.find((rule) => matchesRule(rule, event)) || null;
}

function applyRule(event, rule) {
    if (!rule) return { ...event };
    return {
        ...event,
        action: rule.action,
        heartGoal: rule.heartGoal,
        durationMs: rule.durationMs,
        zoomDurationMs: rule.zoomDurationMs,
        label: rule.label,
        variant: rule.variant,
        fireworkBursts: rule.fireworkBursts,
        masterRuleId: rule.id,
    };
}

function applyBuiltInChatCommand(event) {
    if (event.type !== "chat" || event.masterRuleId) return event;
    const command = normalizeText(event.comment);
    if (command !== "jump" && command !== "nhay") return event;
    return { ...event, action: "jump", durationMs: 950, masterRuleId: "" };
}

module.exports = {
    normalizeText,
    sanitizeMasterConfig,
    resolveMasterRule,
    applyRule,
    applyBuiltInChatCommand,
};
