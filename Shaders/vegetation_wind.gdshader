shader_type spatial;
render_mode cull_disabled;

// Wind parameters (updated by weather system)
uniform float wind_strength : hint_range(0.0, 1.0) = 0.3;
uniform float wind_speed = 1.0;
uniform vec2 wind_direction = vec2(1.0, 0.5);
uniform float wind_turbulence = 0.5;

// Per-prop parameters
uniform sampler2D albedo_texture : source_color;
uniform float sway_stiffness = 0.5; // 0 = grass (very bendy), 1 = tree (stiff)

varying vec3 world_vertex;

void vertex() {
    world_vertex = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    
    // Only sway vertices that are above ground (based on Y coordinate)
    float sway_factor = smoothstep(0.0, 1.0, VERTEX.y);
    
    // Wind wave calculation
    float wind_wave = sin(TIME * wind_speed + world_vertex.x * 0.5 + world_vertex.z * 0.5) * 0.5 + 0.5;
    
    // Turbulence (gusts)
    float turbulence = sin(TIME * wind_speed * 2.0 + world_vertex.x * 2.0) * wind_turbulence;
    
    // Combined wind effect
    float wind_effect = (wind_wave + turbulence) * wind_strength * sway_factor;
    
    // Apply based on stiffness (grass bends more than trees)
    float bend_amount = wind_effect * (1.0 - sway_stiffness * 0.7);
    
    // Bend in wind direction
    VERTEX.x += wind_direction.x * bend_amount;
    VERTEX.z += wind_direction.y * bend_amount;
    
    // Slight vertical compression when bent (realistic)
    VERTEX.y -= abs(bend_amount) * 0.2;
}

void fragment() {
    vec4 albedo = texture(albedo_texture, UV);
    ALBEDO = albedo.rgb;
    ALPHA = albedo.a;
    
    // Simple lighting
    ROUGHNESS = 0.8;
}