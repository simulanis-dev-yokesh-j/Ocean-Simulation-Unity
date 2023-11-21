Shader "Unlit/Ocean"
{
    Properties
    {
        _HeightMap("Height Map", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _LightDir("Light Direction", Vector) = (0, 10, 5, 0)
        _AmientColor("Ambient Color", Color) = (1, 1, 1, 1)
        _DiffuseColor("Diffuse Color", Color) = (1, 1, 1, 1)
        _SpecularColor("Specular Color", Color) = (1, 1, 1, 1)
        _Shininess("Shininess", Range(0, 1)) = 0.078125
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

            #include "UnityCG.cginc"

            sampler2D_half _HeightMap;
            sampler2D_half _NormalMap;
            float4 _LightDir;
            float4 _AmbientColor;
            float4 _DiffuseColor;
            float4 _SpecularColor;
            float _Shininess;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                UNITY_FOG_COORDS(1)
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;
                float4 texel = tex2Dlod(_HeightMap, float4(v.uv, 0, 0));
                float height = texel.r;
                v.vertex.y = height;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // float3 viewDir = normalize(UnityWorldSpaceViewDir(i.vertex));
                // float3 normal = tex2D(_NormalMap, i.uv);
                //
                // //diffuse
                // float3 lightDir = normalize(-_LightDir.xyz);
                // float3 diff = max(0, dot(normal, lightDir)) * _DiffuseColor.rgb;
                //
                // //specular
                // float3 reflectDir = reflect(-lightDir, normal);  
                // float spec = pow(max(dot(viewDir, reflectDir), 0.0), _Shininess) * _SpecularColor.rgb;
                //
                // //result
                // float3 result = _AmbientColor.rgb + diff + spec;
                // return float4(result, 1);

                float3 normal = tex2D(_NormalMap, i.uv);
                return float4(normal, 1);
            }
            ENDCG
        }
    }
}
