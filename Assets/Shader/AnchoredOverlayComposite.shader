Shader "Hidden/Dreadforge/AnchoredOverlayComposite"
{
    Properties
    {
        _MainTex ("Base Texture", 2D) = "white" {}
        _OverlayTex ("Overlay Texture", 2D) = "white" {}
        _OverlayRect ("Overlay Rect", Vector) = (0, 0, 1, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
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
            float4 _MainTex_TexelSize;
            float4 _OverlayRect;

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

                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0)
                {
                    o.uv.y = 1 - o.uv.y;
                }
                #endif

                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 baseColor = tex2D(_MainTex, i.uv);
                float2 safeSize = max(abs(_OverlayRect.zw), float2(0.00001, 0.00001));
                float2 overlayUv = (i.uv - _OverlayRect.xy) / safeSize;
                float inside = step(0, overlayUv.x) *
                    step(0, overlayUv.y) *
                    step(overlayUv.x, 1) *
                    step(overlayUv.y, 1);
                fixed4 overlayColor = tex2D(_OverlayTex, saturate(overlayUv)) * inside;

                return lerp(baseColor, overlayColor, overlayColor.a);
            }
            ENDCG
        }
    }
}
