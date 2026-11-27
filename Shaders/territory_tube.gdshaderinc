uniform vec4 player_one_color : source_color;
uniform vec4 player_two_color : source_color;
uniform vec2 tube_size = vec2(200.0, 25.0);
uniform vec2 fill_levels = vec2(0.0);

float boundary_coverage(float distance_to_boundary, float edge, float amount) {
    return amount > 0.0 ? 1.0 - smoothstep(-edge, edge, distance_to_boundary) : 0.0;
}

vec4 territory_tube(vec2 uv, float clock, float motion) {
    vec2 point = uv * tube_size;
    float radius = min(tube_size.y * 0.5, 11.0);
    vec2 rounded = abs(point - tube_size * 0.5) - (tube_size * 0.5 - radius);
    float clip_distance = length(max(rounded, vec2(0.0))) + min(max(rounded.x, rounded.y), 0.0) - radius;
    float rim = 1.0 - smoothstep(-0.5, 0.5, clip_distance);

    float left_end = clamp(fill_levels.x, 0.0, 1.0);
    float right_start = max(left_end, 1.0 - clamp(fill_levels.y, 0.0, 1.0));
    float edge = 0.55 / max(tube_size.x, 1.0);
    float clearance = min(left_end, 1.0 - right_start);
    // Share the displacement at a full boundary. Using the same shift preserves the gap at every height.
    float wave = sin(uv.y * TAU * 0.72 + clock) + 0.34 * sin(uv.y * TAU * 1.15 - clock * 0.7);
    float offset = wave * min(1.8 / max(tube_size.x, 1.0), max(clearance, 0.0) * 0.32) * motion;
    float one = boundary_coverage(uv.x - left_end - offset, edge, left_end);
    float two = boundary_coverage(right_start + offset - uv.x, edge, fill_levels.y);
    if (left_end >= 1.0) one = 1.0;
    if (fill_levels.y >= 1.0) two = 1.0;
    two = min(two, 1.0 - one);

    float glass = 0.55 + 0.12 * (1.0 - uv.y);
    vec3 tint = one * player_one_color.rgb + two * player_two_color.rgb;
    float color_weight = one + two;
    return vec4(tint / max(color_weight, 0.0001), color_weight * glass * rim);
}
