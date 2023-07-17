Shader "Custom/MatcapShader" {
    Properties{
        _MainTex("Matcap", 2D) = "white" {}
        _Color("Color", Color) = (1, 1, 1, 1)
    }

        SubShader{
            Tags { "RenderType" = "Opaque" }
            LOD 100

            CGPROGRAM
            #pragma surface surf Lambert vertex:vert

            sampler2D _MainTex;
            fixed4 _Color;

            struct Input {
                float2 uv_MainTex;
                float3 worldNormal;
            };

            void surf(Input IN, inout SurfaceOutput o) {
                fixed4 texColor = tex2D(_MainTex, IN.uv_MainTex) * _Color;
                o.Albedo = texColor.rgb;
                o.Alpha = texColor.a;
            }

            void vert(inout appdata_full v, out Input o) {
                UNITY_INITIALIZE_OUTPUT(Input, o);
                o.uv_MainTex = v.texcoord.xy;
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
            }
            ENDCG
        }

            FallBack "Diffuse"
}
