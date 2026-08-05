// The race's sky dome, repaintable per world (F48).
//
// WHY THIS EXISTS. Trash Dash's sky is a MESH, not a skybox, and its colour is baked into the
// vertices — their shader (Assets/Shaders/VertexColor.shader) returns the vertex colour and has
// no properties at all. There are exactly two domes in the project, Day and NightTime, so eight
// of the ten sessions were showing the same bright blue sky whatever the recipe said, and the
// owner's complaint named the skies specifically.
//
// A multiply tint cannot fix that: multiplying a blue gradient can only ever make a DARKER blue,
// so overcast, misty and golden skies are unreachable. This LERPS towards the world's colour
// instead, which can desaturate and repaint while keeping the dome's own gradient shape at
// (1 - _SkyBlend). Deliberately not fogged and deliberately not curved, exactly like theirs: the
// dome is parented to the player and is the backdrop everything else is drawn against.
Shader "SummaRace/SkyTint"
{
    Properties
    {
        _SkyColor ("Sky Color", Color) = (1, 1, 1, 1)
        _SkyBlend ("Sky Blend", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            Name "SkyTint"
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _SkyColor;
                half _SkyBlend;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                half4  color       : COLOR;
            };

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.color = IN.color;
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                half3 rgb = lerp(IN.color.rgb, _SkyColor.rgb, _SkyBlend);
                return half4(rgb, 1.0h);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
