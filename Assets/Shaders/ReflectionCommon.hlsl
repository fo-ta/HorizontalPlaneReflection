#ifndef INCLUDED_REFLECTION_COMMON
#define INCLUDED_REFLECTION_COMMON

float _ReflectionFadeBaseHeight;
float _ReflectionFadeRange;
float _ReflectionPlaneHeight;

float GetReflectionAlpha(float heightWS)
{
    half reflectionFadeAlpha = smoothstep(_ReflectionFadeBaseHeight + _ReflectionFadeRange, _ReflectionFadeBaseHeight, heightWS);
    reflectionFadeAlpha *= step(_ReflectionPlaneHeight, heightWS);
    return reflectionFadeAlpha;
}

#endif
