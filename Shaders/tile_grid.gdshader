shader_type canvas_item;

uniform vec2 grid_size = vec2(1.0);
uniform vec4 grid_color : source_color = vec4(0.094, 0.125, 0.051, 0.333);

void fragment()
{
    vec2 cell = UV * grid_size;
    vec2 cell_units_per_pixel = fwidth(cell);
    float shared_width = max(1.0, 0.04 / max(cell_units_per_pixel.x, cell_units_per_pixel.y));
    float frame_width = max(1.0, shared_width * 0.5);

    // Sum-of-coverage across adjacent pixels stays stable at fractional grid positions.
    vec2 interior_line = clamp(round(cell), vec2(1.0), max(grid_size - 1.0, vec2(1.0)));
    vec2 interior_distance = abs(cell - interior_line) / cell_units_per_pixel;
    vec2 interior = clamp(shared_width * 0.5 + 0.5 - interior_distance, 0.0, 1.0);
    interior *= step(vec2(2.0), grid_size);

    vec2 frame_distance = min(cell, grid_size - cell) / cell_units_per_pixel;
    vec2 frame = clamp(frame_width + 0.5 - frame_distance, 0.0, 1.0);

    // Composite the entire grid once so crossings do not darken.
    float coverage = max(max(interior.x, interior.y), max(frame.x, frame.y));
    COLOR = vec4(grid_color.rgb, grid_color.a * coverage);
}
