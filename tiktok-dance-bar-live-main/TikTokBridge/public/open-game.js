(() => {
    const button = document.getElementById("open-game");
    const status = document.getElementById("open-game-message");
    button.addEventListener("click", async () => {
        button.disabled = true;
        button.textContent = "Đang mở…";
        status.textContent = "";
        try {
            const response = await fetch("/api/game/open", {
                method: "POST",
                headers: { "X-Studio-Action": "open-game" },
            });
            if (!response.ok) {
                const data = await response.json();
                status.textContent = data.error || "Không mở được game.";
            }
        } catch {
            status.textContent = "Không kết nối được Bridge. Hãy chạy run.bat.";
        } finally {
            button.disabled = false;
            button.textContent = "Mở game ↗";
        }
    });
})();
