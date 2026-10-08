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
    print("| Engine | Cảnh | Biến Thể | Điểm / Khối | Cap FPS | FPS Avg | Frame ms (Avg / p50 / p95) | CPU Push (ms) | RAM (MB) | GPU Adapter |")
    print("| :--- | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :---: | :--- |")

    for d in data:
        if d.get("status") == "TIMEOUT":
            engine = d.get("engine", "Unknown").capitalize()
            scene = d.get("scene", "?")
            mode = d.get("mode", "?").upper()
            cap = f"{d.get('fps_cap', 0)} FPS" if d.get('fps_cap', 0) > 0 else "Uncapped"
            print(f"| {engine} | {scene} | {mode} | - | {cap} | **TIMEOUT** | **> 25,000 ms (Killed by Watchdog)** | - | - | - |")
            continue

        engine = d.get("engine", "").capitalize()
        scene = d.get("scene", "")
        mode = d.get("mode", "").upper()
        points = d.get('points', 0)
        bodies = d.get('bodies', 0)
        if bodies > 0:
            pts_str = f"{points:,} + {bodies:,} bodies"
        else:
            pts_str = f"{points:,}"

        fps_cap = d.get('fps_cap', 0)
        cap_str = f"{fps_cap} FPS" if fps_cap > 0 else "Uncapped"

        fps = f"{d.get('fps_avg', 0.0):.1f}"
        frame_avg = f"{d.get('frame_ms_avg', 0.0):.2f}"
        frame_p50 = f"{d.get('frame_ms_p50', 0.0):.2f}"
        frame_p95 = f"{d.get('frame_ms_p95', 0.0):.2f}"
        frame_stat = f"{frame_avg} / {frame_p50} / {frame_p95}"
        cpu_push = f"{d.get('cpu_push_ms', 0.0):.2f}"
        ram = f"{d.get('ram_mb', 0.0):.1f}"
        gpu = d.get("gpu_adapter_name", "Unknown")
        print(f"| {engine} | {scene} | {mode} | {pts_str} | {cap_str} | {fps} | {frame_stat} | {cpu_push} | {ram} | {gpu} |")

    print("\n=========================================================================================\n")

if __name__ == "__main__":
    main()
