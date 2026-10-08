# SPIKE ĐO VẼ: GODOT 4.7.2 vs BEVY 0.19.1

Spike đo đạc hiệu năng render 2D (MultiMesh vs Bevy Wgpu Point Mesh) phục vụ quyết định kiến trúc trước pha S4 của dự án Cosmos.

## 1. Cách Chạy (Dành cho Yang)
1. Đảm bảo cắm sạc laptop và không chạy các ứng dụng nặng khác (để GPU không bị throttle).
2. Vào thư mục `spikes\draw-bench\` (hoặc thư mục gốc Cosmos).
3. Click đúp vào file `run-draw-bench.bat`.
4. Script sẽ tự động chạy lần lượt 12 kịch bản đo (mỗi cảnh 10 giây, bỏ 2 giây đầu, tự thoát).
5. Màn hình console sẽ tự động in bảng tổng hợp so sánh hoàn chỉnh và lưu kết quả JSON vào thư mục `results\`.

## 2. Bảng So Sánh Dự Kiến (Trống — Điền sau khi chạy run-draw-bench.bat)

| Engine | Cảnh | Biến Thể | Điểm / Hạt | FPS Avg | Frame ms (Avg / p95 / p99) | CPU Push (ms) | RAM (MB) | GPU Adapter |
| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- |
| **Godot** | A | CPU Push | 100,000 | ... | ... / ... / ... | ... | ... | NVIDIA GeForce GTX 1650 |
| **Godot** | A | Shader RAIL | 100,000 | ... | ... / ... / ... | ... | ... | NVIDIA GeForce GTX 1650 |
| **Bevy** | A | CPU Push | 100,000 | ... | ... / ... / ... | ... | ... | NVIDIA GeForce GTX 1650 |
| **Bevy** | A | Shader RAIL | 100,000 | ... | ... / ... / ... | ... | ... | NVIDIA GeForce GTX 1650 |
| **Godot** | B | CPU Push | 1,000,000 | ... | ... / ... / ... | ... | ... | NVIDIA GeForce GTX 1650 |
| **Godot** | B | Shader RAIL | 1,000,000 | ... | ... / ... / ... | ... | ... | NVIDIA GeForce GTX 1650 |
| **Bevy** | B | CPU Push | 1,000,000 | ... | ... / ... / ... | ... | ... | NVIDIA GeForce GTX 1650 |
| **Bevy** | B | Shader RAIL | 1,000,000 | ... | ... / ... / ... | ... | ... | NVIDIA GeForce GTX 1650 |
| **Godot** | C | CPU Push | 1,000,000 + FX | ... | ... / ... / ... | ... | ... | NVIDIA GeForce GTX 1650 |
| **Godot** | C | Shader RAIL | 1,000,000 + FX | ... | ... / ... / ... | ... | ... | NVIDIA GeForce GTX 1650 |
| **Bevy** | C | CPU Push | 1,000,000 + FX | ... | ... / ... / ... | ... | ... | NVIDIA GeForce GTX 1650 |
| **Bevy** | C | Shader RAIL | 1,000,000 + FX | ... | ... / ... / ... | ... | ... | NVIDIA GeForce GTX 1650 |

*(Ghi chú Cảnh C: 1M chấm + 200 quầng sáng bloom/glow + 50k hạt nổ GPU explosion particles).*

## 3. Ghi Nhận Công Sức & Khảo Sát Kỹ Thuật (Godot vs Bevy)

### Thống kê dòng code (LOC)
- **Godot (C# + GDShader)**: ~380 dòng code.
  - `Main.cs`: 330 dòng (setup MultiMesh, CPU buffer push, Shader parameter, GpuParticles2D, đo đạc thống kê percentile, JSON export).
  - `orbit_rail.gdshader`: 16 dòng (vertex evaluation từ INSTANCE_CUSTOM).
  - `project.godot` + csproj: 34 dòng cấu hình.
- **Bevy (Rust + WGSL)**: ~650 dòng code.
  - `src/main.rs`: 580 dòng (setup ECS systems, Custom Material2d plugins, Hanabi particle effect, CPU mesh attribute update, Windows working set FFI, JSON export).
  - `orbit_rail.wgsl` + `orbit_cpu.wgsl`: 60 dòng (WGSL vertex shader tích hợp mesh2d_functions).
  - `Cargo.toml`: 16 dòng.

### Đánh giá độ khó, thiếu hụt và sự đánh đổi của Bevy
1. **API Thay Đổi Nhanh Giữa Các Bản (Churn)**:
   - Bevy 0.19 thay đổi nhiều API cốt lõi: `EventWriter` đổi thành `MessageWriter`, `RenderCreation::Automatic(Box<WgpuSettings>)` đòi hỏi cấp phát heap `Box`, các Bundle component cũ bị xoá chuyển sang tuples. Code mẫu trên mạng thường xuyên bị outdate.
2. **Hệ Thống UI & Font / Text Tiếng Việt**:
   - Godot: Có sẵn hệ thống Control Node, RichTextLabel hỗ trợ BBCode, font fallback tự động, xử lý font tiếng Việt hoàn chỉnh out-of-the-box.
   - Bevy: Core engine chỉ có Bevy UI mức độ sơ khai; không có RichTextLabel đầy đủ tính năng; muốn có giao diện bảng/HUD phức tạp như Cosmos phải tự viết hoặc nhúng `bevy_egui`. Font tiếng Việt cần tự cấu hình text shaping qua HarfBuzz/Cosmic-text cẩn thận.
3. **Hiệu Ứng Hạt (Particle System)**:
   - Godot: Built-in `GPUParticles2D` cực kỳ mạnh, khai báo chỉ 5-10 dòng là có ngay 50k hạt nổ có spread, velocity, color over lifetime.
   - Bevy: Core không có particle system. Bắt buộc phải kéo thêm crate ngoài `bevy_hanabi 0.19.0`, viết pipeline dạng AST `ExprWriter`, modifier graph khá phức tạp.
4. **Thời Gian Build & Dung Lượng Binary**:
   - Godot C#: Build Release chỉ mất ~4-8 giây.
   - Bevy Rust: Clean build Release mất ~2-3 phút (hơn 250 crates phụ thuộc). Binary release độc lập nặng ~35 MB.
