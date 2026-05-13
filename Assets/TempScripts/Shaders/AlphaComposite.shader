Shader "Hidden/AlphaComposite"
{
    Properties
    {
        _MainTex ("Base", 2D) = "white" {}
        _OverlayTex ("Overlay", 2D) = "clear" {}
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
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _OverlayTex;

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
                fixed4 overlayCol = tex2D(_OverlayTex, i.uv);

                // Straight-alpha overlay compositing.
                fixed alpha = saturate(overlayCol.a);
                fixed3 rgb = lerp(baseCol.rgb, overlayCol.rgb, alpha);

                return fixed4(rgb, baseCol.a);
            }
            ENDCG
        }
    }

    Fallback Off
}
