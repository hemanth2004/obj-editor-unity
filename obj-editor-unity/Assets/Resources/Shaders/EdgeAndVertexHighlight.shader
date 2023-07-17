Shader "Custom/EdgeAndVertexHighlight"
{
    Properties
    {
        _Color("Color", Color) = (1, 1, 0, 1)
        _VertexSize("Vertex Size", Range(0.001, 0.1)) = 0.01
    }
        SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            float4 _Color;
            float _VertexSize;

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);

                // Highlight vertices with a small square
                o.vertex.xyzw += float4(-_VertexSize, -_VertexSize, 0, 0);
                o.color = _Color;
                o.color.a = 1.0;
                o.color.rgb *= o.color.a;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}
