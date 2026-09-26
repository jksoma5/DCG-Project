#ifndef DCG_OCCLUSION_DITHER_INCLUDED
#define DCG_OCCLUSION_DITHER_INCLUDED

// Screen-door fade for geometry standing between a fixed side camera and the fighters.
// The fight camera cannot move to see around a wall, so whatever is nearer to it than the fighters
// is thinned out with an ordered dither instead. The pass stays opaque: dithered pixels are simply
// clipped, so sorting, depth and shadows behave exactly as for any other opaque surface.
//
// The values are globals, set by FightCameraRig while it drives a camera. With the rig absent they
// are all zero and the surface renders as plain URP Lit.
float _DCGOcclusionStrength;   // 0 disables the fade; 1 applies it fully
float _DCGOcclusionDepth;      // eye depth beyond which a surface is untouched (fighters minus a margin)
float _DCGOcclusionRange;      // eye distance over which the fade ramps in, in front of that depth
float _DCGOcclusionMinVisible; // share of pixels still drawn at full fade

// 4x4 Bayer matrix, thresholds in the open interval (0, 1).
static const float DCGBayer4x4[16] = {
     0.5 / 16,  8.5 / 16,  2.5 / 16, 10.5 / 16,
    12.5 / 16,  4.5 / 16, 14.5 / 16,  6.5 / 16,
     3.5 / 16, 11.5 / 16,  1.5 / 16,  9.5 / 16,
    15.5 / 16,  7.5 / 16, 13.5 / 16,  5.5 / 16 };

float DCGBayer4(uint2 pixel)
{
    return DCGBayer4x4[(pixel.y & 3) * 4 + (pixel.x & 3)];
}

// positionCS is the fragment's SV_POSITION: xy in pixels, z the hardware depth.
void DCGOcclusionDitherClip(float4 positionCS)
{
    if (_DCGOcclusionStrength <= 0) return;
    float eyeDepth = LinearEyeDepth(positionCS.z, _ZBufferParams);
    float fade = saturate((_DCGOcclusionDepth - eyeDepth) / max(_DCGOcclusionRange, 1e-3));
    float visible = lerp(1, _DCGOcclusionMinVisible, fade * _DCGOcclusionStrength);
    clip(visible - DCGBayer4((uint2)positionCS.xy));
}

#endif
