Shader "Protocol/Alien Night Sky"
{
    Properties
    {
        _Daylight ("Daylight", Range(0, 1)) = 0
        _Twilight ("Twilight", Range(0, 1)) = 0
        _StarIntensity ("Stars", Range(0, 4)) = 1
        _Rotation ("Rotation", Range(0, 360)) = 0
    }
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off
        ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float _Daylight, _Twilight, _StarIntensity, _Rotation;
            CBUFFER_END

            struct Attributes { float4 positionOS : POSITION; };
            struct Varyings { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };

            float Hash(float3 p)
            {
                p = frac(p * .1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float Noise(float3 p)
            {
                float3 cell = floor(p);
                float3 f = frac(p);
                f = f * f * (3 - 2 * f);
                return lerp(
                    lerp(lerp(Hash(cell), Hash(cell + float3(1, 0, 0)), f.x),
                        lerp(Hash(cell + float3(0, 1, 0)), Hash(cell + float3(1, 1, 0)), f.x), f.y),
                    lerp(lerp(Hash(cell + float3(0, 0, 1)), Hash(cell + float3(1, 0, 1)), f.x),
                        lerp(Hash(cell + float3(0, 1, 1)), Hash(cell + 1), f.x), f.y), f.z);
            }

            float Fractal(float3 p)
            {
                float value = 0;
                float weight = .5;
                for (int octave = 0; octave < 6; octave++)
                {
                    value += Noise(p) * weight;
                    p = p * 2.03 + float3(17.1, 4.7, 9.2);
                    weight *= .5;
                }
                return value;
            }

            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float angle = radians(_Rotation);
                direction.xz = float2(direction.x * cos(angle) - direction.z * sin(angle),
                    direction.x * sin(angle) + direction.z * cos(angle));
                float horizon = pow(saturate(1 - abs(direction.y)), 4);
                float3 night = lerp(float3(.025, .008, .055), float3(.13, .045, .19), horizon);
                float3 day = lerp(float3(.22, .085, .38), float3(.51, .30, .59), horizon);
                float3 color = lerp(night, day, _Daylight);
                color += float3(.25, .075, .18) * horizon * _Twilight;

                float band = exp(-pow(dot(direction, normalize(float3(.35, .8, -.4))) * 5, 2));
                float dust = Fractal(direction * 8);
                color += float3(.10, .12, .16) * band * pow(dust, 2) * (1 - _Daylight);
                float cloud = smoothstep(.47, .72, Fractal(direction * 5 + float3(_Time.y * .001, 0, 0)));
                color = lerp(color, lerp(float3(.11, .065, .17), float3(.58, .43, .66), _Daylight),
                    cloud * .55);

                float3 starGrid = direction * 650;
                float starSeed = Hash(floor(starGrid));
                float starDistance = length(frac(starGrid) - .5);
                float pixelWidth = max(length(fwidth(starGrid)), .05);
                float star = (1 - smoothstep(.08, .08 + pixelWidth, starDistance)) * step(.996, starSeed);
                color += star * _StarIntensity * (1 - _Daylight) * (1 - cloud) * .65;

                float3 moonDirection = normalize(float3(-.38, .39, .84));
                float cosine = dot(direction, moonDirection);
                float radius = .075;
                float3 offset = (direction - moonDirection * cosine) / radius;
                float distanceSquared = dot(offset, offset);
                if (cosine > 0 && distanceSquared < 1)
                {
                    float3 normal = normalize(offset + moonDirection * sqrt(1 - distanceSquared));
                    float relief = Fractal(normal * 34);
                    float maria = smoothstep(.38, .62, Fractal(normal * 7));
                    float light = saturate(dot(normal, normalize(float3(-.8, .45, .25))));
                    float3 moon = lerp(float3(.17, .20, .23), float3(.5, .52, .53), maria);
                    moon *= (.6 + relief * .65) * (.05 + light * .95);
                    float edge = 1 - smoothstep(1 - max(fwidth(distanceSquared), .001), 1, distanceSquared);
                    color = lerp(color, moon + day * _Daylight * .12, edge);
                }
                return half4(color, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
