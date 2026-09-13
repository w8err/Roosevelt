// Endless-ocean surface for dream sequences: flat low-poly PS1 look, no reflection, refraction, foam,
// caustics or screen-space effects. Just a flat-shaded surface pushed up and down by plain sine waves,
// sunk into the scene's ordinary distance fog. No crest highlight: an earlier version tinted toward a
// second colour near each wave's peak, but peaks of a sine sum land on a perfectly regular lattice (the
// points where all waves constructively interfere) — the tint painted that lattice in bright dots and
// made a repetition the silhouette alone didn't show. Flat colour only.
//
// Height is a sum of a handful of plain sine waves (not Gerstner: no horizontal displacement), kept
// deliberately simple so this exact formula can be re-evaluated in C# later to bob a boat riding on
// top. For wave i, with direction d_i (a world XZ vector, normalized before use), wavelength L_i,
// amplitude A_i and speed S_i (in metres/second):
//   k_i = 2*PI / L_i
//   height += A_i * sin(dot(normalize(d_i), worldXZ) * k_i + t * S_i * k_i)
// t is _Time.y plus the (usually zero) _TimeOffset property. _Time.y equals Unity's
// Time.timeSinceLevelLoad, not Time.time — a C# reproduction must read the same clock (plus whatever
// _TimeOffset that material was given) or it will drift out of sync with the surface under it.
//
// Tuning the waves without bringing the tiling back: any two wavelengths whose ratio is a small
// rational number (2:1, 3:2, ...) realign every few wavelengths, and any two directions a small number
// of degrees apart (0/45/90) reinforce along the same axes — both read as a grid once the whole patch
// is visible. Keep wavelengths and direction angles irregular relative to each other (this shader's
// defaults: 19.7/13.7/10.3/7.9m at 0/37/101/149 degrees) rather than round numbers or clean angles.
//
// The shortest wavelength also has a second floor: anything riding the surface (the rowboat, 2.42m
// long) needs to span only a small fraction of it, or the water under the hull is too curved for the
// hull's own tilt to approximate — keep the shortest wavelength at least ~3x the longest rigid object
// that will float on it.
Shader "Roosevelt/Ocean"
{
    Properties
    {
        [MainColor] _BaseColor ("Water Color", Color) = (0.09, 0.24, 0.30, 1)
        // Added to _Time.y before evaluating the waves. Gameplay leaves this at 0 (real time already
        // advances it); the test-scene builder scrubs it to grab a few frames apart without needing
        // edit-mode time to actually be running.
        _TimeOffset ("Time Offset (s)", Float) = 0

        // Dominant swell: sets the main silhouette direction.
        _Wave1Direction ("Wave 1 Direction (XZ)", Vector) = (1, 0, 0, 0)
        _Wave1Wavelength ("Wave 1 Wavelength (m)", Float) = 19.7
        _Wave1Amplitude ("Wave 1 Amplitude (m)", Float) = 0.35
        _Wave1Speed ("Wave 1 Speed (m/s)", Float) = 2.0

        // Secondary swell, crossing the first at a non-right angle.
        _Wave2Direction ("Wave 2 Direction (XZ)", Vector) = (0.7986, 0.6018, 0, 0)
        _Wave2Wavelength ("Wave 2 Wavelength (m)", Float) = 13.7
        _Wave2Amplitude ("Wave 2 Amplitude (m)", Float) = 0.20
        _Wave2Speed ("Wave 2 Speed (m/s)", Float) = 1.3

        // Breakers: small enough, and irregular enough relative to 1/2, to break up the regularity the
        // two big swells would otherwise show once several wavelengths are in view.
        _Wave3Direction ("Wave 3 Direction (XZ)", Vector) = (-0.1908, 0.9816, 0, 0)
        _Wave3Wavelength ("Wave 3 Wavelength (m)", Float) = 10.3
        _Wave3Amplitude ("Wave 3 Amplitude (m)", Float) = 0.09
        _Wave3Speed ("Wave 3 Speed (m/s)", Float) = 1.7

        _Wave4Direction ("Wave 4 Direction (XZ)", Vector) = (-0.8572, 0.5150, 0, 0)
        _Wave4Wavelength ("Wave 4 Wavelength (m)", Float) = 7.9
        _Wave4Amplitude ("Wave 4 Amplitude (m)", Float) = 0.05
        _Wave4Speed ("Wave 4 Speed (m/s)", Float) = 2.6
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                float _TimeOffset;
                float4 _Wave1Direction; float _Wave1Wavelength; float _Wave1Amplitude; float _Wave1Speed;
                float4 _Wave2Direction; float _Wave2Wavelength; float _Wave2Amplitude; float _Wave2Speed;
                float4 _Wave3Direction; float _Wave3Wavelength; float _Wave3Amplitude; float _Wave3Speed;
                float4 _Wave4Direction; float _Wave4Wavelength; float _Wave4Amplitude; float _Wave4Speed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float fogCoord : TEXCOORD1;
            };

            // One wave's contribution to surface height at world XZ and time t. See the formula in
            // the header comment above — keep this in lockstep with any C# reimplementation.
            float Wave(float2 xz, float2 dir, float wavelength, float amplitude, float speed, float t)
            {
                float k = TWO_PI / max(wavelength, 0.001);
                float phase = dot(normalize(dir), xz) * k + t * speed * k;
                return amplitude * sin(phase);
            }

            float OceanHeight(float2 xz, float t)
            {
                float h = 0;
                h += Wave(xz, _Wave1Direction.xy, _Wave1Wavelength, _Wave1Amplitude, _Wave1Speed, t);
                h += Wave(xz, _Wave2Direction.xy, _Wave2Wavelength, _Wave2Amplitude, _Wave2Speed, t);
                h += Wave(xz, _Wave3Direction.xy, _Wave3Wavelength, _Wave3Amplitude, _Wave3Speed, t);
                h += Wave(xz, _Wave4Direction.xy, _Wave4Wavelength, _Wave4Amplitude, _Wave4Speed, t);
                return h;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float t = _Time.y + _TimeOffset;
                positionWS.y += OceanHeight(positionWS.xz, t);
                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                // Flat/faceted shading on purpose (PS1 low-poly look): the normal comes from how world
                // position changes across the triangle being rasterized (screen-space derivatives),
                // not a smoothed per-vertex normal, so every triangle reads as one flat facet.
                float3 dPdx = ddx(input.positionWS);
                float3 dPdy = ddy(input.positionWS);
                float3 normalWS = normalize(cross(dPdy, dPdx));
                if (normalWS.y < 0) normalWS = -normalWS;

                Light light = GetMainLight();
                half3 lit = _BaseColor.rgb * (SampleSH(normalWS) + light.color * saturate(dot(normalWS, light.direction)));
                lit = MixFog(lit, input.fogCoord);
                return half4(lit, 1);
            }
            ENDHLSL
        }
    }
}
