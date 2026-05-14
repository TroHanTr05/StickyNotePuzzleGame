Shader "Custom/PixelationShaderUnity3D"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _PixelResolution ("Pixel Resolution", Float) = 120
    }

    SubShader
    {
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
            float4 _MainTex_TexelSize;
            float _PixelResolution;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate aspect ratio
                float aspect = _ScreenParams.y / _ScreenParams.x;

                // Pixel size based on horizontal resolution
                float pixelSizeX = 1.0 / _PixelResolution;
                float pixelSizeY = pixelSizeX / aspect;

                // Snap UVs to pixel grid
                float2 pixelatedUV;
                pixelatedUV.x = floor(i.uv.x / pixelSizeX) * pixelSizeX;
                pixelatedUV.y = floor(i.uv.y / pixelSizeY) * pixelSizeY;

                // Sample texture using snapped UVs
                fixed4 col = tex2D(_MainTex, pixelatedUV);

                return col;
            }
            ENDCG
        }
    }

    Fallback Off
}