shader_type canvas_item;
render_mode blend_add;

uniform float time;
uniform vec2 center = vec2(960.0, 540.0);

void vertex() {
    float r = INSTANCE_CUSTOM.r;
    float theta0 = INSTANCE_CUSTOM.g;
    float omega = INSTANCE_CUSTOM.b;
    float theta = theta0 + omega * time;
    vec2 pos = center + vec2(r * cos(theta), r * sin(theta));
    VERTEX += pos;
}
