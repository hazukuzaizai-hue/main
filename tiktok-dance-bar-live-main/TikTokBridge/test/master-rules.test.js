const test = require("node:test");
const assert = require("node:assert/strict");
const {
    sanitizeMasterConfig,
    resolveMasterRule,
    applyRule,
    applyBuiltInChatCommand,
} = require("../src/master/rules");

const master = sanitizeMasterConfig({
    joinMode: "keyword_only",
    giftAlwaysJoins: true,
    rules: [
        { id: "hey", source: "chat", trigger: "hey", action: "join" },
        {
            id: "rose",
            source: "gift",
            trigger: "Rose, Hoa hồng",
            action: "camera",
            durationMs: 4000,
        },
        {
            id: "rosa",
            source: "gift",
            giftId: "777",
            trigger: "Rosa",
            action: "medal",
            label: "CÁNH VIP",
        },
    ],
});

test("matches Vietnamese aliases without accents or case sensitivity", () => {
    const rule = resolveMasterRule(master, {
        type: "gift",
        giftName: "HOA HỒNG",
    });
    assert.equal(rule.id, "rose");
    const event = applyRule({ type: "gift", giftName: "HOA HỒNG" }, rule);
    assert.equal(event.action, "camera");
    assert.equal(event.durationMs, 4000);
});

test("gift id wins even when localized gift name changes", () => {
    const rule = resolveMasterRule(master, {
        type: "gift",
        giftId: "777",
        giftName: "Localized name",
    });
    assert.equal(rule.id, "rosa");
    assert.equal(applyRule({ type: "gift" }, rule).label, "CÁNH VIP");
});

test("gift id rule wins over an earlier matching name rule", () => {
    const config = sanitizeMasterConfig({
        rules: [
            {
                id: "generic-name",
                source: "gift",
                trigger: "Rose",
                action: "dance",
            },
            {
                id: "learned-id",
                source: "gift",
                trigger: "Rose",
                giftId: "5655",
                action: "camera",
            },
        ],
    });
    assert.equal(
        resolveMasterRule(config, {
            type: "gift",
            giftId: "5655",
            giftName: "Rose",
        }).id,
        "learned-id",
    );
});

test("chat hey maps to join", () => {
    const rule = resolveMasterRule(master, { type: "chat", comment: "hey" });
    assert.equal(rule.action, "join");
});

test("shares use the saved action and heart goals do not trigger on individual likes", () => {
    const config = sanitizeMasterConfig({
        rules: [
            { id: "share", source: "share", action: "jump" },
            {
                id: "milestone",
                source: "like",
                action: "orbit",
                heartGoal: 500,
            },
            { id: "level", source: "like", action: "level_up", heartGoal: 100 },
        ],
    });
    assert.equal(resolveMasterRule(config, { type: "share" }).action, "jump");
    assert.equal(config.rules[1].heartGoal, 500);
    assert.equal(config.rules[1].action, "orbit");
    assert.equal(
        applyRule({ type: "like" }, config.rules[2]).action,
        "level_up",
    );
    assert.equal(applyRule({ type: "like" }, config.rules[2]).heartGoal, 100);
    assert.equal(
        resolveMasterRule(config, { type: "like", likeCount: 500 }),
        null,
    );
});

test("a known gift ID cannot trigger a different ID's rule through its name", () => {
    assert.equal(
        resolveMasterRule(master, {
            type: "gift",
            giftId: "999",
            giftName: "Rosa",
        }),
        null,
    );
    assert.equal(
        resolveMasterRule(master, {
            type: "gift",
            giftName: "Rosa",
        }),
        null,
    );
    for (const action of ["wing", "avatar_frame"]) {
        const config = sanitizeMasterConfig({
            rules: [{ source: "gift", giftId: "123", action }],
        });
        assert.equal(
            resolveMasterRule(config, { type: "gift", giftId: "123" }).action,
            action,
        );
    }
});

test("jump and nhảy comments trigger a short built-in jump without a gift rule", () => {
    assert.equal(
        applyBuiltInChatCommand({ type: "chat", comment: "jump" }).action,
        "jump",
    );
    const event = applyBuiltInChatCommand({ type: "chat", comment: "NHẢY" });
    assert.equal(event.action, "jump");
    assert.equal(event.durationMs, 950);
    assert.equal(
        applyBuiltInChatCommand({ type: "gift", giftName: "Jump" }).action,
        undefined,
    );
});

test("explicit interaction rules take priority over the built-in jump command", () => {
    const config = sanitizeMasterConfig({
        rules: [
            { id: "custom", source: "chat", trigger: "NHẢY", action: "grow" },
        ],
    });
    const event = { type: "chat", comment: "nhay" };
    assert.equal(
        applyBuiltInChatCommand(
            applyRule(event, resolveMasterRule(config, event)),
        ).action,
        "grow",
    );
});

test("Vietnamese commands with đ match their unaccented spelling", () => {
    const config = sanitizeMasterConfig({
        rules: [{ id: "jump", source: "chat", trigger: "đổi", action: "jump" }],
    });
    assert.equal(
        resolveMasterRule(config, { type: "chat", comment: "DOI" }).action,
        "jump",
    );
});
