Shader "InfiniteRunner/PrototypeLit"
{
    Properties
    {
        _BaseColor ("Base Color", Color) = (1, 1, 1, 1)
        _Color ("Color", Color) = (1, 1, 1, 1)
        _EmissionColor ("Emission", Color) = (0, 0, 0, 0)
        _Smoothness ("Smoothness", Range(0, 1)) = 0.25
        _Unlit ("Unlit", Range(0, 1)) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #pragma multi_compile_fwdbase
            #pragma multi_compile_fog
            #include "UnityCG.cginc"
            #include "Lighting.cginc"
            #include "AutoLight.cginc"
            struct appdata { float4 vertex : POSITION; float3 normal : NORMAL; };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldPosition : TEXCOORD0;
                half3 normal : TEXCOORD1;
                UNITY_FOG_COORDS(2)
                SHADOW_COORDS(3)
            };
            fixed4 _BaseColor;
            fixed4 _EmissionColor;
            half _Unlit;
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldPosition = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.normal = UnityObjectToWorldNormal(v.normal);
                UNITY_TRANSFER_FOG(o, o.pos);
                TRANSFER_SHADOW(o);
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                half3 n = normalize(i.normal);
                half3 ambient = lerp(unity_AmbientEquator.rgb, unity_AmbientSky.rgb, saturate(n.y));
                ambient = lerp(ambient, unity_AmbientGround.rgb, saturate(-n.y));
                half diffuse = saturate(dot(n, normalize(_WorldSpaceLightPos0.xyz)));
                UNITY_LIGHT_ATTENUATION(shadow, i, i.worldPosition);
                half3 lighting = lerp(ambient + _LightColor0.rgb * diffuse * shadow * 0.5h, half3(1, 1, 1), _Unlit);
                fixed4 color = fixed4(_BaseColor.rgb * lighting + _EmissionColor.rgb, _BaseColor.a);
                UNITY_APPLY_FOG(i.fogCoord, color);
                return color;
            }
            ENDCG
        }
        UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
    }
    Fallback Off
}
