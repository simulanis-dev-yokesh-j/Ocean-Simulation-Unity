// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Unlit/Ocean"
{
    Properties
    {
        _AirBubblesColor("Air Bubbles Color", Color) = (1, 1, 1, 1)
        _WaterScatterColor("Water Scatter Color", Color) = (1, 1, 1, 1)
        [HDR] _SpecColor ("Specular Color", Color) = (1, 1, 1, 1)
        _Shininess ("Shininess", Float) = 10
        _Reflectivity("Reflectivity", Range(0, 2)) = 0.1
        
        _DensityOfWaterBubbles("Density of Water Bubbles", Float) = 0.5
        _Tweak1("Tweak1", Float) = 0.05
        _Tweak2("Tweak2", Float) = 0.5
        _Tweak3("Tweak3", Float) = 0.5
        
        _DisplacementMap1("Displacement Map 1", 2D) = "white" {}
        _DisplacementMap2("Displacement Map 2", 2D) = "white" {}
        _DisplacementMap3("Displacement Map 3", 2D) = "white" {}
        
        _NormalMap1("Normal Map 1", 2D) = "bump" {}
        _NormalMap2("Normal Map 2", 2D) = "bump" {}
        _NormalMap3("Normal Map 3", 2D) = "bump" {}
        
        _FoamMap1("Foam Map 1", 2D) = "white" {}
        _FoamMap2("Foam Map 2", 2D) = "white" {}
        _FoamMap3("Foam Map 3", 2D) = "white" {}
    }
    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType" = "Opaque" }
        LOD 100
        Cull Off

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

            uniform float4 _LightColor0; //From UnityCG
            
            uniform float4 _WaterScatterColor;
            uniform float4 _AirBubblesColor;
            uniform float4 _SpecColor;
            uniform float _Shininess;
            uniform float _Reflectivity;

            uniform float _DensityOfWaterBubbles;
            uniform float _Tweak1;
            uniform float _Tweak2;
            uniform float _Tweak3;
            
            uniform sampler2D_half _DisplacementMap1;
            uniform sampler2D_half _DisplacementMap2;
            uniform sampler2D_half _DisplacementMap3;
            uniform sampler2D_half _NormalMap1;
            uniform sampler2D_half _NormalMap2;
            uniform sampler2D_half _NormalMap3;
            uniform sampler2D_half _FoamMap1;
            uniform sampler2D_half _FoamMap2;
            uniform sampler2D_half _FoamMap3;
            uniform float _LengthScale1;
            uniform float _LengthScale2;
            uniform float _LengthScale3;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
                float3 posWorld : TEXCOORD1;
                float2 uvWorld : TEXCOORD2;
                UNITY_FOG_COORDS(3)
            };

            v2f vert (appdata v)
            {
                v2f o;

                float2 uvWorld = mul(unity_ObjectToWorld, v.vertex).xz;
                
                float2 uvWorld1 = uvWorld / _LengthScale1;
                float2 uvWorld2 = uvWorld / _LengthScale2;
                float2 uvWorld3 = uvWorld / _LengthScale3;
                
                float3 displacement = tex2Dlod(_DisplacementMap1, float4(uvWorld1, 0, 0));
                displacement += tex2Dlod(_DisplacementMap2, float4(uvWorld2, 0, 0));
                displacement += tex2Dlod(_DisplacementMap3, float4(uvWorld3, 0, 0));
                
                v.vertex.x += displacement.x;
                v.vertex.y = displacement.y;
                v.vertex.z += displacement.z;

                float3 posWorld = mul(unity_ObjectToWorld, v.vertex);

                o.posWorld = posWorld;
                
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.uvWorld = uvWorld;
                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            float DotClamped(float3 a, float3 b)
            {
                return max(0, dot(a, b));
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 posWorld = i.posWorld;
                float2 uvWorld1 = i.uvWorld / _LengthScale1;
                float2 uvWorld2 = i.uvWorld / _LengthScale2;
                float2 uvWorld3 = i.uvWorld / _LengthScale3;
                
                float3 viewDirection = normalize(_WorldSpaceCameraPos - posWorld);
                float3 sunDirection = normalize(_WorldSpaceLightPos0.xyz);

                float2 derivatives = tex2Dlod(_NormalMap1, float4(uvWorld1, 0, 0)).rg;
                derivatives += tex2Dlod(_NormalMap2, float4(uvWorld2, 0, 0)).rg;
                derivatives += tex2Dlod(_NormalMap3, float4(uvWorld3, 0, 0)).rg;
                float3 normal = normalize(float3(-derivatives.x, 1, -derivatives.y));

                float part3 = _Tweak3 * normal;
                float3 ambient = part3 * _WaterScatterColor * _LightColor0 + _DensityOfWaterBubbles * _AirBubblesColor * _LightColor0;

                // Fresnel
                float fresnel = pow(1.0 - max(dot(viewDirection, normal), 0.15), 5.0);
                
                float3 reflectDir = reflect(-_WorldSpaceLightPos0.xyz, normal);
                float spec = pow(max(dot(viewDirection, reflectDir), 0.0), _Shininess);
                float3 specular = _LightColor0.rgb * (spec * _SpecColor) * fresnel;  

                float part1 = _Tweak1 * max(0, posWorld.y) * pow(DotClamped(sunDirection, -viewDirection), 4.0f) * pow(0.5f - 0.5f * dot(sunDirection, normal), 3.0f);
				float part2 = _Tweak2 * pow(DotClamped(viewDirection, normal), 2.0f);
                
				float3 scatter = (1 - fresnel) * (part1 + part2) * _WaterScatterColor * _LightColor0;

                float3 I = normalize(posWorld - _WorldSpaceCameraPos);
                half4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflect(I, normal));
                half3 envReflection = fresnel * _Reflectivity * DecodeHDR (skyData, unity_SpecCube0_HDR);

                float3 output = ambient + scatter + specular + envReflection;

                float foam = tex2D(_FoamMap1, uvWorld1).r;
                foam += tex2D(_FoamMap2, uvWorld2).r;
                foam += tex2D(_FoamMap3, uvWorld3).r;

                if(foam > 0)
                {
                    float3 foamColor = float3(foam, foam, foam);
                    output = saturate(output + foamColor) * max(_LightColor0, 0.4);
                }

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, output);
                
                return float4(output, 1);
            }
            ENDCG
        }
    }
}