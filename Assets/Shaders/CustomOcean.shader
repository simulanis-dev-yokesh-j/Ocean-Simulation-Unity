Shader "Unlit/CustomOcean"
{
    Properties
    {
        _DisplacementMap1("Displacement Map 1", 2D) = "white" {}
        _NormalMap1("Normal Map 1", 2D) = "bump" {}
        _LengthScale1("Length Scale 1", Float) = 1
        _LightDir("Light Direction", Vector) = (0, 1, 0, 0)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            // make fog work
            #pragma multi_compile_fog

            #include "UnityCG.cginc"

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
            };
            
            uniform sampler2D_half _DisplacementMap1;
            uniform sampler2D_half _NormalMap1;
            uniform float _LengthScale1;
            uniform float3 _LightDir;

            v2f vert (appdata v)
            {
                v2f o;

                float2 uvWorld = mul(unity_ObjectToWorld, v.vertex).xz;
                float2 uvWorld1 = uvWorld / _LengthScale1;
                float3 displacement = tex2Dlod(_DisplacementMap1, float4(uvWorld1, 0, 0));
                
                v.vertex.xyz += displacement.xyz;

                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.uvWorld = uvWorld;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float2 uvWorld1 = i.uvWorld / _LengthScale1;
                float2 derivatives = tex2D(_NormalMap1, float4(uvWorld1, 0, 0)).rg;
                float3 normal = normalize(float3(-derivatives.x, 1, -derivatives.y));

                //ambient
                float3 ambient = float3(0.1, 0.1, 0.1);
                
                // diffuse lighting
                float3 lightDir = normalize(float3(0.5, 0.5, 0.5));
                float diff = max(dot(normal, lightDir), 0.0);
                float3 diffuse = diff * float3(0.8, 0.8, 0.8);
                
                return float4(ambient + diffuse, 1);
            }
            ENDCG
        }
    }
}
