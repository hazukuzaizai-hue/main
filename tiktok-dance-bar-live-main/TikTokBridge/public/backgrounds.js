(() => {
    const $ = (id) => document.getElementById(id);
    const message = (text, error = false) => {
        $("background-message").textContent = text;
        $("background-message").style.color = error ? "var(--danger)" : "";
    };
    async function request(url, options) {
        const response = await fetch(url, options);
        const data = await response.json();
        if (!response.ok)
            throw new Error(data.error || "Không thể kết nối máy chủ.");
        return data;
    }
    async function load() {
        const data = await request("/api/backgrounds");
        $("background-gallery").replaceChildren();
        data.images.forEach((image) => {
            const card = document.createElement("article");
            card.className =
                "background-choice" +
                (image.name === data.selected ? " selected" : "");
            const preview = document.createElement(image.url ? "img" : "div");
            if (image.url) {
                preview.src = image.url;
                preview.alt = image.label;
                preview.loading = "lazy";
            } else {
                preview.className = "stage-preview";
                preview.textContent = "♫ SÂN KHẤU 3D";
            }
            const name = document.createElement("strong");
            name.textContent = image.label;
            const button = document.createElement("button");
            button.className =
                "btn " +
                (image.name === data.selected
                    ? "btn-primary"
                    : "btn-secondary");
            button.textContent =
                image.name === data.selected ? "✓ Đang chọn" : "Dùng nền này";
            button.addEventListener("click", async () => {
                button.disabled = true;
                try {
                    await request("/api/backgrounds/select", {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ selected: image.name }),
                    });
                    await load();
                    message("Đã chọn nền. Game cập nhật trong khoảng 2 giây.");
                } catch (error) {
                    message(error.message, true);
                    button.disabled = false;
                }
            });
            card.append(preview, name, button);
            $("background-gallery").append(card);
        });
    }
    $("add-background").addEventListener("click", () =>
        $("background-file").click(),
    );
    $("background-file").addEventListener("change", async () => {
        const file = $("background-file").files[0];
        if (!file) return;
        $("add-background").disabled = true;
        try {
            if (
                !/\.(png|jpe?g)$/i.test(file.name) ||
                !file.size ||
                file.size > 20 * 1048576
            )
                throw new Error("Chọn ảnh PNG/JPG, tối đa 20 MB.");
            const bitmap = await createImageBitmap(file);
            const tooLarge =
                bitmap.width > 8192 ||
                bitmap.height > 8192 ||
                bitmap.width * bitmap.height > 32000000;
            bitmap.close();
            if (tooLarge)
                throw new Error(
                    "Ảnh quá lớn. Hãy dùng ảnh tối đa 8192 px mỗi chiều và 32 megapixel.",
                );
            message("Đang tải ảnh nền…");
            await request(
                "/api/backgrounds/upload?name=" + encodeURIComponent(file.name),
                {
                    method: "POST",
                    headers: { "Content-Type": "application/octet-stream" },
                    body: file,
                },
            );
            await load();
            message("Đã thêm ảnh. Bấm “Dùng nền này” để áp dụng.");
        } catch (error) {
            message(error.message, true);
        } finally {
            $("add-background").disabled = false;
            $("background-file").value = "";
        }
    });
    load().catch((error) => message(error.message, true));
})();
