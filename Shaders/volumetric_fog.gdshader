shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded;

uniform float fog_density : hint_range(0.0, 1.0) = 0.3;
uniform vec3 fog_color : source_color = vec3(0.7, 0.75, 0.8);
uniform float fog_height = 20.0;
uniform float fog_falloff = 5.0;
uniform vec3 sun_direction = vec3(0.3, 0.5, 0.2);
uniform vec3 sun_color : source_color = vec3(1.0, 0.95, 0.8);
uniform float sun_scatter = 0.5;

varying vec3 world_pos;
varying vec3 camera_dir;

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    vec3 camera_pos = (inverse(VIEW_MATRIX) * vec4(0.0, 0.0, 0.0, 1.0)).xyz;
    camera_dir = normalize(world_pos - camera_pos);
}

void fragment() {
    // Height-based fog density
    float height_factor = smoothstep(fog_height + fog_falloff, fog_height - fog_falloff, world_pos.y);
    
    // Distance fog
    float dist = length(world_pos - (inverse(VIEW_MATRIX) * vec4(0.0, 0.0, 0.0, 1.0)).xyz);
    float dist_fog = 1.0 - exp(-dist * 0.02);
    
    // Sun scattering (god rays effect)
    float sun_dot = max(dot(camera_dir, sun_direction), 0.0);
    float scatter = pow(sun_dot, 8.0) * sun_scatter;
    
    vec3 final_color = mix(fog_color, sun_color, scatter);
    
    float final_density = fog_density * height_factor * dist_fog;
    
    ALBEDO = final_color;
    ALPHA = final_density;
}