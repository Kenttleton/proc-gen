shader_type sky;

// Time of day (0.0 = midnight, 0.5 = noon, 1.0 = midnight)
uniform float time_of_day : hint_range(0.0, 1.0) = 0.5;

// Day colors
uniform vec3 day_top_color : source_color = vec3(0.1, 0.3, 0.6);
uniform vec3 day_horizon_color : source_color = vec3(0.6, 0.7, 0.8);

// Sunset/sunrise colors
uniform vec3 sunset_top_color : source_color = vec3(0.3, 0.2, 0.4);
uniform vec3 sunset_horizon_color : source_color = vec3(1.0, 0.5, 0.3);

// Night colors
uniform vec3 night_top_color : source_color = vec3(0.01, 0.01, 0.05);
uniform vec3 night_horizon_color : source_color = vec3(0.05, 0.05, 0.15);

// Sun/Moon
uniform vec3 sun_color : source_color = vec3(1.0, 0.95, 0.8);
uniform vec3 moon_color : source_color = vec3(0.8, 0.85, 1.0);
uniform float sun_size = 0.04;
uniform float moon_size = 0.03;

// Stars
uniform sampler2D stars_texture : filter_nearest, repeat_enable;
uniform float stars_intensity : hint_range(0.0, 1.0) = 1.0;

// Weather
uniform float weather_darkness : hint_range(0.0, 1.0) = 0.0;

// Calculate sun direction based on time
vec3 get_sun_direction() {
    float angle = time_of_day * 3.14159 * 2.0;
    return vec3(sin(angle) * 0.3, cos(angle), 0.2);
}

vec3 get_moon_direction() {
    return -get_sun_direction();
}

// Simple star pattern
float get_stars(vec3 direction) {
    // Only show stars when looking up
    if (direction.y < 0.0) return 0.0;
    
    // Sample star texture based on sky direction
    vec2 uv = vec2(
        atan(direction.x, direction.z) / 6.28318 + 0.5,
        acos(direction.y) / 3.14159
    );
    
    float star = texture(stars_texture, uv * 10.0).r;
    
    // Make stars twinkle
    float twinkle = sin(TIME * 2.0 + star * 100.0) * 0.5 + 0.5;
    star *= twinkle * 0.5 + 0.5;
    
    return star;
}

void sky() {
    vec3 direction = normalize(EYEDIR);
    
    // Calculate time-based blending factors
    float is_day = smoothstep(0.2, 0.3, time_of_day) * (1.0 - smoothstep(0.7, 0.8, time_of_day));
    float is_sunset = smoothstep(0.15, 0.25, time_of_day) * smoothstep(0.35, 0.25, time_of_day) +
                      smoothstep(0.65, 0.75, time_of_day) * smoothstep(0.85, 0.75, time_of_day);
    float is_night = smoothstep(0.8, 0.9, time_of_day) + (1.0 - smoothstep(0.1, 0.2, time_of_day));
    
    // Sky gradient
    float sky_gradient = 1.0 - abs(direction.y);
    
    vec3 day_sky = mix(day_top_color, day_horizon_color, sky_gradient);
    vec3 sunset_sky = mix(sunset_top_color, sunset_horizon_color, pow(sky_gradient, 0.5));
    vec3 night_sky = mix(night_top_color, night_horizon_color, sky_gradient);
    
    // Blend between time periods
    vec3 sky_color = day_sky * is_day + sunset_sky * is_sunset + night_sky * is_night;
    
    // Normalize if needed
    float total = is_day + is_sunset + is_night;
    if (total > 0.0) {
        sky_color /= total;
    }
    
    // Apply weather darkness
    sky_color = mix(sky_color, sky_color * 0.3, weather_darkness);
    
    // Sun
    vec3 sun_dir = get_sun_direction();
    float sun_dot = dot(direction, sun_dir);
    float sun_mask = smoothstep(1.0 - sun_size * 2.0, 1.0 - sun_size, sun_dot);
    float sun_glow = pow(max(sun_dot, 0.0), 8.0) * 0.3 * is_day;
    
    // Only show sun during day
    float sun_visibility = smoothstep(0.0, 0.1, sun_dir.y);
    vec3 sun_contrib = (sun_mask + sun_glow) * sun_color * sun_visibility;
    
    // Moon
    vec3 moon_dir = get_moon_direction();
    float moon_dot = dot(direction, moon_dir);
    float moon_mask = smoothstep(1.0 - moon_size * 2.0, 1.0 - moon_size, moon_dot);
    float moon_glow = pow(max(moon_dot, 0.0), 16.0) * 0.2;
    
    // Only show moon during night
    float moon_visibility = smoothstep(0.0, 0.1, moon_dir.y) * is_night;
    vec3 moon_contrib = (moon_mask + moon_glow) * moon_color * moon_visibility;
    
    // Stars (only visible at night)
    float stars = get_stars(direction) * is_night * stars_intensity;
    stars *= smoothstep(0.2, 0.0, weather_darkness); // Hide stars in bad weather
    
    // Combine everything
    vec3 final_color = sky_color + sun_contrib + moon_contrib + vec3(stars);
    
    COLOR = final_color;
}