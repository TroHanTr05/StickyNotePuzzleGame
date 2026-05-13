Shader "Hidden/ShadowComposite"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
        _ShadowTex ("Shadow Source", 2D) = "clear" {}
        _ShadowColor ("Shadow Color", Color) = (0, 0, 0.5, 1)
        _ShadowOffset ("Shadow Offset Pixels", Vector) = (0, 0, 0, 0)
        _RenderInFront ("Render In Front", Float) = 0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _ShadowTex;
            float4 _MainTex_TexelSize;
            fixed4 _ShadowColor;
            float4 _ShadowOffset;
            float _RenderInFront;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseCol = tex2D(_MainTex, i.uv);

                // _ShadowOffset is passed in full-screen pixels by PixelationManager2D_v5.
                float2 shadowUV = i.uv - (_ShadowOffset.xy * _MainTex_TexelSize.xy);
                fixed shadowAlpha = tex2D(_ShadowTex, shadowUV).a * _ShadowColor.a;

                fixed3 rgb = lerp(baseCol.rgb, _ShadowColor.rgb, saturate(shadowAlpha));
                return fixed4(rgb, baseCol.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
