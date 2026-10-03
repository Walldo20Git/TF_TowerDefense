// Toon shader con contorno para URP (GDD 8: estilo anime / cel shading).
// Pase 1: luz en dos tonos con la luz principal. Pase 2: contorno por casco invertido en espacio de pantalla.
Shader "ConcertDefense/Toon"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1, 1, 1, 1)
        _ShadeColor ("Color de sombra", Color) = (0.55, 0.6, 0.8, 1)
        _EmissionColor ("Emision", Color) = (0, 0, 0, 1)
        _RampThreshold ("Umbral de sombra", Range(0, 1)) = 0.45
        _RampSmooth ("Suavizado", Range(0.001, 0.5)) = 0.04
        _OutlineColor ("Color de contorno", Color) = (0.02, 0.03, 0.07, 1)
        _OutlineWidth ("Grosor de contorno (px)", Range(0, 8)) = 2
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            half4 _ShadeColor;
            half4 _EmissionColor;
            half _RampThreshold;
            half _RampSmooth;
            half4 _OutlineColor;
            half _OutlineWidth;
        CBUFFER_END
        ENDHLSL

        Pass
        {
            Name "ToonForward"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                half3 normalWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                Light mainLight = GetMainLight();
                half ndl = dot(normalize(input.normalWS), mainLight.direction) * 0.5 + 0.5;
                half ramp = smoothstep(_RampThreshold - _RampSmooth, _RampThreshold + _RampSmooth, ndl);

                // La estimacion de luz de ARCore cambia el color e intensidad de la luz principal
                half3 lightTint = clamp(mainLight.color, 0.35, 1.5);
                half3 color = _BaseColor.rgb * lerp(_ShadeColor.rgb, half3(1, 1, 1), ramp) * lightTint;
                color += _EmissionColor.rgb;
                return half4(color, 1);
            }
            ENDHLSL
        }

        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }
            Cull Front

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                float4 positionCS = TransformObjectToHClip(input.positionOS.xyz);
                float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
                float3 normalCS = TransformWorldToHClipDir(normalWS);

                // Grosor constante en pixeles, sea cual sea la escala del campo
                float2 dir = normalCS.xy;
                float len = max(length(dir), 0.0001);
                dir /= len;
                positionCS.xy += dir * (_OutlineWidth * 2.0 / _ScreenParams.xy) * positionCS.w;

                output.positionCS = positionCS;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                return half4(_OutlineColor.rgb, 1);
            }
            ENDHLSL
        }
    }

    FallBack "Universal Render Pipeline/Unlit"
}
