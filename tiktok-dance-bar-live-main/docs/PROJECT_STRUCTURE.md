# Cấu trúc và bảo trì dự án

## Thư mục chính

- `MediaSources/Characters/Gifs`: GIF nhân vật gốc và thông tin nguồn.
- `UnityProject/Assets/Resources/Banners`: bảng hiệu đóng gói. Bridge dùng cùng thư mục cho URL `/assets/banners`.
- `MediaSources/Special`: boss và asset đặc biệt chưa kích hoạt.
- `MediaSources/Videos`: video nguồn tạo nhân vật.
- `Data/Music`: nhạc người dùng, cài đặt và trạng thái nhạc.
- `Data/Videos`: video màn DJ, BPM và cấu hình sân khấu.
- `Data/Backgrounds/default.png`: ảnh nền gốc. `images/` chứa ảnh tải lên; `settings.json` giữ lựa chọn.
- `Data/Dj/frames`: frame hoạt ảnh DJ; `source.gif` là bản gốc.
- `Data/Assets/characters`, `wings`, `banners`: asset nhập thêm, mỗi asset có thư mục ID riêng.
- `Data/Assets/library.json`: danh mục, trạng thái bật/tắt và lựa chọn cánh/bảng hiệu. Không tự đổi ID.
- `TikTokBridge/server.js`: điểm khởi động Bridge và xử lý WebSocket.
- `TikTokBridge/src/routes/http.js`: đăng ký trang quản lý, URL media và API HTTP. Các API theo chức năng nằm cùng thư mục.
- `TikTokBridge/src/config/paths.js`: đường dẫn chung phía Node.
- `UnityProject/Assets/Scripts/Core/GamePaths.cs`: đường dẫn chung phía game.
- `UnityProject/Assets/Scripts`: Core, Networking, Players, Stage, Media, Settings, Effects, Diagnostics.
- `UnityProject/Assets/Resources`: tài nguyên đã nhúng vào Unity. Không đổi tên tùy tiện vì code tải theo Resources và Unity lưu GUID trong `.meta`.
- `Build`: chương trình game đã biên dịch; không lưu dữ liệu người dùng ở đây nữa.
- `tools`: công cụ bảo trì/build.
- `docs`: hướng dẫn sử dụng, cấu trúc dự án và thông tin nguồn ảnh cánh, bảng hiệu.
- `.maintenance`: bản khôi phục, log, DLL kiểm thử và bản ZIP bộ cài cũ. Không phải dữ liệu chạy game.

## Khởi động và kiểm tra

1. Chạy `run.bat` hoặc trong `TikTokBridge` chạy `npm start`.
2. Kiểm tra dữ liệu bằng `node tools/verify-data.js`. Kiểm tra Node: `npm test` trong `TikTokBridge`.
3. Build đầy đủ bằng Unity 6000.2.10f1 và `build.bat`.
4. Máy hiện tại chưa có Unity Editor. Có thể dùng `powershell -ExecutionPolicy Bypass -File tools/build-managed.ps1 -Apply` sau khi đóng game để biên dịch lại C# bằng thư viện đúng bản Build đang có. Cách này chỉ cập nhật code, không build lại shader, scene hoặc tài nguyên Unity.

## Nguyên tắc

- Tên file upload do người dùng đặt và ID asset được giữ nguyên; chỉ đường dẫn thư mục được chuẩn hóa.
- Các URL cũ `/assets/gifs`, `/assets/banners`, `/music-files`, `/background-files`, `/library-files` vẫn được hỗ trợ.
- Không xóa `.meta` của asset Unity. Khi chuyển script phải chuyển cả `.cs.meta` tương ứng.
- `Data` và `Build` được commit để clone về là chạy được. File trạng thái do game tự ghi (`MUSIC_COMMAND.json`, `MUSIC_STATUS.json`, `STAGE_STATUS.json`) bị `.gitignore` bỏ qua. Đừng commit nhạc/video cá nhân hoặc có bản quyền.
- `.maintenance/before-cleanup-source.zip` chứa source/config/DLL trước thay đổi. `.maintenance/media-before.json` ghi checksum dữ liệu trước chuyển; `media-after-verification.json` ghi kết quả đối chiếu sau chuyển.

## Nhập WebP

WebP tĩnh/động được xử lý bằng Python 3 + Pillow (hỗ trợ libwebp), script `TikTokBridge/scripts/decode-webp.py`; giữ alpha, tối đa 512px/chiều, 12 FPS và 120 frame. Cài phụ thuộc trên máy mới: `python -m pip install -r tools/requirements-media.txt`. Có thể đặt `PYTHON_PATH` trong `.env` nếu cần đường dẫn Python cụ thể. GIF/PNG/JPG vẫn dùng FFmpeg.
