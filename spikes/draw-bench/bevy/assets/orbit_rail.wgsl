#import bevy_sprite::{
    mesh2d_functions as mesh_functions,
    mesh2d_vertex_output::VertexOutput,
}

struct RailMaterial {
    time: f32,
    center: vec2<f32>,
};

@group(2) @binding(0) var<uniform> material: RailMaterial;

struct Vertex {
    @builtin(instance_index) instance_index: u32,
    @location(0) position: vec3<f32>, // position.x = r, position.y = theta0, position.z = omega
    @location(4) color: vec4<f32>,
};

@vertex
fn vertex(v: Vertex) -> VertexOutput {
    var out: VertexOutput;
    let r = v.position.x;
    let theta0 = v.position.y;
    let omega = v.position.z;
    let theta = theta0 + omega * material.time;
    let local_pos = vec4<f32>(material.center.x + r * cos(theta), material.center.y + r * sin(theta), 0.0, 1.0);
    
    var world_from_local = mesh_functions::get_world_from_local(v.instance_index);
    out.world_position = mesh_functions::mesh2d_position_local_to_world(world_from_local, local_pos);
    out.position = mesh_functions::mesh2d_position_world_to_clip(out.world_position);
    out.color = v.color;
    return out;
}

@fragment
fn fragment(mesh: VertexOutput) -> @location(0) vec4<f32> {
    return mesh.color;
}
