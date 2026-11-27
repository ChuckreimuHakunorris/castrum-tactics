shader_type canvas_item;
#include "legal_frame.gdshaderinc"

void fragment() {
    float pulse = 0.90 + 0.10 * cos(TIME * TAU / 2.5);
    COLOR = legal_frame(UV, pulse);
}
