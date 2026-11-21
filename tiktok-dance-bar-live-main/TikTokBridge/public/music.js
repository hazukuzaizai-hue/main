(() => {
    const $ = (id) => document.getElementById(id);
    const message = (text, error = false) => {
        $("music-message").textContent = text;
        $("music-message").classList.toggle("error", error);
    };
    async function request(url, options) {
        const response = await fetch(url, options);
        const data = await response.json();
        if (!response.ok)
            throw new Error(data.error || "Không thể kết nối máy chủ.");
        return data;
    }
    async function command(action, track = "") {
        try {
            await request("/api/music/command", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({ action, track }),
            });
            message("Đã gửi lệnh đến game. Chờ khoảng 2 giây.");
        } catch (error) {
            message(error.message, true);
        }
    }
    async function updateStatus() {
        try {
            const status = await request("/api/music/status");
            const labels = {
                offline: "Game chưa mở hoặc chưa kết nối trình nhạc",
                playing: "Đang phát",
                paused: "Đã tạm dừng",
                loading: "Đang tải bài",
                idle: "Chưa phát nhạc",
            };
            $("music-now-playing").textContent =
                (labels[status.state] || status.state) +
                (status.track ? ": " + status.track : "");
        } catch {
            $("music-now-playing").textContent =
                "Không lấy được trạng thái game";
        }
    }
    $("pause-game-music").addEventListener("click", () => command("pause"));
    $("resume-game-music").addEventListener("click", () => command("resume"));
    $("next-game-music").addEventListener("click", () => command("next"));
    setInterval(updateStatus, 2000);
    updateStatus();
    async function load() {
        const data = await request("/api/music");
        $("music-enabled").checked = data.enabled;
        $("music-volume").value = Math.round(data.volume * 100);
        $("music-volume-value").textContent = $("music-volume").value + "%";
        const list = $("music-track-list");
        list.replaceChildren();
        if (!data.tracks.length) {
            const empty = document.createElement("p");
            empty.className = "music-empty";
            empty.textContent =
                "Chưa có nhạc. Bấm “+ Thêm nhạc” để tải bài đầu tiên.";
            list.append(empty);
        }
        data.tracks.forEach((track, index) => {
            const row = document.createElement("div");
            row.className = "music-track";
            const number = document.createElement("span");
            number.className = "music-number";
            number.textContent = index + 1;
            const info = document.createElement("div");
            info.className = "music-track-info";
            const name = document.createElement("strong");
            name.textContent = track.name;
            const size = document.createElement("small");
            size.textContent = (track.size / 1048576).toFixed(1) + " MB";
            info.append(name, size);
            const audio = document.createElement("audio");
            audio.controls = true;
            audio.preload = "none";
            audio.src = track.url;
            audio.setAttribute("aria-label", "Nghe thử " + track.name);
            audio.addEventListener("play", () =>
                document
                    .querySelectorAll("#music-track-list audio")
                    .forEach((other) => {
                        if (other !== audio) other.pause();
                    }),
            );
            const play = document.createElement("button");
            play.className = "btn btn-primary";
            play.textContent = "▶ Phát trong game";
            play.addEventListener("click", () => {
                document
                    .querySelectorAll("#music-track-list audio")
                    .forEach((item) => item.pause());
                command("play", track.name);
            });
            row.append(number, info, play, audio);
            list.append(row);
        });
    }
    $("music-volume").addEventListener(
        "input",
        () =>
            ($("music-volume-value").textContent =
                $("music-volume").value + "%"),
    );
    $("save-music").addEventListener("click", async () => {
        $("save-music").disabled = true;
        try {
            await request("/api/music/settings", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    enabled: $("music-enabled").checked,
                    volume: Number($("music-volume").value) / 100,
                }),
            });
            message("Đã lưu. Game cập nhật trong khoảng 2 giây.");
        } catch (error) {
            message(error.message, true);
        } finally {
            $("save-music").disabled = false;
        }
    });
    $("add-music").addEventListener("click", () => $("music-files").click());
    $("music-files").addEventListener("change", async () => {
        const files = Array.from($("music-files").files);
        if (!files.length) return;
        $("add-music").disabled = true;
        const errors = [];
        let uploaded = 0;
        for (const file of files) {
            try {
                if (
                    !/\.(mp3|wav|ogg)$/i.test(file.name) ||
                    !file.size ||
                    file.size > 50 * 1048576
                )
                    throw new Error("Chỉ nhận MP3, WAV, OGG tối đa 50 MB.");
                message("Đang tải: " + file.name);
                await request(
                    "/api/music/upload?name=" + encodeURIComponent(file.name),
                    {
                        method: "POST",
                        headers: { "Content-Type": "application/octet-stream" },
                        body: file,
                    },
                );
                uploaded++;
            } catch (error) {
                errors.push(file.name + ": " + error.message);
            }
        }
        try {
            await load();
        } catch (error) {
            errors.push(error.message);
        }
        message(
            "Đã thêm " +
                uploaded +
                "/" +
                files.length +
                " bài. " +
                errors.join(" "),
            errors.length > 0,
        );
        $("music-files").value = "";
        $("add-music").disabled = false;
    });
    load().catch((error) => message(error.message, true));
})();
