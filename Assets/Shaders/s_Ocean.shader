// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Unlit/Ocean"
{
    Properties
    {
        _AirBubblesColor("Air Bubbles Color", Color) = (1, 1, 1, 1)
        _WaterScatterColor("Water Scatter Color", Color) = (1, 1, 1, 1)
        [HDR] _SpecColor ("Specular Color", Color) = (1, 1, 1, 1)
        _Shininess ("Shininess", Float) = 10
        _Reflectivity("Reflectivity", Range(0, 1)) = 0.1
        
        _DensityOfWaterBubbles("Density of Water Bubbles", Float) = 0.5
        _Tweak1("Tweak1", Float) = 0.05
        _Tweak2("Tweak2", Float) = 0.5
        _Tweak3("Tweak3", Float) = 0.5
        
        _DisplacementMap("Displacement Map", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _FoamMap("Foam Map", 2D) = "white" {}
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
            
            uniform sampler2D_half _DisplacementMap;
            uniform sampler2D_half _NormalMap;
            uniform sampler2D_half _FoamMap;
            uniform float _LengthScale;

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

                float3 posWorld = mul(unity_ObjectToWorld, v.vertex);
                float2 uvWorld = posWorld.xz / _LengthScale;
                
                float3 displacement = tex2Dlod(_DisplacementMap, float4(uvWorld, 0, 0));
                v.vertex.x += displacement.x;
                v.vertex.y = displacement.y;
                v.vertex.z += displacement.z;

                posWorld = mul(unity_ObjectToWorld, v.vertex);

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
                float2 uvWorld = i.uvWorld;
                
                float3 viewDirection = normalize(_WorldSpaceCameraPos - posWorld);
                float3 sunDirection = normalize(_WorldSpaceLightPos0.xyz);
                float3 halfVector = normalize(sunDirection + viewDirection);
                float3 normal = tex2Dlod(_NormalMap, float4(uvWorld, 0, 0));

                float part3 = _Tweak3 * normal;
                float3 ambient = part3 * _WaterScatterColor * _LightColor0 + _DensityOfWaterBubbles * _AirBubblesColor * _LightColor0;

                // Fresnel
                float fresnel = pow(1.0 - max(dot(viewDirection, normal), 0.15), 5.0);
                
                float3 reflectDir = reflect(-_WorldSpaceLightPos0.xyz, normal);
                float spec = pow(max(dot(viewDirection, reflectDir), 0.0), _Shininess);
                float3 specular = _LightColor0.rgb * (spec * _SpecColor) * fresnel;  

                float part1 = _Tweak1 * max(0, posWorld.y) * pow(DotClamped(sunDirection, -viewDirection), 4.0f) * pow(0.5f - 0.5f * dot(sunDirection, normal), 3.0f);
				float part2 = _Tweak2 * pow(DotClamped(viewDirection, normal), 2.0f);
                
				float3 scatter = (part1 + part2) * _WaterScatterColor * _LightColor0;

                float3 I = normalize(posWorld - _WorldSpaceCameraPos);
                half4 skyData = UNITY_SAMPLE_TEXCUBE(unity_SpecCube0, reflect(I, normal));
                half3 envReflection = (1-fresnel) * _Reflectivity * DecodeHDR (skyData, unity_SpecCube0_HDR);

                float3 output = ambient + scatter + specular + envReflection;

                float foam = tex2D(_FoamMap, uvWorld).r;

                if(foam > 0)
                {
                    float3 foamColor = float3(foam, foam, foam);
                    output = saturate(output + foamColor);
                }

                // apply fog
                UNITY_APPLY_FOG(i.fogCoord, output);
                
                return float4(output, 1);
            }
            ENDCG
        }
    }
}