// Far scenery around the forest (the mountain ring). Scene fog would flatten everything past fogEnd into one
// colour, so this shader skips it: surfaces are lit by the main light and ambient, then sunk into the scene
// fog colour by height instead of distance. Bases vanish into the mist and meet the fogged near ground
// seamlessly; peaks stay partly visible against the sky.
Shader "Roosevelt/Distant Haze"
{
    Properties
    {
        [MainTexture] _BaseMap ("Atlas", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _MistTop ("Mist Top (hidden below, m)", Float) = 30
        _ClearHeight ("Clear Height (m)", Float) = 150
        _PeakVisibility ("Peak Visibility", Range(0, 1)) = 0.7
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
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
                float _MistTop;
                float _ClearHeight;
                half _PeakVisibility;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float3 positionWS : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half3 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv).rgb * _BaseColor.rgb;
                float3 normal = normalize(input.normalWS);
                Light light = GetMainLight();
                half3 lit = albedo * (SampleSH(normal) + light.color * saturate(dot(normal, light.direction)));
                half visible = saturate((input.positionWS.y - _MistTop) / (_ClearHeight - _MistTop)) * _PeakVisibility;
                return half4(lerp(unity_FogColor.rgb, lit, visible), 1);
            }
            ENDHLSL
        }
    }
}
