Shader "HorizontalPlaneReflection/SamplePlane"
{
    Properties
    {
        _BaseColor ("Color", Color) = (1,1,1,1)
        _MainTex ("Texture", 2D) = "white" {}
        _ReflectionTextureIntensity ("Reflection Color Intensity", Range(0,1)) = 0.5
    }
    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
            "IgnoreProjector" = "True"
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
        }

        Pass
        {
            Name "ForwardUnlit"
            Tags { }
            ZWrite On

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            // Includes
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Color.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 texcoord : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 positionSS : TEXCOORD1;
            };

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_HorizontalPlaneReflectionTexture);  SAMPLER(sampler_HorizontalPlaneReflectionTexture);

            CBUFFER_START(UnityPerMaterial)
            half4 _BaseColor;
            float4 _MainTex_ST;
            float _ReflectionTextureIntensity;
            CBUFFER_END

            Varyings vert (Attributes input)
            {
                Varyings output = (Varyings)0;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = TRANSFORM_TEX(input.texcoord, _MainTex);
                output.positionSS = ComputeScreenPos(output.positionCS);
                return output;
            }

            half4 frag (Varyings input) : SV_Target
            {
                half4 outColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
                outColor *= _BaseColor;
                half4 reflectionColor = SAMPLE_TEXTURE2D(_HorizontalPlaneReflectionTexture, sampler_HorizontalPlaneReflectionTexture, input.positionSS.xy / input.positionSS.w);

                outColor.rgb = reflectionColor.rgb * _ReflectionTextureIntensity
                    + outColor.rgb * (1.0 - reflectionColor.a* _ReflectionTextureIntensity);
                
                return outColor;
            }
            ENDHLSL
        }
    }
}
