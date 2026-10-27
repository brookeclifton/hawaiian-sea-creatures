#ifndef REEF_SWIM_INCLUDED
#define REEF_SWIM_INCLUDED

// Procedural swimming motion for fish that have no rig/animation.
// Bends the mesh side to side (object-space X) with a travelling wave that grows toward the tail.
//
// The including shader must declare these in its UnityPerMaterial CBUFFER:
//   float _SwimAmplitude;  // tail swing as a fraction of body length (0 = off)
//   float _SwimSpeed;      // tail beats, radians per second
//   float _SwimWaves;      // how many waves travel along the body
//   float _SwimHeadZ;      // object-space Z of the nose   (model faces +Z)
//   float _SwimTailZ;      // object-space Z of the tail tip
//   float _SwimTailBias;   // >1 = only the back of the fish bends
//   float _SwimSway;       // whole-body yaw sway
//   float _SwimVertical;   // 0 = side-to-side (fish, sharks), 1 = up-and-down (whales, dolphins)
//
// Each fish gets its own phase from its scale, so a school (random scales) doesn't swim in lockstep.

float ReefSwimPhase()
{
    float4x4 m = GetObjectToWorldMatrix();
    float s = length(float3(m._m00, m._m10, m._m20));
    return frac(s * 37.1373) * 6.2831853;
}

// Returns the X offset and its derivative along Z for a given object-space Z.
void ReefSwimOffset(float z, out float offsetX, out float dOffsetDz)
{
    float len = max(_SwimHeadZ - _SwimTailZ, 1e-3);
    float along = saturate((_SwimHeadZ - z) / len);          // 0 at the nose, 1 at the tail
    float mask = pow(along, max(_SwimTailBias, 0.01));
    float t = _Time.y * _SwimSpeed + ReefSwimPhase();
    float k = _SwimWaves * 6.2831853;
    float phase = t - along * k;
    float amp = _SwimAmplitude * len;

    // Travelling tail wave + a gentle counter-sway of the whole body around a point 1/3 back.
    offsetX = sin(phase) * amp * mask - sin(t) * _SwimSway * len * (along - 0.33);

    // d/dz (along) = -1/len. Mask derivative ignored (small).
    dOffsetDz = (cos(phase) * amp * mask * k - sin(t) * _SwimSway * len * -1.0) * (1.0 / len);
}

void ReefApplySwim(inout float3 positionOS, inout float3 normalOS)
{
    float offsetX, dxdz;
    ReefSwimOffset(positionOS.z, offsetX, dxdz);
    float v = saturate(_SwimVertical);
    positionOS.x += offsetX * (1.0 - v);
    positionOS.y += offsetX * v;
    // Displacement along x (or y) += f(z)  =>  normal.z -= f'(z) * normal.x (or normal.y)
    normalOS = normalize(float3(normalOS.x, normalOS.y, normalOS.z - dxdz * lerp(normalOS.x, normalOS.y, v)));
}

void ReefApplySwimPosition(inout float3 positionOS)
{
    float offsetX, dxdz;
    ReefSwimOffset(positionOS.z, offsetX, dxdz);
    float v = saturate(_SwimVertical);
    positionOS.x += offsetX * (1.0 - v);
    positionOS.y += offsetX * v;
}

#endif
