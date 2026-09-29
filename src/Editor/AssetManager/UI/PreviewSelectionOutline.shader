Shader "Hidden/ee4v/PreviewSelectionOutline"
{
    Properties
    {
        _MainTex ("Preview", 2D) = "black" {}
        _WithoutSelectionTex ("Preview Without Selection", 2D) = "black" {}
        _OutlineColor ("Outline Color", Color) = (1, 0.4, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" }
        Cull Off ZWrite Off ZTest Always
        Blend One Zero

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragDifference
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _WithoutSelectionTex;

            fixed4 fragDifference(v2f_img input) : SV_Target
            {
                float4 preview = tex2D(_MainTex, input.uv);
                float4 withoutSelection =
                    tex2D(_WithoutSelectionTex, input.uv);
                float4 difference = abs(preview - withoutSelection);
                float changed = step(1.0 / 255.0,
                    max(max(difference.r, difference.g),
                        max(difference.b, difference.a)));
                return fixed4(changed, changed, changed, 1);
            }
            ENDCG
        }

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment fragOutline
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_TexelSize;
            fixed4 _OutlineColor;

            fixed4 fragOutline(v2f_img input) : SV_Target
            {
                float2 stepSize = _MainTex_TexelSize.xy;
                float center = tex2D(_MainTex, input.uv).r;
                float neighbor = 0;

                [unroll]
                for (int x = -2; x <= 2; x++)
                {
                    [unroll]
                    for (int y = -2; y <= 2; y++)
                    {
                        neighbor = max(neighbor,
                            tex2D(_MainTex,
                                input.uv + float2(x, y) * stepSize).r);
                    }
                }

                float edge = (1 - step(0.5, center)) * step(0.5, neighbor);
                return fixed4(_OutlineColor.rgb, _OutlineColor.a * edge);
            }
            ENDCG
        }
    }
    Fallback Off
}
