Shader "Protocol/FogSurface"
{
 Properties
 {
  _Color ("Color", Color) = (1,1,1,1)
  _MainTex ("Albedo", 2D) = "white" {}
  _Glossiness ("Smoothness", Range(0,1)) = .1
  _Metallic ("Metallic", Range(0,1)) = 0
  _EmissionColor ("Emission", Color) = (0,0,0,0)
  _SurfaceDetail ("Mineral detail", Range(0,1)) = 0
  _TerrainBlend ("Terrain blending", Range(0,1)) = 0
  _WindStrength ("Leaf breeze", Range(0,.2)) = 0
  _IgnoreWarFog ("Ignore war fog", Float) = 0
 }
 SubShader
 {
  Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
  HLSLINCLUDE
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
  TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
  TEXTURE2D(_ProtocolFogMap); SAMPLER(sampler_ProtocolFogMap);
  float _ProtocolFogSize;
  CBUFFER_START(UnityPerMaterial)
   float4 _Color, _EmissionColor, _MainTex_ST;
   float _Glossiness, _Metallic, _SurfaceDetail, _TerrainBlend, _WindStrength, _IgnoreWarFog;
  CBUFFER_END
  struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; };
  struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; float3 normalWS:TEXCOORD1; float2 uv:TEXCOORD2; };
  float3 Wind(Attributes input)
  {
   float3 world=TransformObjectToWorld(input.positionOS.xyz);
   float sway=sin(_Time.y*1.4+world.x*.65+world.z*.45);
   return input.positionOS.xyz+float3(sway,0,sway*.4)*_WindStrength*input.uv.y*input.uv.y;
  }
  Varyings Vert(Attributes input)
  {
   Varyings output;
   output.positionWS=TransformObjectToWorld(Wind(input));
   output.positionCS=TransformWorldToHClip(output.positionWS);
   output.normalWS=TransformObjectToWorldNormal(input.normalOS);
   output.uv=TRANSFORM_TEX(input.uv,_MainTex);
   return output;
  }
        float Hash(float2 p)
        {
            return frac(sin(dot(p, float2(127.1, 311.7))) * 43758.5453);
        }

        float Noise(float2 p)
        {
            float2 cell = floor(p), f = frac(p);
            f = f * f * (3 - 2 * f);
            return lerp(lerp(Hash(cell), Hash(cell + float2(1,0)), f.x),
                lerp(Hash(cell + float2(0,1)), Hash(cell + 1), f.x), f.y);
        }

        float FilteredNoise(float2 p, float footprint, float frequency)
        {
            float visibility = 1 - smoothstep(.25, .75, footprint * frequency);
            float result = .5;
            [branch]
            if (visibility > 0)
                result = lerp(.5, Noise(p * frequency), visibility);
            return result;
        }

        float Mineral(float2 p, float footprint)
        {
            return FilteredNoise(p, footprint, 3.7) * .18
                + FilteredNoise(p + 17.3, footprint, 14.1) * .32
                + FilteredNoise(p - 9.2, footprint, 53.7) * .30
                + FilteredNoise(p + 41.8, footprint, 173.3) * .20;
        }


  half4 Frag(Varyings input):SV_Target
  {
   float2 p=input.positionWS.xz;
   float footprint=max(length(ddx(p)),length(ddy(p)));
   float grain=Mineral(p,footprint);
   float patches=Noise(p*.14);
   half4 color=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,input.uv)*_Color;
   float clearing=1-smoothstep(17,26,length(p)+(patches-.5)*8);
   clearing=max(clearing,1-smoothstep(2,5,abs(p.y-sin(p.x/30)*5)+patches));
   float3 stone=lerp(float3(.11,.17,.18),float3(.27,.31,.29),patches);
   float3 dust=lerp(float3(.22,.19,.21),float3(.38,.32,.31),grain);
   color.rgb=lerp(color.rgb,lerp(stone,dust,clearing),_TerrainBlend);
   color.rgb*=lerp(1,.72+grain*.56,_SurfaceDetail);
   float sampleStep=max(.003,footprint*.5);
   float dx=Mineral(p+float2(sampleStep,0),footprint)-grain;
   float dz=Mineral(p+float2(0,sampleStep),footprint)-grain;
   float relief=min(4,.04/sampleStep)*_SurfaceDetail;
   InputData lighting=(InputData)0;
   lighting.positionWS=input.positionWS;
   lighting.normalWS=normalize(input.normalWS+float3(-dx,0,-dz)*relief);
   lighting.viewDirectionWS=GetWorldSpaceNormalizeViewDir(input.positionWS);
   lighting.shadowCoord=TransformWorldToShadowCoord(input.positionWS);
   lighting.bakedGI=SampleSH(lighting.normalWS);
   lighting.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(input.positionCS);
   lighting.shadowMask=half4(1,1,1,1);
   SurfaceData surface=(SurfaceData)0;
   surface.albedo=color.rgb;
   surface.alpha=1;
   surface.metallic=_Metallic;
   surface.smoothness=saturate(_Glossiness+(grain-.5)*_SurfaceDetail*.18);
   surface.normalTS=half3(0,0,1);
   surface.occlusion=1;
   surface.emission=_EmissionColor.rgb;
   half4 result=UniversalFragmentPBR(lighting,surface);
   float fog=SAMPLE_TEXTURE2D(_ProtocolFogMap,sampler_ProtocolFogMap,p/max(_ProtocolFogSize,1)+.5).a;
   result.rgb=lerp(result.rgb,float3(.055,.08,.11),fog*step(1,_ProtocolFogSize)*(1-_IgnoreWarFog));
   return result;
  }
  half4 DepthFrag(Varyings input):SV_Target { return 0; }
  float3 _LightDirection;
  float3 _LightPosition;
  Varyings ShadowVert(Attributes input)
  {
   Varyings output=Vert(input);
   float3 direction=_LightDirection;
   #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    direction=normalize(_LightPosition-output.positionWS);
   #endif
   output.positionCS=TransformWorldToHClip(ApplyShadowBias(output.positionWS,output.normalWS,direction));
   #if UNITY_REVERSED_Z
    output.positionCS.z=min(output.positionCS.z,UNITY_NEAR_CLIP_VALUE);
   #else
    output.positionCS.z=max(output.positionCS.z,UNITY_NEAR_CLIP_VALUE);
   #endif
   return output;
  }
  ENDHLSL
  Pass
  {
   Name "ForwardLit"
   Tags { "LightMode"="UniversalForward" }
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   ENDHLSL
  }
  Pass
  {
   Name "ShadowCaster"
   Tags { "LightMode"="ShadowCaster" }
   ZWrite On ColorMask 0
   HLSLPROGRAM
   #pragma vertex ShadowVert
   #pragma fragment DepthFrag
   #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
   ENDHLSL
  }
  Pass
  {
   Name "DepthOnly"
   Tags { "LightMode"="DepthOnly" }
   ZWrite On ColorMask R
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment DepthFrag
   ENDHLSL
  }
 }
}
