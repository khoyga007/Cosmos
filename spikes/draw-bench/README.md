# SPIKE ĐO VẼ: GODOT 4.7.2 vs BEVY 0.19.1
## GÓI BẢO VỆ AN TOÀN 5 ĐIỂM (CẤP PHẦN CỨNG) DÀNH CHO MÁY YANG

Spike đo đạc hiệu năng render 2D (MultiMesh vs Bevy Wgpu Point Mesh) phục vụ quyết định kiến trúc trước pha S4 của dự án Cosmos, được trang bị gói bảo vệ 5 điểm chống treo máy / TDR driver hang.

---

## 1. GÓI BẢO VỆ AN TOÀN 5 ĐIỂM

1. **Kiểm tra dGPU chặt chẽ ở đầu script (Preflight Check)**:
   - Truy vấn trực tiếp `Win32_VideoController`.
   - Bắt buộc phát hiện `NVIDIA GeForce GTX 1650` (hoặc dGPU NVIDIA).
   - Nếu phát hiện chỉ chạy trên `Intel(R) UHD Graphics` -> **DỪNG NGAY LẬP TỨC**, in cảnh báo đỏ, không đo để tránh nghẽn GPU tích hợp.
2. **Watchdog cấp Hệ điều hành 25 giây (`watchdog_runner.ps1`)**:
   - Mỗi cảnh đo kéo dài 10s (warmup 2s).
   - Nếu một cảnh bị treo driver/TDR hoặc chạy quá 25 giây: Watchdog OS tự động kích hoạt `taskkill /F /T` cưỡng chế ngắt tiến trình, ghi nhận kết quả `TIMEOUT` vào JSON và trả về exit code 124.
3. **Cơ chế Fail-Stop (Tăng dần từ nhẹ tới nặng)**:
   - Thứ tự kịch bản: `A (100k)` → `D1 (100k + 1k bodies)` → `D2 (300k + 1k bodies)` → `D3 (1M + 1k bodies)` → `B (1M)` → `C (1M + Bloom + 50k Particles)`.
   - Nếu bất kỳ cảnh nào thất bại hoặc bị watchdog ngắt -> **DỪNG NGAY TOÀN BỘ BENCHMARK**, không chạy tiếp cảnh nặng hơn!
4. **2 Lượt đo cho mỗi cảnh (Capped 60 FPS & Uncapped)**:
   - Lượt 1: **Capped 60 FPS** — Phản ánh chính xác trải nghiệm thực tế Yang thấy khi chơi game trên màn hình 60Hz.
   - Lượt 2: **Uncapped FPS** — Đo trần throughput tối đa của phần cứng.
   - Đo và báo cáo đầy đủ: **FPS avg, Frame time avg, p50 (median), p95**.
5. **Kịch bản Cảnh D (§15 SPEC — Mô hình "Khối + Vật thể")**:
   - Khối bụi/đá vụn parcel mass biểu diễn bằng 100k / 300k / 1M điểm hạt render qua GPU shader.
   - Kết hợp ~1.000 bodies thật (những thiên thể lớn nhất, có quỹ đạo Keplerian riêng và được CPU cập nhật tọa độ mỗi frame).

---

## 2. Cách Chạy (Dành Cho Yang)

1. Cắm sạc laptop và không mở các tác vụ 3D/video nặng khác.
2. Chạy file batch an toàn tại đường dẫn tuyệt đối:
   `E:\Cosmos-draw-bench\spikes\draw-bench\run-draw-bench.bat`
3. Script sẽ tự kiểm tra dGPU NVIDIA, chạy tuần tự từng kịch bản tăng dần (mỗi cảnh bọc watchdog 25s).
4. Sau khi hoàn tất (hoặc nếu có cảnh dừng sớm), script tự động chạy `aggregate_results.py` in bảng Markdown so sánh hoàn chỉnh lên màn hình console và lưu kết quả JSON vào thư mục `results\`.

---

## 3. Bảng Khung Kết Quả Đo

| Engine | Cảnh | Biến Thể | Điểm / Khối | Cap FPS | FPS Avg | Frame ms (Avg / p50 / p95) | CPU Push (ms) | RAM (MB) | GPU Adapter |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **Godot** | A | Shader | 100,000 | 60 FPS | ... | ... / ... / ... | ... | ... | ... |
| **Godot** | A | Shader | 100,000 | Uncapped | ... | ... / ... / ... | ... | ... | ... |
| **Bevy** | A | Shader | 100,000 | 60 FPS | ... | ... / ... / ... | ... | ... | ... |
| **Bevy** | A | Shader | 100,000 | Uncapped | ... | ... / ... / ... | ... | ... | ... |
| **Godot** | D1 (§15) | Shader | 100k + 1k bodies | 60 FPS | ... | ... / ... / ... | ... | ... | ... |
| **Godot** | D1 (§15) | Shader | 100k + 1k bodies | Uncapped | ... | ... / ... / ... | ... | ... | ... |
| **Bevy** | D1 (§15) | Shader | 100k + 1k bodies | 60 FPS | ... | ... / ... / ... | ... | ... | ... |
| **Bevy** | D1 (§15) | Shader | 100k + 1k bodies | Uncapped | ... | ... / ... / ... | ... | ... | ... |
| **Godot** | D2 (§15) | Shader | 300k + 1k bodies | 60 FPS | ... | ... / ... / ... | ... | ... | ... |
| **Godot** | D2 (§15) | Shader | 300k + 1k bodies | Uncapped | ... | ... / ... / ... | ... | ... | ... |
| **Bevy** | D2 (§15) | Shader | 300k + 1k bodies | 60 FPS | ... | ... / ... / ... | ... | ... | ... |
| **Bevy** | D2 (§15) | Shader | 300k + 1k bodies | Uncapped | ... | ... / ... / ... | ... | ... | ... |
| **Godot** | D3 (§15) | Shader | 1M + 1k bodies | 60 FPS | ... | ... / ... / ... | ... | ... | ... |
| **Godot** | D3 (§15) | Shader | 1M + 1k bodies | Uncapped | ... | ... / ... / ... | ... | ... | ... |
| **Bevy** | D3 (§15) | Shader | 1M + 1k bodies | 60 FPS | ... | ... / ... / ... | ... | ... | ... |
| **Bevy** | D3 (§15) | Shader | 1M + 1k bodies | Uncapped | ... | ... / ... / ... | ... | ... | ... |
| **Godot** | B | Shader | 1,000,000 | 60 FPS | ... | ... / ... / ... | ... | ... | ... |
| **Godot** | B | Shader | 1,000,000 | Uncapped | ... | ... / ... / ... | ... | ... | ... |
| **Bevy** | B | Shader | 1,000,000 | 60 FPS | ... | ... / ... / ... | ... | ... | ... |
| **Bevy** | B | Shader | 1,000,000 | Uncapped | ... | ... / ... / ... | ... | ... | ... |
| **Godot** | C | Shader | 1M + Bloom/Part | 60 FPS | ... | ... / ... / ... | ... | ... | ... |
| **Godot** | C | Shader | 1M + Bloom/Part | Uncapped | ... | ... / ... / ... | ... | ... | ... |
| **Bevy** | C | Shader | 1M + Bloom/Part | 60 FPS | ... | ... / ... / ... | ... | ... | ... |
| **Bevy** | C | Shader | 1M + Bloom/Part | Uncapped | ... | ... / ... / ... | ... | ... | ... |
