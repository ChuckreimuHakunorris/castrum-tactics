shader_type canvas_item;
#include "legal_frame.gdshaderinc"

void fragment() {
    COLOR = legal_frame(UV, 1.0);
}
