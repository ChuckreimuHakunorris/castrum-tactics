shader_type canvas_item;

uniform vec4 indicator_color : source_color = vec4(1.0, 0.84, 0.25, 1.0);
uniform float border_width : hint_range(0.0, 0.15) = 0.045;
uniform float fade_depth : hint_range(0.01, 0.5) = 0.20;
uniform float fade_opacity : hint_range(0.0, 1.0) = 0.42;

void fragment()
{
    vec2 edge_distance = min(UV, vec2(1.0) - UV);
    float distance_to_edge = min(edge_distance.x, edge_distance.y);

    // A single distance field keeps the corners from accumulating opacity.
    float border = 1.0 - step(border_width, distance_to_edge);
    float gradient = 1.0 - smoothstep(border_width, border_width + fade_depth, distance_to_edge);
    COLOR = vec4(indicator_color.rgb, indicator_color.a * max(border, gradient * fade_opacity));
}
