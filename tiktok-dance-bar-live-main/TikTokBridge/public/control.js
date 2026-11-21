/* ─────────────────────────────────────────────────────────
   Control Panel JS
───────────────────────────────────────────────────────── */

/* ═══ TAB NAVIGATION ════════════════════════════════════ */
document.querySelectorAll(".tab-btn").forEach((btn) => {
    btn.addEventListener("click", () => {
        document.querySelectorAll(".tab-btn").forEach((b) => {
            b.classList.remove("active");
            b.setAttribute("aria-selected", "false");
        });
        document
            .querySelectorAll(".tab-panel")
            .forEach((p) => p.classList.remove("active"));
        btn.classList.add("active");
        btn.setAttribute("aria-selected", "true");
        document
            .getElementById("tab-" + btn.dataset.tab)
            ?.classList.add("active");
    });
});

/* ═══ UI ELEMENT REFS ════════════════════════════════════ */
const ui = {
    // Navbar status
    statusDot: document.getElementById("status-dot"),
    statusTitle: document.getElementById("status-title"),
    // Card status (tab live)
    statusCardDot: document.getElementById("status-card-dot"),
    statusTitleCard: document.getElementById("status-title-card"),
    statusMessage: document.getElementById("status-message"),
    // Live tab
    username: document.getElementById("username"),
    connect: document.getElementById("connect"),
    disconnect: document.getElementById("disconnect"),
    // Test lab
    stopDemo: document.getElementById("stop-demo"),
    // Session
    reset: document.getElementById("reset"),
    // Events
    userIndex: document.getElementById("user-index"),
    // Rules
    joinMode: document.getElementById("join-mode"),
    giftAlwaysJoins: document.getElementById("gift-always-joins"),
    masterRules: document.getElementById("master-rules"),
    masterIndex: document.getElementById("master-index"),
    masterEditorEmpty: document.getElementById("master-editor-empty"),
    filterMaster: document.getElementById("filter-master"),
    masterEmpty: document.getElementById("master-empty"),
    masterRuleTemplate: document.getElementById("master-rule-template"),
    addMasterRule: document.getElementById("add-master-rule"),
    saveMaster: document.getElementById("save-master"),
    spawnTestActor: document.getElementById("spawn-test-actor"),
    testActorStatus: document.getElementById("test-actor-status"),
    masterMessage: document.getElementById("master-message"),
    recentGifts: document.getElementById("recent-gifts"),
    metrics: {
        events: document.getElementById("metric-events"),
        members: document.getElementById("metric-members"),
        chats: document.getElementById("metric-chats"),
        gifts: document.getElementById("metric-gifts"),
        diamonds: document.getElementById("metric-diamonds"),
        likes: document.getElementById("metric-likes"),
    },
};

/* ═══ STATE ══════════════════════════════════════════════ */
let socket;
let reconnectTimer;
let masterConfig = {
    joinMode: "keyword_only",
    giftAlwaysJoins: true,
    rules: [],
};
const recentGifts = new Map();
let ruleSource = "chat";
const sourceButtons = document.querySelectorAll("[data-rule-source]");
function selectSource(source) {
    ruleSource = source;
    for (const button of sourceButtons) {
        const active = button.dataset.ruleSource === source;
        button.setAttribute("aria-pressed", String(active));
        button.classList.toggle("btn-primary", active);
        button.classList.toggle("btn-secondary", !active);
    }
    ui.recentGifts.hidden = source !== "gift";
    document.getElementById("gift-catalog-status").hidden = source !== "gift";
    ui.filterMaster.placeholder =
        source === "gift"
            ? "Tìm quà, hiệu ứng, ID…"
            : source === "like"
              ? "Tìm mốc tim…"
              : "Tìm bình luận, chia sẻ…";
    ui.filterMaster.value = "";
    selectRule(
        [...ui.masterRules.children].find((row) => row.dataset.src === source),
        false,
    );
}
for (const button of sourceButtons)
    button.addEventListener("click", () =>
        selectSource(button.dataset.ruleSource),
    );
let activeRuleId;
let testingRule;
let testTimeout;
let masterLoaded = false;
let masterDirty = false;
let masterRevision = 0;
let savingRevision = null;
let savingHeartRules = [];
ui.saveMaster.disabled = true;
ui.addMasterRule.disabled = true;

/* ═══ WEBSOCKET ══════════════════════════════════════════ */
function send(message) {
    if (socket?.readyState === WebSocket.OPEN) {
        socket.send(JSON.stringify(message));
        return true;
    }
    return false;
}

function setStatus(data) {
    const state = data.state || "";
    ui.statusDot.className = "status-dot " + state;
    if (ui.statusCardDot)
        ui.statusCardDot.className = "status-card-dot " + state;

    const names = {
        idle: "Sẵn sàng",
        connecting: "Đang kết nối…",
        connected: "🔴 LIVE",
        demo: "🧪 DEMO",
        disconnected: "Mất kết nối",
        ended: "Live kết thúc",
        error: "Lỗi kết nối",
    };
    const label = names[state] || state;
    ui.statusTitle.textContent = label;
    if (ui.statusTitleCard) ui.statusTitleCard.textContent = label;
    if (ui.statusMessage) ui.statusMessage.textContent = data.message || "";
    ui.connect.disabled = state === "connecting";

    // Badge đỏ nhấp nháy khi đang LIVE thật
    const liveBadge = document.getElementById("live-badge");
    if (liveBadge) liveBadge.classList.toggle("is-live", state === "connected");
}

function setMetrics(data) {
    for (const [key, el] of Object.entries(ui.metrics)) {
        el.textContent = Number(data[key] || 0).toLocaleString("vi-VN");
    }
}

/* ═══ MASTER RULES ═══════════════════════════════════════ */
function makeRuleId() {
    return `rule-${Date.now()}-${Math.random().toString(36).slice(2, 8)}`;
}

function emptyRule(overrides = {}) {
    return {
        id: makeRuleId(),
        enabled: true,
        source: ruleSource,
        trigger: "",
        giftId: "",
        match: "exact",
        action: ruleSource === "like" ? "party" : "dance",
        heartGoal: 1000,
        displayDiamonds: 0,
        durationMs: 3000,
        label: "",
        variant: "",
        fireworkBursts: 0,
        ...overrides,
    };
}

function setMasterMessage(msg, error = false) {
    ui.masterMessage.textContent = msg || "";
    ui.masterMessage.classList.toggle("error", error);
}

function updateRuleFields(row) {
    const sourcePicker = row.querySelector('[data-field="source"]');
    const source = sourcePicker.value;
    const gift = source === "gift";
    row.querySelector("[data-test-quantity]").hidden = !gift;
    const heart = source === "like";
    const actionPicker = row.querySelector('[data-field="action"]');
    for (const option of actionPicker.options) {
        option.hidden = heart
            ? !option.hasAttribute("data-heart-action") &&
              option.value !== "fireworks"
            : option.hasAttribute("data-heart-action");
        option.disabled = option.hidden;
    }
    if (actionPicker.selectedOptions[0]?.disabled)
        actionPicker.value = heart ? "party" : "dance";
    const action = actionPicker.value;
    const enabled = row.querySelector('[data-field="enabled"]').checked;
    row.dataset.src = source === "share" ? "chat" : source;
    row.querySelector("[data-social-field]").hidden = gift || heart;
    for (const option of sourcePicker.options)
        option.hidden = option.value === "gift" || option.value === "like";
    row.querySelector("[data-heart-field]").hidden = !heart;
    row.querySelector("[data-heart-goal-label]").textContent =
        action === "level_up"
            ? "Số tim cá nhân để lên 1 cấp"
            : "Mốc tim của cả phòng";
    const durationField = row.querySelector("[data-duration-field]");
    durationField.hidden =
        [
            "join",
            "change",
            "wing",
            "avatar_frame",
            "medal",
            "vip_set",
            "level_up",
            "fireworks",
        ].includes(action) ||
        (gift && action === "camera");
    durationField.querySelector(".rule-field-label").textContent =
        action === "orbit"
            ? "Thời gian bay (giây)"
            : action === "topdj"
              ? "Thời gian làm DJ (giây)"
              : "Thời lượng (giây)";
    row.querySelector("[data-zoom-field]").hidden = action !== "topdj" && !gift;
    row.querySelector("[data-orbit-duration-help]").hidden = action !== "orbit";
    row.querySelector("[data-trigger-field]").hidden =
        heart || source === "share";
    row.dataset.enabled = enabled;
    row.querySelector("[data-trigger-label]").textContent = gift
        ? "Tên quà"
        : "Lệnh bình luận";
    row.querySelectorAll("[data-gift-field]").forEach((field) => {
        field.hidden = !gift;
    });
    row.querySelector("[data-chat-field]").hidden = source !== "chat";
    row.querySelectorAll("[data-title-field]").forEach((field) => {
        field.hidden = !["medal", "vip_set"].includes(action);
    });
    row.querySelector("[data-firework-field]").hidden = ![
        "fireworks",
        "topdj",
    ].includes(action);
    row.querySelector('[data-command="test"]').disabled =
        !enabled || !!testingRule;
    const giftId =
        gift && row.querySelector('[data-field="giftId"]').value.trim();
    const picker = row.querySelector("[data-gift-picker]");
    picker.textContent =
        recentGifts.get(giftId)?.giftName ||
        (giftId && row.querySelector('[data-field="trigger"]').value) ||
        "Chọn từ thư viện quà";
    const preview = row.querySelector("[data-gift-preview]");
    const image = recentGifts.get(giftId)?.giftPictureUrl;
    preview.hidden = !image;
    if (image && preview.getAttribute("src") !== image) preview.src = image;
}

function setRuleTestStatus(row, message, error = false) {
    const status = row.querySelector("[data-rule-test-result]");
    status.textContent = message;
    status.classList.toggle("error", error);
}

function finishRuleTest(message, error) {
    if (!testingRule) return;
    setRuleTestStatus(testingRule, message, error);
    testingRule = null;
    clearTimeout(testTimeout);
    for (const row of ui.masterRules.children) updateRuleFields(row);
}

function updateRuleList() {
    const rows = [...ui.masterRules.children].filter(
        (row) => row.dataset.src === ruleSource,
    );
    const query = ui.filterMaster.value.trim().toLocaleLowerCase("vi");
    const selected = rows.find((row) => row.dataset.ruleId === activeRuleId);
    const buttons = [];
    for (const row of ui.masterRules.children) row.hidden = row !== selected;
    rows.forEach((row, index) => {
        row.querySelector('[data-command="up"]').disabled = index === 0;
        row.querySelector('[data-command="down"]').disabled =
            index === rows.length - 1;
        const source = row.querySelector('[data-field="source"]').value;
        const trigger =
            source === "share"
                ? "Chia sẻ LIVE"
                : source === "like"
                  ? `${Number(row.querySelector('[data-field="heartGoal"]').value).toLocaleString("vi-VN")} tim`
                  : row.querySelector('[data-field="trigger"]').value.trim();
        const gift = row.dataset.src === "gift";
        const giftId = gift
            ? row.querySelector('[data-field="giftId"]').value.trim()
            : "";
        const action =
            row.querySelector('[data-field="action"]').selectedOptions[0]
                ?.textContent || "";
        if (
            !`${trigger} ${giftId} ${action}`
                .toLocaleLowerCase("vi")
                .includes(query)
        )
            return;

        const button = document.createElement("button");
        button.type = "button";
        button.className = "interaction-item";
        button.setAttribute("aria-pressed", row === selected);
        button.dataset.enabled = row.dataset.enabled;
        const title = document.createElement("strong");
        title.textContent = trigger || (giftId ? `Quà #${giftId}` : "Luật mới");
        const meta = document.createElement("span");
        meta.textContent = `${action}${row.dataset.enabled === "false" ? " · Tắt" : ""}`;
        const giftImage = giftId && recentGifts.get(giftId)?.giftPictureUrl;
        if (giftImage) {
            const image = document.createElement("img");
            image.src = giftImage;
            image.alt = "";
            image.width = image.height = 32;
            image.addEventListener("error", () => image.remove(), {
                once: true,
            });
            const text = document.createElement("div");
            text.append(title, meta);
            button.classList.add("interaction-item-with-icon");
            button.append(image, text);
        } else button.append(title, meta);
        button.addEventListener("click", () => selectRule(row));
        buttons.push(button);
    });
    ui.masterIndex.replaceChildren(...buttons);
    ui.masterEditorEmpty.hidden = !!selected;
    ui.masterEmpty.hidden = buttons.length > 0;
    ui.masterEmpty.textContent = rows.length
        ? "Không tìm thấy luật."
        : "Chưa có luật.";
}

function selectRule(row, focus = true) {
    if (row && row.dataset.src !== ruleSource) selectSource(row.dataset.src);
    activeRuleId = row?.dataset.ruleId;
    updateRuleList();
    if (focus)
        row?.querySelector('[data-field="trigger"]').focus({
            preventScroll: true,
        });
}

function markMasterDirty() {
    masterDirty = true;
    masterRevision++;
    setMasterMessage("Có thay đổi chưa lưu.");
}

function addRule(rule) {
    if (!masterLoaded)
        return setMasterMessage("Đang tải cấu hình từ máy chủ.", true);
    if (ui.masterRules.children.length >= 100)
        return setMasterMessage("Tối đa 100 luật.", true);
    ui.filterMaster.value = "";
    const row = createRuleRow(rule);
    ui.masterRules.append(row);
    selectRule(row);
    ui.masterIndex
        .querySelector('[aria-pressed="true"]')
        ?.scrollIntoView({ block: "nearest" });
    markMasterDirty();
}

function createRuleRow(rule) {
    const row = ui.masterRuleTemplate.content.firstElementChild.cloneNode(true);
    row.dataset.ruleId = rule.id || makeRuleId();
    const picker = row.querySelector("[data-gift-picker]");
    for (const input of row.querySelectorAll("[data-field]")) {
        const field = input.dataset.field;
        if (field === "durationSeconds")
            input.value = (Number(rule.durationMs) || 0) / 1000;
        else if (field === "zoomSeconds")
            input.value = (Number(rule.zoomDurationMs) || 3500) / 1000;
        else if (input.type === "checkbox")
            input.checked = rule[field] !== false;
        else
            input.value =
                field === "action" && rule[field] === "vip"
                    ? "camera"
                    : (rule[field] ?? "");
    }
    row.addEventListener("input", (event) => {
        if (event.target.closest("[data-test-quantity]")) return;
        updateRuleFields(row);
        updateRuleList();
        markMasterDirty();
    });
    picker.addEventListener("click", () =>
        openGiftDialog((gift) => {
            for (const [field, value] of Object.entries({
                trigger: gift.giftName,
                giftId: gift.giftId,
                match: "exact",
                displayDiamonds: gift.diamondCount,
            }))
                row.querySelector(`[data-field="${field}"]`).value = value;
            updateRuleFields(row);
            updateRuleList();
            markMasterDirty();
        }),
    );
    row.querySelector('[data-command="delete"]').addEventListener(
        "click",
        () => {
            const next = [...ui.masterRules.children].find(
                (item) => item !== row && item.dataset.src === ruleSource,
            );
            row.remove();
            selectRule(next);
            markMasterDirty();
        },
    );
    row.querySelector('[data-command="up"]').addEventListener("click", () => {
        const siblings = [...ui.masterRules.children].filter(
            (item) => item.dataset.src === row.dataset.src,
        );
        siblings[siblings.indexOf(row) - 1]?.before(row);
        updateRuleList();
        markMasterDirty();
    });
    row.querySelector('[data-command="down"]').addEventListener("click", () => {
        const siblings = [...ui.masterRules.children].filter(
            (item) => item.dataset.src === row.dataset.src,
        );
        siblings[siblings.indexOf(row) + 1]?.after(row);
        updateRuleList();
        markMasterDirty();
    });
    row.querySelector('[data-command="test"]').addEventListener("click", () => {
        if (testingRule) return;
        const gift =
            row.querySelector('[data-field="source"]').value === "gift";
        const repeatCount = gift
            ? Number(row.querySelector("[data-test-count]").value)
            : 1;
        if (
            !Number.isInteger(repeatCount) ||
            repeatCount < 1 ||
            repeatCount > 100
        )
            return setRuleTestStatus(row, "Nhập số lượng từ 1 đến 100.", true);
        if (
            !send({
                type: "master_test",
                ruleId: row.dataset.ruleId,
                repeatCount,
                master: readMasterFromUi(),
            })
        )
            return setRuleTestStatus(row, "Mất kết nối máy chủ.", true);
        testingRule = row;
        setRuleTestStatus(row, "Đang gửi sang game…");
        for (const rule of ui.masterRules.children) updateRuleFields(rule);
        testTimeout = setTimeout(
            () => finishRuleTest("Máy chủ chưa phản hồi. Thử lại.", true),
            8000,
        );
    });
    row.querySelectorAll("[data-test-preset]").forEach((button) =>
        button.addEventListener("click", () => {
            row.querySelector("[data-test-count]").value =
                button.dataset.testPreset;
        }),
    );
    updateRuleFields(row);
    return row;
}

function renderMaster(config) {
    masterConfig = config || masterConfig;
    masterLoaded = true;
    ui.saveMaster.disabled = false;
    ui.addMasterRule.disabled = false;
    ui.joinMode.value = masterConfig.joinMode || "keyword_only";
    ui.giftAlwaysJoins.checked = masterConfig.giftAlwaysJoins !== false;
    ui.masterRules.replaceChildren(
        ...(masterConfig.rules || []).map(createRuleRow),
    );
    const rows = [...ui.masterRules.children].filter(
        (row) => row.dataset.src === ruleSource,
    );
    if (!rows.some((row) => row.dataset.ruleId === activeRuleId))
        selectSource(ruleSource);
    else
        selectRule(
            rows.find((row) => row.dataset.ruleId === activeRuleId),
            false,
        );
}

function readMasterFromUi() {
    const rules = [...ui.masterRules.querySelectorAll(".master-rule-row")].map(
        (row) => ({
            id: row.dataset.ruleId,
            enabled: row.querySelector('[data-field="enabled"]').checked,
            source: row.querySelector('[data-field="source"]').value,
            heartGoal: Number(
                row.querySelector('[data-field="heartGoal"]').value,
            ),
            trigger: row.querySelector('[data-field="trigger"]').value.trim(),
            giftId: row.querySelector('[data-field="giftId"]').value.trim(),
            match: row.querySelector('[data-field="match"]').value,
            action: row.querySelector('[data-field="action"]').value,
            displayDiamonds:
                Number(
                    row.querySelector('[data-field="displayDiamonds"]').value,
                ) || 0,
            zoomDurationMs: Math.round(
                (Number(
                    row.querySelector('[data-field="zoomSeconds"]').value,
                ) || 3.5) * 1000,
            ),
            durationMs: Math.round(
                (Number(
                    row.querySelector('[data-field="durationSeconds"]').value,
                ) || 0) * 1000,
            ),
            label: row.querySelector('[data-field="label"]').value.trim(),
            variant: row.querySelector('[data-field="variant"]').value,
            fireworkBursts:
                Number(
                    row.querySelector('[data-field="fireworkBursts"]').value,
                ) || 0,
        }),
    );
    return {
        joinMode: ui.joinMode.value,
        giftAlwaysJoins: ui.giftAlwaysJoins.checked,
        rules,
    };
}

/* ═══ GIFT CATALOG ═══════════════════════════════════════ */
const giftDialog = document.getElementById("gift-library-dialog");
const giftPriceTabs = document.getElementById("gift-price-tabs");
const giftGrid = document.getElementById("gift-library-grid");
const giftLibraryStatus = document.getElementById("gift-library-status");
const refreshGiftLibrary = document.getElementById("refresh-gift-library");
let giftRefreshTimer;
let giftPrice = null;
const failedGiftIcons = new Set();
let onGiftSelected;

function openGiftDialog(selectGift) {
    onGiftSelected = selectGift;
    failedGiftIcons.clear();
    giftPrice = null;
    renderGiftDialog();
    giftDialog.showModal();
}

refreshGiftLibrary.addEventListener("click", () => {
    if (!send({ type: "gift_catalog_refresh" })) {
        giftLibraryStatus.textContent = "Mất kết nối Bridge.";
        return;
    }
    refreshGiftLibrary.disabled = true;
    giftLibraryStatus.textContent = "Đang tải quà từ LIVE…";
    giftRefreshTimer = setTimeout(() => {
        refreshGiftLibrary.disabled = false;
        giftLibraryStatus.textContent =
            "Bridge chưa phản hồi. Kiểm tra kết nối LIVE hoặc khởi động lại Bridge.";
    }, 15000);
});

function renderGiftDialog() {
    const gifts = [...recentGifts.values()].filter(
        (gift) =>
            gift.giftPictureUrl && !failedGiftIcons.has(gift.giftPictureUrl),
    );
    const priceOf = (gift) => Number(gift.diamondCount) || 0;
    const prices = [...new Set(gifts.map(priceOf))].sort((a, b) => a - b);
    if (giftPrice !== null && !prices.includes(giftPrice)) giftPrice = null;
    giftPriceTabs.replaceChildren(
        ...[null, ...prices].map((price) => {
            const button = document.createElement("button");
            button.type = "button";
            button.className = "btn btn-secondary btn-sm";
            button.textContent =
                price === null
                    ? "Tất cả"
                    : price
                      ? `${price.toLocaleString("vi-VN")} xu`
                      : "Chưa rõ giá";
            button.setAttribute("aria-pressed", String(giftPrice === price));
            button.addEventListener("click", () => {
                giftPrice = price;
                renderGiftDialog();
                giftPriceTabs.querySelector('[aria-pressed="true"]')?.focus();
            });
            return button;
        }),
    );
    giftGrid.replaceChildren();
    for (const gift of gifts) {
        const price = priceOf(gift);
        if (giftPrice !== null && price !== giftPrice) continue;
        const button = document.createElement("button");
        button.type = "button";
        button.className = "btn btn-secondary gift-library-item";
        const icon = document.createElement("span");
        icon.className = "gift-library-icon";
        const image = document.createElement("img");
        image.alt = "";
        image.loading = "lazy";
        button.disabled = true;
        image.addEventListener(
            "load",
            () => {
                button.disabled = false;
            },
            { once: true },
        );
        image.addEventListener(
            "error",
            () => {
                failedGiftIcons.add(gift.giftPictureUrl);
                if (!button.isConnected) return;
                button.remove();
                if (!giftGrid.childElementCount)
                    giftGrid.textContent = "Mức giá này chưa có quà có ảnh.";
            },
            { once: true },
        );
        image.src = gift.giftPictureUrl;
        icon.append(image);
        const title = document.createElement("strong");
        title.textContent = gift.giftName || "Quà";
        const cost = document.createElement("small");
        cost.textContent = price
            ? `${price.toLocaleString("vi-VN")} xu`
            : "Chưa rõ giá";
        button.append(icon, title, cost);
        button.addEventListener("click", () => {
            giftDialog.close();
            onGiftSelected(gift);
        });
        giftGrid.append(button);
    }
    if (!gifts.length)
        giftGrid.textContent = "Chưa có quà có ảnh trong thư viện.";
}

function renderGiftCatalog() {
    if (giftDialog.open) renderGiftDialog();
    for (const row of ui.masterRules.children) {
        updateRuleFields(row);
    }
    updateRuleList();
}

/* ═══ SOCKET ═════════════════════════════════════════════ */
function connectSocket() {
    clearTimeout(reconnectTimer);
    const protocol = location.protocol === "https:" ? "wss:" : "ws:";
    socket = new WebSocket(`${protocol}//${location.host}`);

    socket.addEventListener("open", () => {
        send({ type: "register", role: "control" });
    });

    socket.addEventListener("message", (event) => {
        let data;
        try {
            data = JSON.parse(event.data);
        } catch {
            return;
        }
        if (data.type === "status" || data.type === "error") setStatus(data);
        if (data.type === "metrics") setMetrics(data);
        if (
            data.type === "master_config" &&
            !masterDirty &&
            savingRevision === null
        )
            renderMaster(data.master);
        if (data.type === "master_saved") {
            const savedCurrentDraft = savingRevision === masterRevision;
            savingRevision = null;
            ui.saveMaster.disabled = false;
            const changedHeartRule = savingHeartRules.some(
                (rule) =>
                    !data.master?.rules?.some(
                        (saved) =>
                            saved.id === rule.id &&
                            saved.source === rule.source &&
                            saved.action === rule.action &&
                            saved.heartGoal === rule.heartGoal,
                    ),
            );
            savingHeartRules = [];
            if (!data.error && changedHeartRule) {
                masterDirty = true;
                return setMasterMessage(
                    "Máy chủ không lưu đúng luật tim. Khởi động lại Bridge rồi lưu lại.",
                    true,
                );
            }
            if (!data.error && savedCurrentDraft) {
                masterDirty = false;
                renderMaster(data.master);
            }
            setMasterMessage(
                !data.error && masterDirty
                    ? "Đã lưu bản trước. Còn thay đổi chưa lưu."
                    : data.message,
                data.error,
            );
        }
        if (data.type === "master_test_spawn_result") {
            ui.spawnTestActor.disabled = false;
            ui.testActorStatus.textContent = data.message;
        }
        if (data.type === "master_test_result")
            finishRuleTest(data.message, data.error);
        if (data.type === "gift_catalog") {
            recentGifts.clear();
            for (const g of data.gifts || [])
                if (g.giftId) recentGifts.set(String(g.giftId), g);
            document.getElementById("gift-catalog-status").textContent =
                {
                    room: `${data.roomCount} quà từ phòng LIVE · còn lại là quà tham khảo hoặc đã lưu.`,
                    loading: "Đang tải danh mục phòng LIVE…",
                    failed: "Chưa tải được danh mục phòng LIVE. Đang hiện quà đã lưu.",
                }[data.state] ||
                (data.vietnamReferenceCount
                    ? `${data.vietnamReferenceCount} quà tham khảo Việt Nam. Kết nối LIVE để xác nhận danh mục phòng.`
                    : "Danh mục đã lưu. Kết nối LIVE để đồng bộ quà của phòng.");
            renderGiftCatalog();
            giftLibraryStatus.textContent =
                `${recentGifts.size} món · ` +
                document.getElementById("gift-catalog-status").textContent;
        }
        if (data.type === "gift_catalog_refresh_result") {
            clearTimeout(giftRefreshTimer);
            refreshGiftLibrary.disabled = false;
            if (data.message) giftLibraryStatus.textContent = data.message;
        }
        if (data.type === "error")
            setMasterMessage(data.message || "Có lỗi.", true);
    });

    socket.addEventListener("close", () => {
        ui.spawnTestActor.disabled = false;
        ui.testActorStatus.textContent = "";
        finishRuleTest("Mất kết nối máy chủ.", true);
        if (savingRevision !== null) {
            savingRevision = null;
            savingHeartRules = [];
            ui.saveMaster.disabled = false;
            setMasterMessage(
                "Mất kết nối khi lưu. Hãy kiểm tra và lưu lại.",
                true,
            );
        }
        setStatus({
            state: "disconnected",
            message: "Đang kết nối lại máy chủ…",
        });
        reconnectTimer = setTimeout(connectSocket, 2000);
    });

    socket.addEventListener("error", () => socket.close());
}

/* ═══ EVENT LISTENERS ════════════════════════════════════ */
ui.connect.addEventListener("click", () => {
    const username = ui.username.value.trim();
    if (username) send({ type: "set_username", username });
});
ui.username.addEventListener("keydown", (e) => {
    if (e.key === "Enter") ui.connect.click();
});
ui.disconnect.addEventListener("click", () =>
    send({ type: "disconnect_tiktok" }),
);
ui.stopDemo.addEventListener("click", () => send({ type: "demo_stop" }));
ui.reset.addEventListener("click", () => send({ type: "reset_game" }));
ui.spawnTestActor.addEventListener("click", () => {
    if (!send({ type: "master_test_spawn" })) {
        ui.testActorStatus.textContent = "Mất kết nối máy chủ.";
        return;
    }
    ui.spawnTestActor.disabled = true;
    ui.testActorStatus.textContent = "Đang chờ game…";
});

ui.addMasterRule.addEventListener("click", () => addRule(emptyRule()));
ui.filterMaster.addEventListener("input", updateRuleList);
ui.recentGifts.addEventListener("click", () => {
    openGiftDialog((gift) =>
        addRule(
            emptyRule({
                source: "gift",
                trigger: gift.giftName || "",
                giftId: String(gift.giftId),
                displayDiamonds: Number(gift.diamondCount) || 0,
            }),
        ),
    );
});
ui.joinMode.addEventListener("change", markMasterDirty);
ui.giftAlwaysJoins.addEventListener("change", markMasterDirty);
ui.saveMaster.addEventListener("click", () => {
    if (savingRevision !== null) return;
    const master = readMasterFromUi();
    const invalid = master.rules.find(
        (rule) =>
            rule.enabled &&
            (rule.source === "gift"
                ? !rule.giftId
                : rule.source === "like"
                  ? !Number.isInteger(rule.heartGoal) ||
                    rule.heartGoal < 1 ||
                    rule.heartGoal > 100000000
                  : rule.source === "chat" &&
                    !rule.trigger.split(",").some((value) => value.trim())),
    );
    if (invalid) {
        ui.filterMaster.value = "";
        selectRule(
            [...ui.masterRules.children].find(
                (row) => row.dataset.ruleId === invalid.id,
            ),
        );
        return setMasterMessage(
            invalid.source === "gift"
                ? "Chọn món quà từ thư viện."
                : invalid.source === "like"
                  ? "Nhập mốc từ 1 đến 100.000.000 tim."
                  : "Nhập lệnh bình luận.",
            true,
        );
    }
    if (!send({ type: "master_save", master }))
        return setMasterMessage(
            "Mất kết nối máy chủ. Chưa lưu thay đổi.",
            true,
        );
    savingRevision = masterRevision;
    savingHeartRules = master.rules.filter(
        (rule) => rule.source === "like" && rule.enabled,
    );
    ui.saveMaster.disabled = true;
    setMasterMessage("Đang lưu và áp dụng…");
});

document.querySelectorAll("[data-demo-count]").forEach((btn) => {
    btn.addEventListener("click", () =>
        send({ type: "demo_start", count: Number(btn.dataset.demoCount) }),
    );
});

document.querySelectorAll("[data-action]").forEach((btn) => {
    btn.addEventListener("click", () =>
        send({
            type: "demo_event",
            action: btn.dataset.action,
            value: Number(btn.dataset.value) || 1,
            giftName: btn.dataset.giftName || "",
            userIndex: Number(ui.userIndex.value) || 1,
        }),
    );
});

/* ═══ INIT ═══════════════════════════════════════════════ */
connectSocket();

// Assets categories share the main Assets navigation entry.
const assetSections = document.querySelectorAll("[data-asset-section]");
for (const button of assetSections) {
    button.addEventListener("click", () => {
        for (const category of assetSections) {
            const selected = category === button;
            category.setAttribute("aria-pressed", String(selected));
            category.classList.toggle("btn-primary", selected);
            category.classList.toggle("btn-secondary", !selected);
            document.getElementById(category.dataset.assetSection).hidden =
                !selected;
        }
    });
}
