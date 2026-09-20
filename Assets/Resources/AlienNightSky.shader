Shader "Protocol/Alien Night Sky"
{
 Properties{_ZenithColor("Zenith",Color)=(.015,.03,.12,1)_HorizonColor("Horizon",Color)=(.28,.07,.36,1)_NebulaColor("Nebula",Color)=(.12,.66,.92,1)_AuroraColor("Aurora",Color)=(.95,.22,.68,1)_StarIntensity("Stars",Range(0,4))=1.8_Rotation("Rotation",Range(0,360))=0}
 SubShader{Tags{"Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox"} Cull Off ZWrite Off Pass{
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #include "UnityCG.cginc"
 float4 _ZenithColor,_HorizonColor,_NebulaColor,_AuroraColor; float _StarIntensity,_Rotation;
 struct appdata{float4 vertex:POSITION;}; struct v2f{float4 pos:SV_POSITION;float3 dir:TEXCOORD0;};
 float hash21(float2 p){p=frac(p*float2(123.34,456.21));p+=dot(p,p+45.32);return frac(p.x*p.y);}
 float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);}
 v2f vert(appdata v){v2f o;o.pos=UnityObjectToClipPos(v.vertex);o.dir=v.vertex.xyz;return o;}
 fixed4 frag(v2f i):SV_Target{
  float3 d=normalize(i.dir);float a=radians(_Rotation),s=sin(a),c=cos(a);d.xz=float2(d.x*c-d.z*s,d.x*s+d.z*c);
  float vertical=saturate(d.y*.72+.32),horizon=pow(saturate(1-abs(d.y)),3.2);
  float3 color=lerp(_HorizonColor.rgb,_ZenithColor.rgb,vertical);
  float2 q=d.xz*3.6+d.y*float2(1.7,-.9);float cloud=noise(q)*.62+noise(q*2.15+8.4)*.38;
  cloud=smoothstep(.45,.82,cloud)*horizon;float aurora=pow(saturate(1-abs(d.x*.7+d.y-.22)),7)*horizon;
  color+=_NebulaColor.rgb*cloud*.42+_AuroraColor.rgb*aurora*.22;
  float2 uv=float2(atan2(d.z,d.x)/6.2831853+.5,asin(d.y)/3.14159265+.5),cell=floor(uv*float2(820,410));
  float star=smoothstep(.994,1,hash21(cell))*(.45+hash21(cell+17.3)*1.35);color+=star*_StarIntensity*saturate(d.y*2.8+.35);
  float da=dot(d,normalize(float3(-.38,.39,.84))),discA=smoothstep(.991,.992,da);
  float3 ca=lerp(float3(.07,.24,.48),float3(.35,.85,1),saturate((da-.991)/.009))*(.75+noise(uv*95)*.28);
  color=lerp(color,ca,discA);color+=smoothstep(.9885,.991,da)*(1-discA)*float3(.08,.38,.65);
  float db=dot(d,normalize(float3(.27,.29,.92))),discB=smoothstep(.9954,.9961,db);
  float3 cb=lerp(float3(.15,.16,.48),float3(.66,.45,1),saturate((db-.9954)/.0046))*(.72+noise(uv*130+4.7)*.3);
  color=lerp(color,cb,discB);color+=smoothstep(.9935,.9954,db)*(1-discB)*float3(.22,.08,.48);
  return float4(color,1);
 }
 ENDCG } } Fallback Off
}