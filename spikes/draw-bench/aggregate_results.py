import os
import json
import glob

def main():
    script_dir = os.path.dirname(os.path.abspath(__file__))
    results_dir = os.path.join(script_dir, "results")
    if not os.path.exists(results_dir):
        print(f"Results directory not found: {results_dir}")
        return

    json_files = glob.glob(os.path.join(results_dir, "*.json"))
    if not json_files:
        print("No result JSON files found.")
        return

    data = []
    for f in sorted(json_files):
        try:
            with open(f, "r", encoding="utf-8") as fp:
                d = json.load(fp)
                data.append(d)
        except Exception as e:
            print(f"Error reading {f}: {e}")

    print("\n=========================================================================================")
    print("                    BẢNG SO SÁNH HIỆU NĂNG VẼ: GODOT vs BEVY (dGPU NVIDIA)")
    print("=========================================================================================\n")
    print("| Engine | Cảnh | Biến Thể | Điểm / Hạt | FPS Avg | Frame ms (Avg / p95 / p99) | CPU Push (ms) | RAM (MB) | GPU Adapter |")
    print("| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- |")

    for d in data:
        engine = d.get("engine", "").capitalize()
        scene = d.get("scene", "")
        mode = d.get("mode", "").upper()
        points = f"{d.get('points', 0):,}"
        fps = f"{d.get('fps_avg', 0.0):.1f}"
        frame_avg = f"{d.get('frame_ms_avg', 0.0):.2f}"
        frame_p95 = f"{d.get('frame_ms_p95', 0.0):.2f}"
        frame_p99 = f"{d.get('frame_ms_p99', 0.0):.2f}"
        frame_stat = f"{frame_avg} / {frame_p95} / {frame_p99}"
        cpu_push = f"{d.get('cpu_push_ms', 0.0):.2f}"
        ram = f"{d.get('ram_mb', 0.0):.1f}"
        gpu = d.get("gpu_adapter_name", "Unknown")
        print(f"| {engine} | {scene} | {mode} | {points} | {fps} | {frame_stat} | {cpu_push} | {ram} | {gpu} |")

    print("\n=========================================================================================\n")

if __name__ == "__main__":
    main()
