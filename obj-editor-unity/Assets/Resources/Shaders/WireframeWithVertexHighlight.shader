Shader "Custom/VertexBillboardShader" {
    Properties{
        _Color("Color", Color) = (1, 1, 1, 1)
        _Size("Size", Range(0, 1)) = 0.1
        _CameraPosition("Camera Position", Vector) = (0, 0, 0, 0)
    }

        SubShader{
            Tags { "RenderType" = "Opaque" }
            LOD 200

            Pass {
                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag

                #pragma geometry geom

                sampler2D _MainTex;
                fixed4 _Color;
                float _Size;
                float4 _CameraPosition;

                struct appdata {
                    float4 vertex : POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct v2g {
                    float4 vertex : SV_POSITION;
                    float2 uv : TEXCOORD0;
                };

                struct g2f {
                    float4 vertex : SV_POSITION;
                    float2 uv : TEXCOORD0;
                };

                v2g vert(appdata v) {
                    v2g o;
                    o.vertex = UnityObjectToClipPos(v.vertex);
                    o.uv = v.uv;
                    return o;
                }

                [maxvertexcount(4)]
                void geom(point v2g input[1], inout TriangleStream<g2f> OutputStream) {
                    float3 viewDir = normalize(_CameraPosition.xyz - mul(unity_ObjectToWorld, input[0].vertex).xyz);

                    // Calculate the right and up vectors for the square based on the view direction
                    float3 right = normalize(cross(viewDir, float3(0, 1, 0)));
                    float3 up = normalize(cross(right, viewDir));

                    // Calculate the vertices of the square
                    float halfSize = _Size * 0.5;
                    float4 v1 = input[0].vertex + float4(-right * halfSize + up * halfSize, 0);
                    float4 v2 = input[0].vertex + float4(right * halfSize + up * halfSize, 0);
                    float4 v3 = input[0].vertex + float4(-right * halfSize - up * halfSize, 0);
                    float4 v4 = input[0].vertex + float4(right * halfSize - up * halfSize, 0);

                    // Emit the vertices of the square
                    g2f output;
                    output.vertex = v1;
                    output.uv = float2(0, 1);
                    OutputStream.Append(output);

                    output.vertex = v2;
                    output.uv = float2(1, 1);
                    OutputStream.Append(output);

                    output.vertex = v3;
                    output.uv = float2(0, 0);
                    OutputStream.Append(output);

                    output.vertex = v4;
                    output.uv = float2(1, 0);
                    OutputStream.Append(output);
                }

                fixed4 frag(g2f i) : SV_Target {
                    return _Color;
                }
                ENDCG
            }
    }
}