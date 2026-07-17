// URP port of Trash Dash's curved-world unlit shader (CurvedCode.cginc):
// vertex-colored, fogged, and bent downward with distance^2 in clip space.
// _CurveStrength is a GLOBAL float — RaceController sets it (0 outside road mode).
Shader "SummaRace/CurvedVertexColor"
{
    Properties
    {
        _BaseMap ("Base Map", 2D) = "white" {}
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "CurvedUnlit"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST;
                half4 _BaseColor;
            CBUFFER_END

            float _CurveStrength; // global, not per-material

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : TEXCOORD1;
                half   fogCoord   : TEXCOORD2;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);

                // Trash Dash world bend: drop clip-space y by strength * viewDepth^2.
                float dist = output.positionCS.w;
                output.positionCS.y -= _CurveStrength * dist * dist * _ProjectionParams.x;

                output.uv = TRANSFORM_TEX(input.uv, _BaseMap);
                output.color = input.color;
                output.fogCoord = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                half4 color = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, input.uv)
                              * _BaseColor * input.color;
                color.rgb = MixFog(color.rgb, input.fogCoord);
                return color;
            }
            ENDHLSL
        }
    }
}
