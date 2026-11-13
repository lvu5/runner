Shader "InfiniteRunner/SaigonSky"
{
    SubShader
    {
        Tags { "Queue"="Background" "RenderType"="Background" "PreviewType"="Skybox" }
        Cull Off ZWrite Off
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct v2f { float4 vertex : SV_POSITION; float3 direction : TEXCOORD0; };
            v2f vert(float4 vertex : POSITION)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(vertex);
                o.direction = vertex.xyz;
                return o;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                // Humid tropical afternoon: warm hazy horizon fading to a deep blue zenith.
                float3 d = normalize(i.direction);
                float elevation = saturate(d.y);
                float3 horizon = float3(0.97, 0.88, 0.74);
                float3 middle = float3(0.62, 0.8, 0.93);
                float3 zenith = float3(0.22, 0.52, 0.86);
                float3 sky = lerp(horizon, middle, smoothstep(0.0, 0.22, elevation));
                sky = lerp(sky, zenith, smoothstep(0.18, 0.95, elevation));
                float sun = dot(d, normalize(float3(-0.5, 0.32, 1)));
                sky = lerp(sky, float3(1, 0.86, 0.6), pow(saturate(sun), 24) * 0.35);
                sky = lerp(sky, float3(1, 0.97, 0.85), smoothstep(0.9985, 0.999, sun));
                // Below the horizon blend into the fog colour so the far ground has no seam.
                sky = lerp(sky, float3(0.93, 0.86, 0.74), saturate(-d.y * 6));
                return fixed4(sky, 1);
            }
            ENDCG
        }
    }
    Fallback Off
}
