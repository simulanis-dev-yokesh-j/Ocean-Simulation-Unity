// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Unlit/Ocean"
{
    Properties
    {
        _Color("Color", Color) = (1, 1, 1, 1)
        _DiffuseColor("Diffuse Color", Color) = (1, 1, 1, 1)
        [HDR] _SpecColor ("Specular Color", Color) = (1, 1, 1, 1)
        _Shininess ("Shininess", Float) = 10
        _DisplacementMap("Displacement Map", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        
        _WaterScatterColor("Water Scatter Color", Color) = (1, 1, 1, 1)
        _AirBubblesColor("Air Bubbles Color", Color) = (1, 1, 1, 1)
        _DensityOfWaterBubbles("Density of Water Bubbles", Float) = 0.5
        _Tweak1("Tweak1", Float) = 0.05
        _Tweak2("Tweak2", Float) = 0.5
        _Tweak3("Tweak3", Float) = 0.5
        _Tweak4("Tweak4", Float) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            uniform float4 _LightColor0; //From UnityCG
            
            uniform float4 _Color;
            uniform float4 _DiffuseColor;
            uniform float4 _SpecColor;
            uniform float _Shininess;
            uniform sampler2D_half _DisplacementMap;
            uniform sampler2D_half _NormalMap;

            uniform float4 _WaterScatterColor;
            uniform float4 _AirBubblesColor;
            uniform float _DensityOfWaterBubbles;
            uniform float _Tweak1;
            uniform float _Tweak2;
            uniform float _Tweak3;
            uniform float _Tweak4;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : POSITION;
                float2 uv : TEXCOORD0;
                float4 posWorld : TEXCOORD1;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float3 displacement = tex2Dlod(_DisplacementMap, float4(v.uv, 0, 0));
                v.vertex.x += displacement.x;
                v.vertex.y = displacement.y;
                v.vertex.z += displacement.z;

                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 pos = i.posWorld.xyz;
                float3 viewDirection = normalize(_WorldSpaceCameraPos - pos);
                float3 sunDirection = normalize(_WorldSpaceLightPos0.xyz);
                float3 halfVector = normalize(sunDirection + viewDirection);
                float3 normal = normalize(tex2D(_NormalMap, i.uv));
                float height = max(0, i.posWorld.y);
                
                // float3 normal = normalize(tex2D(_NormalMap, i.uv));
                // float3 viewDirection = normalize(_WorldSpaceCameraPos - i.posWorld.xyz);
                //
                // float3 ambient = _LightColor0.rgb * _Color.rgb; //Ambient component
                //
                // float diff = max(dot(normal, _WorldSpaceLightPos0.xyz), 0.0);
                // float3 diffuse = _LightColor0.rgb * _DiffuseColor.rgb * diff; //Diffuse component
                //
                float3 reflectDir = reflect(-_WorldSpaceLightPos0.xyz, normal);
                float spec = pow(max(dot(viewDirection, reflectDir), 0.0), _Shininess);
                float3 specular = _LightColor0.rgb * (spec * _SpecColor);  
                //
                // float3 color = ambient + diffuse + specular;
                //
                // return float4(color.rgb, 1);


                
                float part1 = _Tweak1 * height * pow(max(0, dot(sunDirection, -viewDirection)), 4);
                float part2 = 0.5 - 0.5 * pow(dot(sunDirection, normal), 3);
                float part3 = _Tweak2 * pow(max(0, dot(viewDirection, normal)), 2);
                
                float3 scatter = (part1 * part2 + part3) * _WaterScatterColor.rgb * _LightColor0.rgb;

                part1 = _Tweak3 * dot(sunDirection, normal) ;
                part2 = _Tweak4 * _DensityOfWaterBubbles;

                scatter += part1 * _WaterScatterColor.rgb * _LightColor0.rgb + part2 * _AirBubblesColor.rgb * _LightColor0.rgb;
                
                return float4(scatter + specular, 1);
            }
            ENDCG
        }
    }
}
