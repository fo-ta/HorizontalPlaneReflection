Shader "HorizontalPlaneReflection/Transparent/SampleAdditive"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
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
            half4 outColor = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv);
            outColor *= input.color;
            outColor.rgb *= pow(2.0, _ColorIntensity);
            outColor.rgb *= outColor.a;
            return outColor;
        }

        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { }
            Blend One One
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
            Tags { "LightMode"="HorizontalPlaneReflectionTransparent" }
            Blend One One, Zero One
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
                half4 outColor = frag(input);
                half reflectionFadeAlpha = GetReflectionAlpha(input.positionWS.y);

                outColor.rgb *= reflectionFadeAlpha;

                return outColor;
            }
            ENDHLSL
        }
    }
}
