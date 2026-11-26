shader_type canvas_item;

uniform sampler2D recolor_mask : filter_nearest, repeat_disable;
uniform vec4 faction_color : source_color = vec4(1.0, 0.25, 0.25, 1.0);

void fragment()
{
    vec4 base = texture(TEXTURE, UV);
    vec4 mask_sample = texture(recolor_mask, UV);

    // Modulate the neutral shading wherever the mask supplies coverage.
    float mask_strength = clamp(mask_sample.r * mask_sample.a, 0.0, 1.0);
    vec3 recolored = base.rgb * faction_color.rgb;
    // The mask controls recoloring. The source sprite still determines the silhouette.
    COLOR = vec4(mix(base.rgb, recolored, mask_strength), base.a);
}
