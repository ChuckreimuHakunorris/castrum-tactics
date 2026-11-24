shader_type canvas_item;
render_mode unshaded;

#include "res://Shaders/background_gradient.gdshaderinc"

void fragment() {
	COLOR = vec4(blue_gradient(UV, vec2(0.0)), 1.0);
}
