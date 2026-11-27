shader_type canvas_item;
#include "territory_tube.gdshaderinc"

void fragment() {
    COLOR = territory_tube(UV, TIME * TAU / 4.0, 1.0);
}
