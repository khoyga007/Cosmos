use std::fs::File;
use std::io::Write;
use std::path::Path;
use std::time::Instant;

use bevy::asset::RenderAssetUsages;
use bevy::mesh::MeshVertexBufferLayoutRef;
use bevy::post_process::bloom::Bloom;
use bevy::prelude::*;
use bevy::render::mesh::PrimitiveTopology;
use bevy::render::render_resource::{
    AsBindGroup, RenderPipelineDescriptor, SpecializedMeshPipelineError,
};
use bevy::render::renderer::RenderAdapterInfo;
use bevy::render::settings::{PowerPreference, RenderCreation, WgpuSettings};
use bevy::render::RenderPlugin;
use bevy::shader::ShaderRef as BevyShaderRef;
use bevy::sprite_render::{Material2d, Material2dKey, Material2dPlugin};
use bevy::window::PresentMode;
use bevy_hanabi::prelude::*;
use bevy_hanabi::Gradient as HanabiGradient;
use serde::Serialize;

#[repr(C)]
struct ProcessMemoryCounters {
    cb: u32,
    page_fault_count: u32,
    peak_working_set_size: usize,
    working_set_size: usize,
    quota_peak_paged_pool_usage: usize,
    quota_paged_pool_usage: usize,
    quota_peak_non_paged_pool_usage: usize,
    quota_non_paged_pool_usage: usize,
    pagefile_usage: usize,
    peak_pagefile_usage: usize,
}

#[link(name = "psapi")]
extern "system" {
    fn K32GetProcessMemoryInfo(
        process: *mut std::ffi::c_void,
        counters: *mut ProcessMemoryCounters,
        cb: u32,
    ) -> i32;
    fn GetCurrentProcess() -> *mut std::ffi::c_void;
}

fn get_working_set_mb() -> f64 {
    unsafe {
        let mut pmc: ProcessMemoryCounters = std::mem::zeroed();
        pmc.cb = std::mem::size_of::<ProcessMemoryCounters>() as u32;
        if K32GetProcessMemoryInfo(GetCurrentProcess(), &mut pmc, pmc.cb) != 0 {
            pmc.working_set_size as f64 / (1024.0 * 1024.0)
        } else {
            0.0
        }
    }
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone)]
struct RailMaterial {
    #[uniform(0)]
    time: f32,
    #[uniform(0)]
    center: Vec2,
}

impl Material2d for RailMaterial {
    fn vertex_shader() -> BevyShaderRef {
        "orbit_rail.wgsl".into()
    }

    fn fragment_shader() -> BevyShaderRef {
        "orbit_rail.wgsl".into()
    }

    fn specialize(
        descriptor: &mut RenderPipelineDescriptor,
        _layout: &MeshVertexBufferLayoutRef,
        _key: Material2dKey<Self>,
    ) -> Result<(), SpecializedMeshPipelineError> {
        descriptor.primitive.topology = PrimitiveTopology::PointList;
        Ok(())
    }
}

#[derive(Asset, TypePath, AsBindGroup, Debug, Clone, Default)]
struct CpuPointMaterial {}

impl Material2d for CpuPointMaterial {
    fn vertex_shader() -> BevyShaderRef {
        "orbit_cpu.wgsl".into()
    }

    fn fragment_shader() -> BevyShaderRef {
        "orbit_cpu.wgsl".into()
    }

    fn specialize(
        descriptor: &mut RenderPipelineDescriptor,
        _layout: &MeshVertexBufferLayoutRef,
        _key: Material2dKey<Self>,
    ) -> Result<(), SpecializedMeshPipelineError> {
        descriptor.primitive.topology = PrimitiveTopology::PointList;
        Ok(())
    }
}

#[derive(Resource)]
struct BenchConfig {
    scene: String,
    mode: String,
    points: usize,
    bodies: usize,
    fps_cap: u32,
    has_bloom_and_particles: bool,
    duration_sec: f64,
    warmup_sec: f64,
    out_path: String,
    headless: bool,
}

#[derive(Resource)]
struct OrbitData {
    radii: Vec<f32>,
    theta0: Vec<f32>,
    omega: Vec<f32>,
}

#[derive(Resource)]
struct BodyData {
    radii: Vec<f32>,
    theta0: Vec<f32>,
    omega: Vec<f32>,
}

#[derive(Component)]
struct OrbitMeshMarker;

#[derive(Component)]
struct BodyMeshMarker;

#[derive(Resource)]
struct BenchMetrics {
    elapsed_total: f64,
    last_frame_instant: Option<Instant>,
    frame_times_ms: Vec<f64>,
    cpu_push_times_ms: Vec<f64>,
    rail_mat_handle: Option<Handle<RailMaterial>>,
    mesh_handle: Option<Handle<Mesh>>,
    body_mesh_handle: Option<Handle<Mesh>>,
}

#[derive(Serialize)]
struct BenchResult {
    engine: String,
    engine_version: String,
    scene: String,
    points: usize,
    bodies: usize,
    mode: String,
    fps_cap: u32,
    has_bloom_and_particles: bool,
    frame_count_measured: usize,
    frame_ms_avg: f64,
    frame_ms_p50: f64,
    frame_ms_p95: f64,
    frame_ms_p99: f64,
    fps_avg: f64,
    cpu_push_ms: f64,
    ram_mb: f64,
    vram_mb: f64,
    gpu_adapter_name: String,
    duration_sec: f64,
    warmup_sec: f64,
}

fn parse_args() -> BenchConfig {
    let mut scene = "A".to_string();
    let mut mode = "cpu".to_string();
    let mut duration_sec = 10.0;
    let mut warmup_sec = 2.0;
    let mut out_path = "results_bevy.json".to_string();
    let mut headless = false;
    let mut fps_cap = 0u32;
    let mut override_points = None;
    let mut override_bodies = None;

    for arg in std::env::args() {
        if let Some(val) = arg.strip_prefix("--scene=") {
            scene = val.trim().to_uppercase();
        } else if let Some(val) = arg.strip_prefix("--mode=") {
            mode = val.trim().to_lowercase();
        } else if let Some(val) = arg.strip_prefix("--duration=") {
            if let Ok(v) = val.trim().parse::<f64>() {
                duration_sec = v;
            }
        } else if let Some(val) = arg.strip_prefix("--warmup=") {
            if let Ok(v) = val.trim().parse::<f64>() {
                warmup_sec = v;
            }
        } else if let Some(val) = arg.strip_prefix("--out=") {
            out_path = val.trim().to_string();
        } else if let Some(val) = arg.strip_prefix("--fps-cap=") {
            if let Ok(v) = val.trim().parse::<u32>() {
                fps_cap = v;
            }
        } else if let Some(val) = arg.strip_prefix("--points=") {
            if let Ok(v) = val.trim().parse::<usize>() {
                override_points = Some(v);
            }
        } else if let Some(val) = arg.strip_prefix("--bodies=") {
            if let Ok(v) = val.trim().parse::<usize>() {
                override_bodies = Some(v);
            }
        } else if arg == "--headless" {
            headless = true;
        }
    }

    let (mut points, mut bodies, has_bloom_and_particles) = match scene.as_str() {
        "A" => (100_000, 0, false),
        "B" => (1_000_000, 0, false),
        "C" => (1_000_000, 0, true),
        "D1" | "D_100K" => (100_000, 1_000, false),
        "D2" | "D_300K" => (300_000, 1_000, false),
        "D3" | "D_1M" => (1_000_000, 1_000, false),
        "D" => (100_000, 1_000, false),
        _ => (100_000, 0, false),
    };

    if let Some(p) = override_points {
        points = p;
    }
    if let Some(b) = override_bodies {
        bodies = b;
    }

    BenchConfig {
        scene,
        mode,
        points,
        bodies,
        fps_cap,
        has_bloom_and_particles,
        duration_sec,
        warmup_sec,
        out_path,
        headless,
    }
}

fn init_orbit_data(count: usize) -> (OrbitData, Vec<[f32; 4]>) {
    let mut radii = Vec::with_capacity(count);
    let mut theta0 = Vec::with_capacity(count);
    let mut omega = Vec::with_capacity(count);
    let mut colors = Vec::with_capacity(count);

    let mut seed = 1234u64;
    let mut next_float = || -> f32 {
        seed = seed.wrapping_mul(6364136223846793005).wrapping_add(1442695040888963407);
        ((seed >> 32) as u32 as f64 / 4294967296.0) as f32
    };

    for _ in 0..count {
        let r = 50.0 + next_float() * 450.0;
        let t0 = next_float() * std::f32::consts::PI * 2.0;
        let w = 25.0 / r.sqrt();

        radii.push(r);
        theta0.push(t0);
        omega.push(w);

        let norm_r = (r - 50.0) / 450.0;
        let color = [
            0.4 + 0.6 * (1.0 - norm_r),
            0.5 + 0.5 * norm_r,
            0.8 + 0.2 * next_float(),
            0.85,
        ];
        colors.push(color);
    }

    (OrbitData { radii, theta0, omega }, colors)
}

fn init_body_data(count: usize) -> (BodyData, Vec<[f32; 4]>) {
    let mut radii = Vec::with_capacity(count);
    let mut theta0 = Vec::with_capacity(count);
    let mut omega = Vec::with_capacity(count);
    let mut colors = Vec::with_capacity(count);

    let mut seed = 9999u64;
    let mut next_float = || -> f32 {
        seed = seed.wrapping_mul(6364136223846793005).wrapping_add(1442695040888963407);
        ((seed >> 32) as u32 as f64 / 4294967296.0) as f32
    };

    for _ in 0..count {
        let r = 80.0 + next_float() * 420.0;
        let t0 = next_float() * std::f32::consts::PI * 2.0;
        let w = 22.0 / r.sqrt();

        radii.push(r);
        theta0.push(t0);
        omega.push(w);

        // Major physical bodies (§15): distinct bright gold/white/cyan colors
        let color = [
            0.8 + 0.2 * next_float(),
            0.6 + 0.4 * next_float(),
            0.3 + 0.5 * next_float(),
            1.0,
        ];
        colors.push(color);
    }

    (BodyData { radii, theta0, omega }, colors)
}

fn setup_bench(
    mut commands: Commands,
    config: Res<BenchConfig>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut rail_materials: ResMut<Assets<RailMaterial>>,
    mut cpu_materials: ResMut<Assets<CpuPointMaterial>>,
    mut effects: ResMut<Assets<EffectAsset>>,
    mut metrics: ResMut<BenchMetrics>,
) {
    if !config.headless {
        let mut camera = commands.spawn((
            Camera2d,
            Transform::from_xyz(0.0, 0.0, 0.0),
        ));

        if config.has_bloom_and_particles {
            camera.insert(Bloom::NATURAL);
        }
    }

    let (orbit_data, colors) = init_orbit_data(config.points);
    let mut mesh = Mesh::new(PrimitiveTopology::PointList, RenderAssetUsages::default());

    if config.mode == "shader" {
        let mut pos_data = Vec::with_capacity(config.points);
        for i in 0..config.points {
            pos_data.push([orbit_data.radii[i], orbit_data.theta0[i], orbit_data.omega[i]]);
        }
        mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, pos_data);
        mesh.insert_attribute(Mesh::ATTRIBUTE_COLOR, colors);

        let mesh_h = meshes.add(mesh);
        let mat_h = rail_materials.add(RailMaterial {
            time: 0.0,
            center: Vec2::new(0.0, 0.0),
        });

        commands.spawn((
            Mesh2d(mesh_h.clone()),
            MeshMaterial2d(mat_h.clone()),
            OrbitMeshMarker,
        ));

        metrics.rail_mat_handle = Some(mat_h);
        metrics.mesh_handle = Some(mesh_h);
    } else {
        let mut pos_data = Vec::with_capacity(config.points);
        for i in 0..config.points {
            let r = orbit_data.radii[i];
            let t0 = orbit_data.theta0[i];
            pos_data.push([r * t0.cos(), r * t0.sin(), 0.0]);
        }
        mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, pos_data);
        mesh.insert_attribute(Mesh::ATTRIBUTE_COLOR, colors);

        let mesh_h = meshes.add(mesh);
        let mat_h = cpu_materials.add(CpuPointMaterial::default());

        commands.spawn((
            Mesh2d(mesh_h.clone()),
            MeshMaterial2d(mat_h),
            OrbitMeshMarker,
        ));

        metrics.mesh_handle = Some(mesh_h);
    }

    if config.bodies > 0 {
        let (body_data, body_colors) = init_body_data(config.bodies);
        let mut body_mesh = Mesh::new(PrimitiveTopology::PointList, RenderAssetUsages::default());
        let mut b_pos = Vec::with_capacity(config.bodies);
        for i in 0..config.bodies {
            let r = body_data.radii[i];
            let t0 = body_data.theta0[i];
            b_pos.push([r * t0.cos(), r * t0.sin(), 0.0]);
        }
        body_mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, b_pos);
        body_mesh.insert_attribute(Mesh::ATTRIBUTE_COLOR, body_colors);

        let b_mesh_h = meshes.add(body_mesh);
        let b_mat_h = cpu_materials.add(CpuPointMaterial::default());

        commands.spawn((
            Mesh2d(b_mesh_h.clone()),
            MeshMaterial2d(b_mat_h),
            BodyMeshMarker,
        ));

        metrics.body_mesh_handle = Some(b_mesh_h);
        commands.insert_resource(body_data);
    }

    if config.has_bloom_and_particles && !config.headless {
        let mut glow_mesh = Mesh::new(PrimitiveTopology::PointList, RenderAssetUsages::default());
        let mut glow_pos = Vec::with_capacity(200);
        let mut glow_colors = Vec::with_capacity(200);

        for i in 0..200 {
            let r = 50.0 + (i as f32 * 17.0) % 450.0;
            let a = i as f32 * 0.314;
            glow_pos.push([r * a.cos(), r * a.sin(), 0.0]);
            glow_colors.push([2.0, 4.0, 9.0, 0.4]);
        }
        glow_mesh.insert_attribute(Mesh::ATTRIBUTE_POSITION, glow_pos);
        glow_mesh.insert_attribute(Mesh::ATTRIBUTE_COLOR, glow_colors);
        let glow_mesh_h = meshes.add(glow_mesh);
        let glow_mat_h = cpu_materials.add(CpuPointMaterial::default());

        commands.spawn((
            Mesh2d(glow_mesh_h),
            MeshMaterial2d(glow_mat_h),
        ));

        let mut color_gradient = HanabiGradient::new();
        color_gradient.add_key(0.0, Vec4::new(1.0, 0.6, 0.2, 1.0));
        color_gradient.add_key(1.0, Vec4::new(1.0, 0.1, 0.0, 0.0));

        let writer = ExprWriter::new();
        let init_pos = SetPositionSphereModifier {
            center: writer.lit(Vec3::ZERO).expr(),
            radius: writer.lit(150.0).expr(),
            dimension: ShapeDimension::Volume,
        };

        let effect = EffectAsset::new(
            50_000,
            SpawnerSettings::rate(20_000.0.into()),
            writer.finish(),
        )
        .with_name("explosion")
        .init(init_pos)
        .render(ColorOverLifetimeModifier::new(color_gradient));

        let effect_handle = effects.add(effect);
        commands.spawn((
            ParticleEffect::new(effect_handle),
            Transform::from_translation(Vec3::ZERO),
        ));
    }

    commands.insert_resource(orbit_data);
    println!("[Bevy DrawBench] Ready: Scene {}, Mode {}, Points: {}, Bodies: {}", config.scene, config.mode, config.points, config.bodies);
}

fn bench_tick(
    time: Res<Time>,
    config: Res<BenchConfig>,
    orbit: Option<Res<OrbitData>>,
    bodies: Option<Res<BodyData>>,
    adapter_info: Option<Res<RenderAdapterInfo>>,
    mut metrics: ResMut<BenchMetrics>,
    mut meshes: ResMut<Assets<Mesh>>,
    mut rail_materials: ResMut<Assets<RailMaterial>>,
    mut app_exit: MessageWriter<AppExit>,
) {
    let now = Instant::now();
    let frame_ms = if let Some(last) = metrics.last_frame_instant {
        now.duration_since(last).as_secs_f64() * 1000.0
    } else {
        time.delta_secs_f64() * 1000.0
    };
    metrics.last_frame_instant = Some(now);

    let dt = time.delta_secs_f64();
    metrics.elapsed_total += dt;
    let t = metrics.elapsed_total as f32;

    let push_start = Instant::now();

    if config.mode == "shader" {
        if let Some(mat_h) = &metrics.rail_mat_handle {
            if let Some(mut mat) = rail_materials.get_mut(mat_h) {
                mat.time = t;
            }
        }
    } else if let Some(orbit_data) = orbit {
        if let Some(mesh_h) = &metrics.mesh_handle {
            if let Some(mut mesh) = meshes.get_mut(mesh_h) {
                if let Some(bevy::mesh::VertexAttributeValues::Float32x3(positions)) =
                    mesh.attribute_mut(Mesh::ATTRIBUTE_POSITION)
                {
                    let count = orbit_data.radii.len();
                    for i in 0..count {
                        let r = orbit_data.radii[i];
                        let theta = orbit_data.theta0[i] + orbit_data.omega[i] * t;
                        positions[i][0] = r * theta.cos();
                        positions[i][1] = r * theta.sin();
                    }
                }
            }
        }
    }

    if let Some(body_data) = bodies {
        if let Some(b_mesh_h) = &metrics.body_mesh_handle {
            if let Some(mut mesh) = meshes.get_mut(b_mesh_h) {
                if let Some(bevy::mesh::VertexAttributeValues::Float32x3(positions)) =
                    mesh.attribute_mut(Mesh::ATTRIBUTE_POSITION)
                {
                    let count = body_data.radii.len();
                    for i in 0..count {
                        let r = body_data.radii[i];
                        let theta = body_data.theta0[i] + body_data.omega[i] * t;
                        positions[i][0] = r * theta.cos();
                        positions[i][1] = r * theta.sin();
                    }
                }
            }
        }
    }

    let cpu_push_ms = push_start.elapsed().as_secs_f64() * 1000.0;

    if metrics.elapsed_total >= config.warmup_sec {
        metrics.frame_times_ms.push(frame_ms);
        metrics.cpu_push_times_ms.push(cpu_push_ms);
    }

    if metrics.elapsed_total >= config.duration_sec {
        finish_benchmark(&config, &adapter_info, &metrics);
        app_exit.write(AppExit::Success);
    }

    if config.fps_cap > 0 {
        let target_frame_ms = 1000.0 / config.fps_cap as f64;
        let elapsed_ms = now.elapsed().as_secs_f64() * 1000.0;
        if elapsed_ms < target_frame_ms {
            let sleep_dur = std::time::Duration::from_secs_f64((target_frame_ms - elapsed_ms) / 1000.0);
            std::thread::sleep(sleep_dur);
        }
    }
}

fn finish_benchmark(
    config: &BenchConfig,
    adapter_info: &Option<Res<RenderAdapterInfo>>,
    metrics: &BenchMetrics,
) {
    let count = metrics.frame_times_ms.len();
    let avg_frame_ms = if count > 0 {
        metrics.frame_times_ms.iter().sum::<f64>() / count as f64
    } else {
        0.0
    };

    let mut sorted_frames = metrics.frame_times_ms.clone();
    sorted_frames.sort_by(|a, b| a.partial_cmp(b).unwrap_or(std::cmp::Ordering::Equal));

    let percentile = |p: f64| -> f64 {
        if sorted_frames.is_empty() {
            return 0.0;
        }
        let n = (sorted_frames.len() - 1) as f64 * p;
        let k = n.floor() as usize;
        let d = n - k as f64;
        if k + 1 < sorted_frames.len() {
            sorted_frames[k] + d * (sorted_frames[k + 1] - sorted_frames[k])
        } else {
            sorted_frames[k]
        }
    };

    let p50_frame_ms = percentile(0.50);
    let p95_frame_ms = percentile(0.95);
    let p99_frame_ms = percentile(0.99);

    let avg_cpu_push_ms = if !metrics.cpu_push_times_ms.is_empty() {
        metrics.cpu_push_times_ms.iter().sum::<f64>() / metrics.cpu_push_times_ms.len() as f64
    } else {
        0.0
    };

    let ram_mb = get_working_set_mb();
    let gpu_name = adapter_info
        .as_ref()
        .map(|a| a.name.clone())
        .unwrap_or_else(|| "Headless / Unknown".to_string());

    let result = BenchResult {
        engine: "bevy".to_string(),
        engine_version: "0.19.1".to_string(),
        scene: config.scene.clone(),
        points: config.points,
        bodies: config.bodies,
        mode: config.mode.clone(),
        fps_cap: config.fps_cap,
        has_bloom_and_particles: config.has_bloom_and_particles,
        frame_count_measured: count,
        frame_ms_avg: (avg_frame_ms * 10000.0).round() / 10000.0,
        frame_ms_p50: (p50_frame_ms * 10000.0).round() / 10000.0,
        frame_ms_p95: (p95_frame_ms * 10000.0).round() / 10000.0,
        frame_ms_p99: (p99_frame_ms * 10000.0).round() / 10000.0,
        fps_avg: if avg_frame_ms > 0.0 {
            (1000.0 / avg_frame_ms * 100.0).round() / 100.0
        } else {
            0.0
        },
        cpu_push_ms: (avg_cpu_push_ms * 10000.0).round() / 10000.0,
        ram_mb: (ram_mb * 100.0).round() / 100.0,
        vram_mb: 0.0,
        gpu_adapter_name: gpu_name,
        duration_sec: config.duration_sec,
        warmup_sec: config.warmup_sec,
    };

    let json = serde_json::to_string_pretty(&result).unwrap_or_default();
    println!("[Bevy DrawBench Result]\n{}", json);

    let p = Path::new(&config.out_path);
    if let Some(parent) = p.parent() {
        let _ = std::fs::create_dir_all(parent);
    }
    if let Ok(mut f) = File::create(p) {
        let _ = f.write_all(json.as_bytes());
        println!("Saved result to {}", config.out_path);
    } else {
        eprintln!("Failed to save result to {}", config.out_path);
    }
}

fn main() {
    let config = parse_args();

    let mut app = App::new();

    if config.headless {
        app.add_plugins(MinimalPlugins);
        app.add_plugins(bevy::asset::AssetPlugin::default());
        app.init_asset::<Mesh>();
        app.init_asset::<RailMaterial>();
        app.init_asset::<CpuPointMaterial>();
        app.init_asset::<EffectAsset>();
    } else {
        app.add_plugins(
            DefaultPlugins
                .set(RenderPlugin {
                    render_creation: RenderCreation::Automatic(Box::new(WgpuSettings {
                        power_preference: PowerPreference::HighPerformance,
                        ..default()
                    })),
                    ..default()
                })
                .set(WindowPlugin {
                    primary_window: Some(Window {
                        resolution: (1920u32, 1080u32).into(),
                        present_mode: PresentMode::AutoNoVsync,
                        title: format!(
                            "Bevy DrawBench - Scene {} ({}) - Mode {}",
                            config.scene, config.points, config.mode
                        ),
                        ..default()
                    }),
                    ..default()
                }),
        );
        app.add_plugins(HanabiPlugin);
        app.add_plugins(Material2dPlugin::<RailMaterial>::default());
        app.add_plugins(Material2dPlugin::<CpuPointMaterial>::default());
    }

    app.insert_resource(BenchMetrics {
        elapsed_total: 0.0,
        last_frame_instant: None,
        frame_times_ms: Vec::new(),
        cpu_push_times_ms: Vec::new(),
        rail_mat_handle: None,
        mesh_handle: None,
        body_mesh_handle: None,
    });
    app.insert_resource(config);

    app.add_systems(Startup, setup_bench);
    app.add_systems(Update, bench_tick);

    app.run();
}
