Shader "UJA/World Projected Path"
{
    Properties
    {
        _BaseMap("Base Map", 2D) = "white" {}
        _BaseColor("Base Color", Color) = (1, 1, 1, 1)
        _WorldTiling("Repeats Per World Unit", Float) = 0.5
        _Rotation("Rotation (Degrees)", Range(0, 360)) = 0
        _Smoothness("Smoothness", Range(0, 1)) = 0.15
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
            };

            TEXTURE2D(_BaseMap);
            SAMPLER(sampler_BaseMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BaseColor;
                float _WorldTiling;
                float _Rotation;
                float _Smoothness;
            CBUFFER_END

            Varyings Vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                return output;
            }

            float2 WorldUV(float2 worldXZ)
            {

                float angle = radians(_Rotation);
                float sine = sin(angle);
                float cosine = cos(angle);

                float2 rotated = float2(
                    worldXZ.x * cosine - worldXZ.y * sine,
                    worldXZ.x * sine + worldXZ.y * cosine
                );

                return frac(rotated * _WorldTiling);

             }

            half4 Frag(Varyings input) : SV_Target
            {
                float2 uv = WorldUV(input.positionWS.xz);
                half4 albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv) * _BaseColor;

                Light mainLight = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 normalWS = normalize(input.normalWS);
                half diffuse = saturate(dot(normalWS, mainLight.direction));
                half3 lighting = saturate(SampleSH(normalWS) + mainLight.color * diffuse * mainLight.shadowAttenuation);

                return half4(albedo.rgb * lighting, albedo.a);
            }
            ENDHLSL
        }

        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
