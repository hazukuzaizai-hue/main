# TikTok Live Studio

Game sàn nhảy tương tác cho TikTok LIVE: nhân vật, cánh, bảng hiệu, sân khấu LED, video DJ và nhạc.

## Cách chạy (Windows 10/11)

1. Cài [Node.js 20+](https://nodejs.org/).
2. Tải repo về (**Code → Download ZIP**, giải nén) hoặc `git clone`.
3. Nhấp đúp **`run.bat`**.

Lần đầu `run.bat` tự cài thư viện (cần mạng), rồi mở game và trang điều khiển http://127.0.0.1:3000/control.html.
Vào tab **Kết nối LIVE**, nhập username TikTok đang phát và bấm **Kết nối**.

## Tuỳ chọn

- **FFmpeg**: chỉ cần khi tự thêm nhân vật từ GIF/PNG/JPG.
- **Python 3 + Pillow**: chỉ cần khi nhập WebP (`pip install -r tools/requirements-media.txt`).
- **Đổi cổng hoặc nguồn live**: copy `TikTokBridge/.env.example` thành `TikTokBridge/.env` rồi sửa.

## Thư mục

| Thư mục | Nội dung |
|---|---|
| `Build` | Game đã build sẵn |
| `Data` | Nhạc, video, nhân vật, ảnh nền (thêm qua tab Assets) |
| `TikTokBridge` | Server Node.js và trang điều khiển |
| `UnityProject` | Source Unity 6000.2.10f1 (`build.bat` để build lại) |

Hướng dẫn chi tiết: [docs/usage/HUONG_DAN_SU_DUNG.txt](docs/usage/HUONG_DAN_SU_DUNG.txt) · Cấu trúc dự án: [docs/PROJECT_STRUCTURE.md](docs/PROJECT_STRUCTURE.md)
