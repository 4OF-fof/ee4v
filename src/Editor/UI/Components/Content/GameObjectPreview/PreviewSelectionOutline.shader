Shader "Hidden/ee4v/PreviewSelectionOutline"
{
    Properties
    {
        _MainTex ("Preview", 2D) = "black" {}
        _OutlineColor ("Outline Color", Color) = (1, 0.4, 0, 1)
        _PickColor ("Pick Color", Vector) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Overlay" }
        Cull Off ZWrite Off ZTest Always
        Blend One Zero

        Pass
        {
            Name "Outline"
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

        Pass
        {
            Name "SelectionGeometry"
            Cull Off
            ZWrite Off
            ZTest Always
            Blend One Zero

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            float4 vert(float4 vertex : POSITION) : SV_POSITION
            {
                return UnityObjectToClipPos(vertex);
            }

            fixed4 frag() : SV_Target
            {
                return fixed4(1, 1, 1, 1);
            }
            ENDCG
        }

        Pass
        {
            Name "Pick"
            Cull Off
            ZWrite On
            ZTest LEqual
            Blend One Zero

            CGPROGRAM
            #pragma vertex vertPick
            #pragma fragment fragPick
            #include "UnityCG.cginc"

            float4 _PickColor;

            struct PickVertex
            {
                float4 vertex : POSITION;
            };

            struct PickFragment
            {
                float4 position : SV_POSITION;
            };

            PickFragment vertPick(PickVertex input)
            {
                PickFragment output;
                output.position = UnityObjectToClipPos(input.vertex);
                return output;
            }

            float4 fragPick(PickFragment input) : SV_Target
            {
                return _PickColor;
            }
            ENDCG
        }
    }
    Fallback Off
}
