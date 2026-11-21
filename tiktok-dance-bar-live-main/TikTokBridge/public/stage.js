(() => {
    const $ = (id) => document.getElementById(id);
    const message = (text, error = false) => {
        $("stage-message").textContent = text;
        $("stage-message").style.color = error ? "var(--danger)" : "";
    };
    let videos = [];
    async function request(url, options) {
        const response = await fetch(url, options);
        const data = await response.json();
        if (!response.ok)
            throw new Error(data.error || "Không thể kết nối máy chủ.");
        return data;
    }
    function preview() {
        const video = videos.find(
            (video) => video.name === $("stage-video").value,
        );
        const player = $("stage-video-preview");
        player.pause();
        if (video) player.src = video.url;
        else {
            player.removeAttribute("src");
            player.load();
        }
    }
    async function load() {
        const data = await request("/api/stage");
        videos = data.videos;
        $("stage-mode").value = data.mode;
        $("stage-video").replaceChildren();
        const empty = document.createElement("option");
        empty.value = "";
        empty.textContent = "Chọn video";
        $("stage-video").append(empty);
        videos.forEach((video) => {
            const option = document.createElement("option");
            option.value = video.name;
            option.textContent = video.name;
            $("stage-video").append(option);
        });
        $("stage-video").value = data.video;
        preview();
        $("stage-game-status").textContent = data.status.message;
    }
    $("stage-video").addEventListener("change", preview);
    $("save-stage").addEventListener("click", async () => {
        $("save-stage").disabled = true;
        try {
            await request("/api/stage/settings", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    mode: $("stage-mode").value,
                    video: $("stage-video").value,
                }),
            });
            message("Đã lưu. Game cập nhật trong khoảng 2 giây.");
        } catch (error) {
            message(error.message, true);
        } finally {
            $("save-stage").disabled = false;
        }
    });
    $("add-stage-video").addEventListener("click", () =>
        $("stage-video-file").click(),
    );
    $("stage-video-file").addEventListener("change", async () => {
        const file = $("stage-video-file").files[0];
        if (!file) return;
        $("add-stage-video").disabled = true;
        try {
            if (
                !/\.mp4$/i.test(file.name) ||
                !file.size ||
                file.size > 100 * 1048576
            )
                throw new Error("Chọn video MP4 tối đa 100 MB.");
            message("Đang tải video…");
            await request(
                "/api/stage/upload?name=" + encodeURIComponent(file.name),
                {
                    method: "POST",
                    headers: { "Content-Type": "application/octet-stream" },
                    body: file,
                },
            );
            await load();
            $("stage-video").value = file.name;
            $("stage-mode").value = "video";
            preview();
            message("Đã thêm video. Bấm “Lưu & áp dụng” để phát trong game.");
        } catch (error) {
            message(error.message, true);
        } finally {
            $("add-stage-video").disabled = false;
            $("stage-video-file").value = "";
        }
    });
    setInterval(async () => {
        if (
            !$("tab-assets").classList.contains("active") ||
            $("tab-stage").hidden
        )
            return;
        try {
            const data = await request("/api/stage");
            $("stage-game-status").textContent = data.status.message;
        } catch {
            $("stage-game-status").textContent =
                "Không lấy được trạng thái game";
        }
    }, 3000);
    load().catch((error) => message(error.message, true));
})();
