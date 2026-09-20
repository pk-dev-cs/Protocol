Shader "Protocol/FogSurface"
{
 Properties {
  _Color ("Color", Color) = (1,1,1,1)
  _MainTex ("Albedo", 2D) = "white" {}
  _Glossiness ("Smoothness", Range(0,1)) = 0.1
  _Metallic ("Metallic", Range(0,1)) = 0
  _EmissionColor ("Emission", Color) = (0,0,0,0)
 }
 SubShader {
  Tags { "RenderType"="Opaque" }
  LOD 200
  CGPROGRAM
  #pragma surface surf Standard fullforwardshadows finalcolor:ApplyFog
  #pragma target 3.0
  #pragma multi_compile_instancing
  sampler2D _MainTex;
  sampler2D _ProtocolFogMap;
  float _ProtocolFogSize;
  fixed4 _Color, _EmissionColor;
  half _Glossiness, _Metallic;
  struct Input { float2 uv_MainTex; float3 worldPos; };
  void surf(Input IN, inout SurfaceOutputStandard o) {
   fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
   o.Albedo = c.rgb; o.Alpha = c.a; o.Smoothness = _Glossiness; o.Metallic = _Metallic;
   o.Emission = _EmissionColor.rgb;
  }
  void ApplyFog(Input IN, SurfaceOutputStandard o, inout fixed4 color) {
   float2 uv = IN.worldPos.xz / max(_ProtocolFogSize, 1) + 0.5;
   float fog = tex2D(_ProtocolFogMap, uv).a;
   color.rgb = lerp(color.rgb, float3(0.055, 0.08, 0.11), fog);
  }
  ENDCG
 }
 Fallback "Standard"
}
