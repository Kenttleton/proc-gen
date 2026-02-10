shader_type spatial;
render_mode unshaded, cull_disabled, depth_draw_opaque;

uniform float rain_intensity : hint_range(0.0, 1.0) = 0.5;
uniform float rain_speed = 20.0;
uniform vec3 rain_direction = vec3(0.0, -1.0, 0.1);
uniform vec3 player_position;
uniform float rain_area_size = 50.0;

varying vec3 world_pos;

void vertex() {
    // Create rain around player
    vec3 offset = vec3(
        (VERTEX.x - 0.5) * rain_area_size,
        VERTEX.y * 30.0 - 15.0,  // Rain falls from above
        (VERTEX.z - 0.5) * rain_area_size
    );
    
    // Follow player
    vec3 pos = player_position + offset;
    
    // Animate rain falling
    float time_offset = TIME * rain_speed;
    pos.y -= mod(time_offset + VERTEX.y * 100.0, 30.0);
    
    // Add wind sway
    pos.x += sin(TIME * 2.0 + VERTEX.z * 10.0) * 0.5;
    
    world_pos = pos;
    VERTEX = (inverse(MODEL_MATRIX) * vec4(pos, 1.0)).xyz;
}

void fragment() {
    // Raindrop appearance
    float alpha = rain_intensity * 0.3;
    
    // Fade with distance from camera
    float dist = length(world_pos - player_position);
    alpha *= smoothstep(rain_area_size, rain_area_size * 0.5, dist);
    
    // Raindrop color (slight blue tint)
    vec3 rain_color = vec3(0.7, 0.8, 1.0);
    
    ALBEDO = rain_color;
    ALPHA = alpha;
}