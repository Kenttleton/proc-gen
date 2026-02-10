shader_type spatial;

uniform sampler2D grass_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D rock_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D snow_texture : source_color, filter_linear_mipmap, repeat_enable;
uniform sampler2D dirt_texture : source_color, filter_linear_mipmap, repeat_enable;

uniform float grass_height_min = -5.0;
uniform float grass_height_max = 3.0;
uniform float rock_height_min = 2.0;
uniform float rock_height_max = 8.0;
uniform float snow_height_min = 7.0;

uniform float texture_scale = 0.1;
uniform float slope_threshold = 0.7;

varying float vertex_height;
varying vec3 world_position;
varying vec3 world_normal;

void vertex() {
    vertex_height = VERTEX.y;
    world_position = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
    world_normal = normalize((MODEL_MATRIX * vec4(NORMAL, 0.0)).xyz);
}

// Triplanar texture sampling
vec4 triplanar_texture(sampler2D tex, vec3 pos, vec3 normal, float scale) {
    vec3 blend_weights = abs(normal);
    blend_weights = blend_weights / (blend_weights.x + blend_weights.y + blend_weights.z);
    
    vec4 col_x = texture(tex, pos.yz * scale);
    vec4 col_y = texture(tex, pos.xz * scale);
    vec4 col_z = texture(tex, pos.xy * scale);
    
    return col_x * blend_weights.x + col_y * blend_weights.y + col_z * blend_weights.z;
}

void fragment() {
    float slope = 1.0 - abs(NORMAL.y);
    
    // Sample textures using triplanar mapping
    vec4 grass_col = triplanar_texture(grass_texture, world_position, world_normal, texture_scale * 2.0);
    vec4 rock_col = triplanar_texture(rock_texture, world_position, world_normal, texture_scale);
    vec4 snow_col = triplanar_texture(snow_texture, world_position, world_normal, texture_scale);
    vec4 dirt_col = triplanar_texture(dirt_texture, world_position, world_normal, texture_scale * 1.5);
    
    float grass_blend = smoothstep(grass_height_min, grass_height_max, vertex_height);
    grass_blend *= smoothstep(grass_height_max + 2.0, grass_height_max, vertex_height);
    
    float rock_blend = smoothstep(rock_height_min, rock_height_max, vertex_height);
    rock_blend *= smoothstep(rock_height_max + 3.0, rock_height_max, vertex_height);
    
    float snow_blend = smoothstep(snow_height_min, snow_height_min + 2.0, vertex_height);
    
    float slope_rock = smoothstep(slope_threshold - 0.1, slope_threshold + 0.1, slope);
    rock_blend = max(rock_blend, slope_rock);
    
    float dirt_blend = smoothstep(grass_height_min + 1.0, grass_height_min, vertex_height);
    
    float total = grass_blend + rock_blend + snow_blend + dirt_blend;
    if (total > 0.0) {
        grass_blend /= total;
        rock_blend /= total;
        snow_blend /= total;
        dirt_blend /= total;
    }
    
    vec3 final_color = vec3(0.0);
    final_color += grass_col.rgb * grass_blend;
    final_color += rock_col.rgb * rock_blend;
    final_color += snow_col.rgb * snow_blend;
    final_color += dirt_col.rgb * dirt_blend;
    
    ALBEDO = final_color;
    
    ROUGHNESS = 0.8 * grass_blend + 0.6 * rock_blend + 0.3 * snow_blend + 0.9 * dirt_blend;
}