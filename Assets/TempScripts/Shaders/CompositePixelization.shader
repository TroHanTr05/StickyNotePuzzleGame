Shader "Custom/CompositePixelation2D_v3"
{
    Properties
    {
        _MainTex ("Base Full-Res Texture", 2D) = "white" {}
        _PixelTex ("Pixelated Layer", 2D) = "black" {}
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            ZTest Always
            Cull Off
            ZWrite Off

            CGPROGRAM
            #pragma target 3.0
            #pragma vertex vert_img
            #pragma fragment frag

            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _PixelTex;

            fixed4 frag(v2f_img i) : SV_Target
            {
                fixed4 mainColor = tex2D(_MainTex, i.uv);
                fixed4 pixelColor = tex2D(_PixelTex, i.uv);

                return lerp(mainColor, pixelColor, pixelColor.a);
            }
            ENDCG
        }
    }
}