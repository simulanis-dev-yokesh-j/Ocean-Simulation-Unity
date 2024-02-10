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
                float3 normal = normalize(tex2D(_NormalMap, i.uv));
                float3 viewDirection = normalize(_WorldSpaceCameraPos - i.posWorld.xyz);
                
                float3 ambient = _LightColor0.rgb * _Color.rgb; //Ambient component
                
                float diff = max(dot(normal, _WorldSpaceLightPos0.xyz), 0.0);
                float3 diffuse = _LightColor0.rgb * _DiffuseColor.rgb * diff; //Diffuse component

                float3 reflectDir = reflect(-_WorldSpaceLightPos0.xyz, normal);
                float spec = pow(max(dot(viewDirection, reflectDir), 0.0), _Shininess);
                float3 specular = _LightColor0.rgb * (spec * _SpecColor);  
                
                float3 color = ambient + diffuse + specular;
                
                return float4(color.rgb, 1);
            }
            ENDCG
        }
    }
}
