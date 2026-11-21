(() => {
    const $ = (id) => document.getElementById(id);
    const msg = (text, error = false) => {
        $("asset-message").textContent = text;
        $("asset-message").style.color = error ? "var(--danger)" : "";
    };
    async function api(route, body) {
        const response = await fetch(
            "/api/library" + route,
            body
                ? {
                      method: "POST",
                      headers: { "Content-Type": "application/json" },
                      body: JSON.stringify(body),
                  }
                : {},
        );
        const data = await response.json();
        if (!response.ok) throw new Error(data.error);
        return data;
    }
    let activeKind = "character";
    let uploadKind = activeKind;
    async function test(id = "") {
        try {
            await api("/test", { id, text: $("asset-text").value });
            msg(
                "Đã gửi lệnh Test. Asset sẽ hiện trên nhân vật test trong 60 giây.",
            );
        } catch (e) {
            msg(e.message, true);
        }
    }
    async function load() {
        const data = await api("/");
        $("asset-gallery").replaceChildren();
        if (!data.items.some((item) => item.kind === activeKind)) {
            $("asset-gallery").textContent =
                activeKind === "avatarFrame"
                    ? "Thêm 3 ảnh nền trong suốt, lần lượt cho Top 1, Top 2 và Top 3."
                    : "Chưa có asset tải lên. Bấm “+ Thêm file” để bắt đầu.";
            return;
        }
        const items = data.items.filter((item) => item.kind === activeKind);
        for (const [index, item] of items.entries()) {
            const card = document.createElement("article");
            card.className = "background-choice";
            const image = document.createElement("img");
            const imageSource = /\.(gif|webp)$/i.test(item.source)
                ? item.source
                : item.preview;
            if (item.preview && item.preview !== imageSource) {
                image.addEventListener(
                    "error",
                    () => {
                        image.src = item.preview;
                    },
                    { once: true },
                );
            }
            image.alt = "";
            image.loading = "lazy";
            image.src = imageSource;
            const name = document.createElement("strong");
            const prefix = {
                character: "Dance",
                wing: "Cánh",
                banner: "Bảng hiệu",
                avatarFrame: "Khung",
            }[item.kind];
            const number =
                item.danceNumber ??
                item.displayNumber ??
                item.avatarRank ??
                index + 1;
            const displayName = `${prefix} ${number}`;
            name.textContent = displayName;
            name.title = displayName;
            const info = document.createElement("small");
            info.textContent =
                {
                    character: "Nhân vật",
                    wing: "Cánh",
                    banner: "Bảng hiệu",
                    avatarFrame: `Top ${item.avatarRank}`,
                }[item.kind] +
                " · " +
                item.frames +
                " frame" +
                (item.kind === "banner" && data.banner === item.id
                    ? " · Đang dùng"
                    : "");
            const testButton = document.createElement("button");
            testButton.className = "btn btn-primary";
            testButton.textContent = "Test";
            testButton.title = "Test trong game";
            testButton.onclick = () => test(item.id);
            const toggle = document.createElement("button");
            toggle.className = "btn btn-secondary";
            toggle.textContent = item.enabled ? "Bật" : "Tắt";
            toggle.title = item.enabled
                ? "Bấm để tắt asset"
                : "Bấm để bật asset";
            toggle.setAttribute("aria-pressed", String(item.enabled));
            toggle.onclick = async () => {
                try {
                    await api("/update", {
                        id: item.id,
                        enabled: !item.enabled,
                    });
                    await load();
                } catch (e) {
                    msg(e.message, true);
                }
            };
            const actions = document.createElement("div");
            actions.className = "asset-card-actions";
            actions.append(testButton, toggle);
            card.append(image, name, info, actions);
            if (item.kind === "banner") {
                const select = document.createElement("button");
                select.className = "btn btn-secondary";
                select.textContent = "Dùng";
                select.title = "Dùng trong game";
                select.onclick = async () => {
                    try {
                        await api("/update", {
                            id: item.id,
                            select: true,
                            enabled: true,
                        });
                        await load();
                        msg("Đã chọn. Cập nhật ở hiệu ứng VIP tiếp theo.");
                    } catch (e) {
                        msg(e.message, true);
                    }
                };
                actions.append(select);
            }
            const remove = document.createElement("button");
            remove.className = "btn btn-danger";
            remove.textContent = "Xóa";
            remove.onclick = async () => {
                if (!window.confirm(`Xóa “${displayName}” khỏi Assets?`))
                    return;
                remove.disabled = true;
                try {
                    await api("/delete", { id: item.id });
                    await load();
                    msg(`Đã xóa “${displayName}”.`);
                } catch (e) {
                    msg(e.message, true);
                } finally {
                    remove.disabled = false;
                }
            };
            actions.append(remove);
            $("asset-gallery").append(card);
        }
    }
    document.querySelectorAll("[data-asset-kind]").forEach((button) =>
        button.addEventListener("click", () => {
            activeKind = button.dataset.assetKind;
            $("asset-banner-tools").hidden = activeKind !== "banner";
            msg("");
            document.querySelectorAll("[data-asset-kind]").forEach((tab) => {
                tab.setAttribute("aria-pressed", String(tab === button));
                tab.className =
                    "btn " + (tab === button ? "btn-primary" : "btn-secondary");
            });
            load().catch((e) => msg(e.message, true));
        }),
    );
    $("asset-test-text").onclick = () => test();
    $("asset-default-banner").onclick = async () => {
        try {
            await api("/defaults", { kind: "banner" });
            await load();
            msg("Đã trở về banner gốc.");
        } catch (e) {
            msg(e.message, true);
        }
    };
    $("asset-upload").onclick = () => {
        uploadKind = activeKind;
        $("asset-file").click();
    };
    $("asset-file").onchange = async () => {
        const file = $("asset-file").files[0];
        if (!file) return;
        $("asset-upload").disabled = true;
        try {
            if (!file.size || file.size > 20 * 1048576)
                throw new Error("File tối đa 20 MB.");
            msg("Đang tải và xử lý các frame…");
            const response = await fetch(
                "/api/library/upload?kind=" +
                    uploadKind +
                    "&name=" +
                    encodeURIComponent(file.name),
                {
                    method: "POST",
                    headers: { "Content-Type": "application/octet-stream" },
                    body: file,
                },
            );
            const data = await response.json();
            if (!response.ok) throw new Error(data.error);
            await load();
            msg("Đã thêm asset. Bấm Test trong game để kiểm tra.");
        } catch (e) {
            msg(e.message, true);
        } finally {
            $("asset-upload").disabled = false;
            $("asset-file").value = "";
        }
    };
    load().catch((e) => msg(e.message, true));
})();
