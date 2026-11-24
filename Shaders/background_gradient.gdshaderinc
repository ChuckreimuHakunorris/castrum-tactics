uniform vec4 deep_blue : source_color = vec4(0.051, 0.098, 0.220, 1.0);
uniform vec4 royal_blue : source_color = vec4(0.141, 0.353, 0.680, 1.0);
uniform vec4 bright_blue : source_color = vec4(0.290, 0.610, 0.980, 1.0);

vec3 blue_gradient(vec2 uv, vec2 light_offset) {
	vec2 light = vec2(0.72, 0.40) + light_offset;
	float glow = 1.0 - smoothstep(0.02, 0.91, length((uv - light) * vec2(0.9, 1.0)));
	float diagonal = smoothstep(0.0, 1.35, uv.x + (1.0 - uv.y) * 0.52);
	vec3 base = mix(deep_blue.rgb, royal_blue.rgb, 0.16 + diagonal * 0.54);
	return mix(base, bright_blue.rgb, glow * 0.60);
}
