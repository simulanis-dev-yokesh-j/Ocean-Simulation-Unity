Shader "Unlit/Ocean"
{
    Properties
    {
        _HeightMap("Height Map", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
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
                v.vertex.y = height * 3;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float4 normalColor = tex2D(_NormalMap, i.uv);
                normalColor = normalColor * 0.5 + 0.5; // Transform from [-1, 1] to [0, 1]
                return normalColor;
            }
            ENDCG
        }
    }
}
