shader_type canvas_item;
#include "territory_tube.gdshaderinc"

void fragment() {
    COLOR = territory_tube(UV, 0.0, 0.0);
}
