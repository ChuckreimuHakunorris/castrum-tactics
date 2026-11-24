shader_type canvas_item;
render_mode unshaded;

#include "res://Shaders/background_gradient.gdshaderinc"

uniform vec4 shape_color : source_color = vec4(0.62, 0.88, 1.0, 1.0);
uniform float animation_speed : hint_range(0.05, 2.0) = 1.0;
uniform float shape_opacity : hint_range(0.0, 0.35) = 0.21;

// Match the project's TIME rollover. Whole cycles make the gradient and
// shape parameters periodic even when a shape is visible at the rollover.
const float LOOP_SECONDS = 3600.0;
const int SHAPE_COUNT = 5;
varying flat vec4 shapes[SHAPE_COUNT]; // center.xy, radius, visibility
varying flat vec2 light_offset;

float hash_value(float seed) {
	vec3 p = fract(vec3(seed) * vec3(0.1031, 0.11369, 0.13787));
	p += dot(p, p.yzx + 19.19);
	return fract((p.x + p.y) * p.z);
}

void vertex() {
	// Speed controls the lifetime and gradient rates. Quantize the number of
	// cycles over the full hour so every speed still joins at TIME rollover.
	float elapsed = TIME / LOOP_SECONDS;
	vec2 gradient_cycles = max(vec2(1.0), round(vec2(80.0, 62.0) * animation_speed));
	// The wider sweep keeps the glow visible in the narrow margins around the board.
	light_offset = sin(TAU * elapsed * gradient_cycles) * vec2(0.32, 0.27);
	for (int i = 0; i < SHAPE_COUNT; i++) {
		float index = float(i);
		float cycles = max(1.0, round(mix(240.0, 450.0, hash_value(index + 11.0)) * animation_speed));
		float cycle_time = elapsed * cycles + hash_value(index + 23.0);
		float age = fract(cycle_time);
		float cycle = floor(cycle_time);
		// Compare whole cycle counts directly to avoid division precision at the seam.
		float seed = cycle - cycles * step(cycles, cycle) + index * 1009.0;
		vec2 origin = vec2(hash_value(seed + 1.0), hash_value(seed + 2.0));
		// Give each edge a light so several fades remain visible around central content.
		if (i < 4) {
			if (i % 2 == 0) {
				float inset = mix(0.07, 0.20, origin.x);
				origin.x = i == 0 ? inset : 1.0 - inset;
			} else {
				float inset = mix(0.07, 0.20, origin.y);
				origin.y = i == 1 ? inset : 1.0 - inset;
			}
		}
		vec2 drift = (vec2(hash_value(seed + 3.0), hash_value(seed + 4.0)) - 0.5) * 0.50;
		float radius = mix(0.20, 0.36, hash_value(seed + 5.0));
		float fade = smoothstep(0.03, 0.23, age) * (1.0 - smoothstep(0.66, 0.96, age));
		shapes[i] = vec4(origin + drift * age, radius, fade * mix(0.70, 1.0, hash_value(seed + 6.0)));
	}
}

void fragment() {
	// The full-viewport rect has the same aspect ratio as the render target.
	float aspect = SCREEN_PIXEL_SIZE.y / SCREEN_PIXEL_SIZE.x;
	vec2 aspect_scale = vec2(max(aspect, 1.0), max(1.0 / aspect, 1.0));
	float illumination = 0.0;
	for (int i = 0; i < SHAPE_COUNT; i++) {
		vec4 shape = shapes[i];
		vec2 delta = abs(UV - shape.xy) * aspect_scale;
		float circle = length(delta);
		float diamond = (delta.x + delta.y) * 0.85;
		float distance_to_center = mix(circle, diamond, float(i % 2) * 0.82);
		illumination += (1.0 - smoothstep(shape.z * 0.12, shape.z, distance_to_center)) * shape.w;
	}

	float strength = min(illumination * shape_opacity, 0.27);
	vec3 color = blue_gradient(UV, light_offset);
	COLOR = vec4(mix(color, shape_color.rgb, strength), 1.0);
}
