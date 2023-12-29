// Upgrade NOTE: replaced 'mul(UNITY_MATRIX_MVP,*)' with 'UnityObjectToClipPos(*)'

Shader "Unlit/Ocean"
{
    Properties
    {
        _HeightMap("Height Map", 2D) = "white" {}
        _NormalMap("Normal Map", 2D) = "bump" {}
        _Color("Color", Color) = (1, 1, 1, 1)
        _SpecColor ("Specular Color", Color) = (1, 1, 1, 1)
        _Shininess ("Shininess", Float) = 10
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

            uniform float4 _LightColor0; //From UnityCG
            
            uniform sampler2D_half _HeightMap;
            uniform sampler2D_half _NormalMap;
            uniform float4 _Color;
            uniform float4 _SpecColor;
            uniform float _Shininess;

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
                float4 texel = tex2Dlod(_HeightMap, float4(v.uv, 0, 0));
                v.vertex.x += texel.x;
                v.vertex.y = texel.y;
                v.vertex.z += texel.z;

                o.posWorld = mul(unity_ObjectToWorld, v.vertex);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float3 normal = normalize(tex2D(_NormalMap, i.uv));
                float3 viewDirection = normalize(_WorldSpaceCameraPos - i.posWorld.xyz);

                float3 ambient = UNITY_LIGHTMODEL_AMBIENT.rgb * _Color.rgb; //Ambient component

                float3 diffuse = _LightColor0.rgb * _Color.rgb * max(0.0, dot(normal, _WorldSpaceLightPos0.xyz)); //Diffuse component
                
                //
                // float3 vert2LightSource = _WorldSpaceLightPos0.xyz - i.posWorld.xyz;
                // float oneOverDistance = 1.0 / length(vert2LightSource);
                // float attenuation = lerp(1.0, oneOverDistance, _WorldSpaceLightPos0.w);
                // float3 lightDirection = _WorldSpaceLightPos0.xyz - i.posWorld.xyz * _WorldSpaceLightPos0.w;
                //
                // float3 ambientLighting = UNITY_LIGHTMODEL_AMBIENT.rgb * _Color.rgb; //Ambient component
                // float3 diffuseReflection = attenuation * _LightColor0.rgb * _Color.rgb * max(0.0, dot(normal, lightDirection)); //Diffuse component
                //
                // // float3 specularReflection;
                // // if (dot(normal2, lightDirection) < 0.0) //Light on the wrong side - no specular
                // // {
                // //     specularReflection = float3(0.0, 0.0, 0.0);
                // // }
                // // else
                // // {
                // //     //Specular component
                // //     specularReflection = attenuation * _LightColor0.rgb * _SpecColor.rgb * pow(max(0.0, dot(reflect(-lightDirection, normal), viewDirection)), _Shininess);
                // // }

                float3 color = ambient + diffuse;
                
                return float4(color, 1);
            }
            ENDCG
        }
    }
}
