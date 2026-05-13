Shader "Custom/PixelationShaderMetal"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PixelResolution ("Pixel Resolution", Range(1, 512)) = 60
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Overlay" }
        LOD 100

        Pass
        {
            ZTest Always
            Cull Off
            ZWrite Off

            CGPROGRAM
            // Use a vertex/fragment pair that’s known to work on Metal.
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            
            #include "HLSLSupport.cginc"

            sampler2D _MainTex;
            float _PixelResolution;

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

            half4 frag(v2f i) : SV_Target
            {
                // Pixelate the UVs
                float2 pixelUV = floor(i.uv * _PixelResolution) / _PixelResolution;
                return tex2D(_MainTex, pixelUV);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}