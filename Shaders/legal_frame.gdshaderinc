uniform vec4 active_color : source_color;
uniform vec4 opposing_color : source_color;
varying float target_kind;

void vertex() {
    // Red stores a category per quad, letting all targets share one material and clock.
    target_kind = COLOR.r;
}

vec4 legal_frame(vec2 uv, float pulse) {
    float near_edge = min(min(uv.x, 1.0 - uv.x), min(uv.y, 1.0 - uv.y));
    // Keep the grid-side inset and halve the stroke width measured at half opacity.
    float shape = smoothstep(0.012, 0.027, near_edge) * (1.0 - smoothstep(0.05075, 0.06875, near_edge));
    // The nearest pair of edges meets on the corner bisectors, with no overlapping alpha.
    float far_edge = min(1.0 - uv.x, 1.0 - uv.y);
    float top_left_edge = min(uv.x, uv.y);
    bool active = target_kind < 0.375 || (target_kind > 0.625 && top_left_edge <= far_edge);
    return vec4(active ? active_color.rgb : opposing_color.rgb, shape * pulse);
}
