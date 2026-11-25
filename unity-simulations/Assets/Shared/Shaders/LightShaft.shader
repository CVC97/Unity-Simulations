// A beam of light made visible (dust in the light), for an open cone mesh around the beam: additive, both faces, no
// depth write. Each face adds light in proportion to |n·v|: drawn front and back, that is the path length through a
// uniformly lit cylinder of dust, so the beam is brightest along its middle and fades to nothing at its silhouette
// instead of ending in a hard edge (the URP Unlit cone of 2026-11-25 read as a solid column). _EdgePower > 1 softens
// the edges further. UV v runs from 0 at the light to 1 at the floor; the beam fades upwards as v^_FadePower.
// First used by the Foucault-Pendel's oculus (FoucaultSceneSetup).
Shader "Noir/LightShaft" {
    Properties {
        _Color ("Colour", Color) = (1, 1, 1, 1)
        _Intensity ("Intensity", Float) = 0.025
        _EdgePower ("Edge softness (power of |n·v|)", Float) = 1.5
        _FadePower ("Fade upwards (power of v)", Float) = 5
    }
    SubShader {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        Pass {
            Name "LightShaft"
            Tags { "LightMode" = "UniversalForward" }
            Blend One One
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                half _Intensity;
                half _EdgePower;
                half _FadePower;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct Varyings {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 viewDirWS : TEXCOORD1;
                float v : TEXCOORD2;
            };

            Varyings Vert(Attributes input) {
                Varyings output;
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.viewDirWS = GetCameraPositionWS() - position.positionWS;
                output.v = input.uv.y;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target {
                half facing = abs(dot(normalize(input.normalWS), normalize(input.viewDirWS)));
                half fade = pow(saturate(input.v), _FadePower);
                half brightness = _Intensity * fade * pow(facing, _EdgePower);
                return half4(_Color.rgb * brightness, 0);
            }
            ENDHLSL
        }
    }
}
