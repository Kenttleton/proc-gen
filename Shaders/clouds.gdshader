shader_type spatial;
render_mode blend_mix, depth_draw_never, cull_back, unshaded;

uniform sampler2D noise_texture : filter_linear_mipmap, repeat_enable;
uniform float cloud_coverage : hint_range(0.0, 1.0) = 0.5;
uniform float cloud_speed = 0.05;
uniform float time_of_day : hint_range(0.0, 1.0) = 0.5;

varying vec3 world_pos;

float cloud_noise(vec2 uv) {
    vec2 moving_uv = uv + vec2(TIME * cloud_speed, TIME * cloud_speed * 0.5);
    
    float noise1 = texture(noise_texture, moving_uv * 0.5).r;
    float noise2 = texture(noise_texture, moving_uv * 1.0 + vec2(100.0)).r * 0.5;
    float noise3 = texture(noise_texture, moving_uv * 2.0 + vec2(200.0)).r * 0.25;
    
    return noise1 + noise2 + noise3;
}

void vertex() {
    world_pos = (MODEL_MATRIX * vec4(VERTEX, 1.0)).xyz;
}

void fragment() {
    vec2 cloud_uv = world_pos.xz * 0.001;
    
    float cloud_pattern = cloud_noise(cloud_uv);
    cloud_pattern = smoothstep(1.0 - cloud_coverage, 1.0, cloud_pattern);
    
    float detail = texture(noise_texture, cloud_uv * 5.0 + TIME * 0.1).r;
    cloud_pattern *= 0.7 + detail * 0.3;
    
    // Cloud color changes with time of day
    vec3 day_cloud = vec3(1.0, 1.0, 1.0);
    vec3 sunset_cloud = vec3(1.0, 0.6, 0.4);
    vec3 night_cloud = vec3(0.1, 0.1, 0.15);
    
    float is_day = smoothstep(0.3, 0.4, time_of_day) * (1.0 - smoothstep(0.6, 0.7, time_of_day));
    float is_sunset = smoothstep(0.2, 0.3, time_of_day) * smoothstep(0.5, 0.3, time_of_day) +
                      smoothstep(0.5, 0.7, time_of_day) * smoothstep(0.8, 0.7, time_of_day);
    float is_night = 1.0 - is_day - is_sunset;
    
    vec3 cloud_color = day_cloud * is_day + sunset_cloud * is_sunset + night_cloud * is_night;
    
    ALBEDO = cloud_color;
    ALPHA = cloud_pattern * 0.8;
}