Shader "HorizontalPlaneReflection/Transparent/SampleAlphaAdditive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}

        _AlphaTintColor ("Alpha Blend", Color) = (1, 1, 1, 1)
        _AddTintColor ("Additive", Color) = (1, 1, 1, 1)

        _ColorIntensity ("ColorIntensity", float) = 0.0

        [Enum(UnityEngine.Rendering.CullMode)] _CullMode ("Culling Mode", int) = 2  // Back
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
        }

        HLSLINCLUDE

        // Includes
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
        #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

        struct Attributes
        {
            float4 positionOS : POSITION;
            half4 color : COLOR;
            float2 texcoord : TEXCOORD0;
        };

        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            half4 color : COLOR;
            float2 uv : TEXCOORD0;
            float3 positionWS : TEXCOORD1;
        };

        TEXTURE2D(_MainTex);    SAMPLER(sampler_MainTex);

        CBUFFER_START(UnityPerMaterial)
        float4 _MainTex_ST;
        half4 _AlphaTintColor;
        half4 _AddTintColor;
        float _ColorIntensity;
        CBUFFER_END

        Varyings vert (Attributes input)
        {
            Varyings output = (Varyings)0;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
            output.color = input.color;
            output.positionWS = TransformObjectToWorld(input.positionOS.xyz);

            return output;
        }

        half4 frag (Varyings input) : SV_Target
        {
            // sample the texture
            half4 texColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            texColor *= input.color;

            half3 blendColor = texColor.rgb;
            half blendAlpha = texColor.a;

            half alpha = blendAlpha * _AlphaTintColor.a;
            half4 outColor = half4(blendColor * _AlphaTintColor.rgb * alpha, alpha);

            outColor.rgb += blendColor.rgb * _AddTintColor.rgb * (blendAlpha * _AddTintColor.a);

            outColor.rgb *= pow(2.0, _ColorIntensity);

            return outColor;
        }

        ENDHLSL

        Pass
        {
            Name "ForwardUnlit"
            Tags { }
            Blend One OneMinusSrcAlpha
            Cull [_CullMode]
            ZTest LEqual
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            ENDHLSL
        }

        Pass
        {
            Name "HorizontalPlaneReflectionTransparent"
            Tags { "LightMode"="HorizontalPlaneReflectionTransparent" }
            Blend One OneMinusSrcAlpha
            Cull [_CullMode]
            ZTest LEqual
            ZWrite Off

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment fragReflection

            // 反射用 HLSL
            #include "ReflectionCommon.hlsl"

            half4 fragReflection (Varyings input) : SV_Target
            {
                // sample the texture
                half4 outColor = frag(input);
                half reflectionFadeAlpha = GetReflectionAlpha(input.positionWS.y);

                outColor *= reflectionFadeAlpha;

                return outColor;
            }
            ENDHLSL
        }
    }
}
