// Endless-ocean surface for dream sequences: flat low-poly PS1 look, no reflection, refraction,
// caustics or screen-space effects. Just a flat-shaded surface pushed up and down by plain sine waves,
// sunk into the scene's ordinary distance fog, with whitecap foam on the tallest crests.
//
// READ THIS BEFORE TOUCHING THE FOAM. An earlier version tinted toward a second colour near each
// wave's peak and had to be removed: peaks of a sine sum land on a perfectly regular lattice (the
// points where all four waves constructively interfere), so tinting by height alone painted that
// lattice in bright dots and exposed a repetition the silhouette by itself never showed. Foam keyed on
// height is therefore a lattice detector, and "make it whiter where it's higher" is exactly the naive
// version that failed.
//
// What makes it safe here is that the threshold is not constant. A drifting value-noise field pushes
// it up and down across the surface (_FoamNoiseStrength), so a crest reaching a given height foams in
// one place and not in another. Foam lands on *some* crests instead of *every* crest — which is both
// what breaks the lattice and what real whitecaps do. Three knobs decide how well that works:
//   _FoamAmount        how high a crest must be before it can foam at all
//   _FoamNoiseStrength how much the threshold wanders; at 0 the lattice comes straight back
//   _FoamNoiseScale    how large the wandering patches are
// Turning _FoamNoiseStrength down to 0, or _FoamAmount up near 1 with no noise, reproduces the
// original bug. After changing any of these, re-shoot the straight-down orthographic capture
// (Logs/Water_Test_top.png) and look at it: an oblique view makes a regular lattice look like
// harmless noise, which is how it got missed the first time.
//
// Foam is fragment-side and purely cosmetic — no C# counterpart, and BoatBuoyancy neither knows nor
// cares about it. The waves keep their single shared definition (see below); foam adds no second
// source of truth. It is evaluated per fragment from world XZ rather than per vertex, which is exact
// here because these waves displace Y only and leave XZ untouched, so foam gets detail finer than the
// 2m water quads without refining the mesh.
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

        // Whitecap foam. Deliberately not pure white: the project's palette is derived from five
        // Mouthwashing fan-extracted colours, and this leans on the pale green (#a6d8af) lightened,
        // so foam reads as "the bright end of this world" rather than a UI white laid over it.
        _FoamColor ("Foam Color", Color) = (0.76, 0.86, 0.84, 1)
        // How much of the sea foams. Drives the height a crest must reach: 0 = only a perfectly
        // constructive peak (essentially never), 1 = most of the upper half of every wave.
        _FoamAmount ("Foam Amount", Range(0, 1)) = 0.5
        // Width of the fade from water to foam, in normalized wave height. Small values give the hard
        // pixel-art edge the rest of the art direction uses; large values give a soft wash.
        _FoamSoftness ("Foam Edge Softness", Range(0.005, 0.5)) = 0.05
        // Size of the patches where foam is more or less likely, as 1/metres. This MUST be fine enough
        // that neighbouring crests get uncorrelated values: the constructive-interference peaks here
        // sit about 14m apart, and the first attempt used 0.08 (12.5m patches), so adjacent crests read
        // almost the same threshold and the grid survived at full strength. 0.22 is ~4.5m patches.
        _FoamNoiseScale ("Foam Patch Scale", Float) = 0.22
        // How far the noise raises the foam threshold. THIS IS THE ANTI-LATTICE KNOB — at 0 every crest
        // of equal height foams identically and the interference grid reappears in full. It has to be
        // large: the lattice peaks all reach nearly the same height, so anything less than roughly the
        // gap between that height and the base threshold suppresses none of them. See header.
        _FoamNoiseStrength ("Foam Patch Strength", Range(0, 1.5)) = 0.9
        // Slow drift of the patches across the water, so foam isn't pinned to fixed world spots.
        _FoamDrift ("Foam Patch Drift (m/s)", Float) = 0.03
        // How far, in metres, foam's view of the surface is bent away from the real one. This is what
        // gets foam off the interference lattice instead of just thinning it. Keep it small against the
        // dominant wavelength or foam stops looking attached to the waves.
        _FoamWarp ("Foam Warp (m)", Float) = 2.0
        // Size of the bending, as 1/metres. Coarse on purpose: this should slowly distort whole
        // stretches of crest, not jitter each fragment.
        _FoamWarpScale ("Foam Warp Scale", Float) = 0.05
        // How hard the fine noise chews the foam edges. 0 gives clean lobes that read as ice floes up
        // close and hand the lattice back to the eye.
        _FoamErosion ("Foam Edge Erosion", Range(0, 1)) = 0.35
        // Size of that chewing, as 1/metres. Fine enough to fray an edge, coarse enough not to boil
        // into per-pixel static in the distance.
        _FoamErosionScale ("Foam Erosion Scale", Float) = 0.35
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
                half4 _FoamColor;
                float _FoamAmount; float _FoamSoftness; float _FoamNoiseScale;
                float _FoamNoiseStrength; float _FoamDrift;
                float _FoamWarp; float _FoamWarpScale;
                float _FoamErosion; float _FoamErosionScale;
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

            // Hash-based value noise for the foam threshold. Deliberately not another sine: the whole
            // problem being solved is that sums of sines repeat on a lattice, so breaking that lattice
            // with more sines would only make a larger one.
            float FoamHash(float2 p)
            {
                p = frac(p * float2(127.1, 311.7));
                p += dot(p, p + 34.23);
                return frac(p.x * p.y);
            }

            float FoamValueNoise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f); // smoothstep interpolation, so patches have no hard cell seams
                float a = FoamHash(i);
                float b = FoamHash(i + float2(1, 0));
                float c = FoamHash(i + float2(0, 1));
                float d = FoamHash(i + float2(1, 1));
                return lerp(lerp(a, b, f.x), lerp(c, d, f.x), f.y);
            }

            // Two octaves at 2.37x, not the usual 2x: an exact octave ladder lines its cells up with
            // itself, which is the same failure mode as round wavelengths a few lines up. The offset
            // keeps the second octave from sharing the first one's cell boundaries.
            float FoamNoise(float2 p)
            {
                return FoamValueNoise(p) * 0.62 + FoamValueNoise(p * 2.37 + 19.3) * 0.38;
            }

            // 0 = water, 1 = foam, at a world XZ and time. Height is normalized by the sum of the wave
            // amplitudes, so the amount of foam survives retuning the waves: reaching 1.0 means every
            // wave peaked at once, which is rare by construction.
            float Foam(float2 xz, float t)
            {
                // Foam reads the wave height at a position pushed around by noise, not at the fragment
                // itself. Suppressing crests is not enough on its own: the only places tall enough to
                // foam ARE the lattice points, so thinning them leaves the survivors still standing on
                // the grid and the eye joins the dots. Bending the sample position a couple of metres
                // moves foam off those points entirely. The warp is small against the 19.7m dominant
                // wavelength, so foam still tracks the swell rather than floating free of it.
                float2 warp = float2(FoamNoise(xz * _FoamWarpScale + 11.7),
                                     FoamNoise(xz * _FoamWarpScale + 53.1)) - 0.5;
                float2 foamXZ = xz + warp * _FoamWarp;

                float maxAmplitude = abs(_Wave1Amplitude) + abs(_Wave2Amplitude)
                                   + abs(_Wave3Amplitude) + abs(_Wave4Amplitude);
                float normalizedHeight = OceanHeight(foamXZ, t) / max(maxAmplitude, 0.001);

                float2 noisePosition = xz * _FoamNoiseScale + float2(t * _FoamDrift, t * _FoamDrift * 0.7);

                // The noise only ever RAISES the threshold, never lowers it. Centring it (patch - 0.5)
                // is the obvious version and it is worse in both directions at once: where it dips it
                // drops the bar below the troughs and washes whole swathes of flat water white, and to
                // suppress enough crests to matter it has to swing so far that those washes are huge.
                // Suppressing foam is the whole job — a crest that would foam anyway needs no help.
                float patch = FoamNoise(noisePosition);
                float threshold = lerp(0.75, 0.05, _FoamAmount) + patch * _FoamNoiseStrength;

                // A finer, centred noise that eats into and spills out of the patch edges. Without it
                // foam comes out as large smooth lobes — close up they read as drifting ice, not foam —
                // and every lobe has the same clean outline, which is most of what lets the eye pick
                // the wave lattice back out of a thinned field. Sampled from the unwarped position so
                // its structure is independent of the warp above rather than bending with it.
                float erosion = (FoamNoise(xz * _FoamErosionScale
                                 + float2(t * _FoamDrift * 1.3, -t * _FoamDrift)) - 0.5) * _FoamErosion;

                return smoothstep(threshold, threshold + _FoamSoftness, normalizedHeight + erosion);
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

                // Foam tints the albedo and is then lit like the water it sits on, rather than being
                // composited over the finished pixel: the flat-shaded facets have to keep reading as
                // facets, and foam pasted on at full brightness after lighting looks like a decal
                // floating above the surface instead of part of it.
                float t = _Time.y + _TimeOffset;
                half3 albedo = lerp(_BaseColor.rgb, _FoamColor.rgb, Foam(input.positionWS.xz, t));

                Light light = GetMainLight();
                half3 lit = albedo * (SampleSH(normalWS) + light.color * saturate(dot(normalWS, light.direction)));
                lit = MixFog(lit, input.fogCoord);
                return half4(lit, 1);
            }
            ENDHLSL
        }
    }
}
