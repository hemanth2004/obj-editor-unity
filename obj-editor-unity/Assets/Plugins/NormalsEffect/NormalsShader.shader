Shader "Ogxd/Normals"
{
   Properties
   {
      [Header(Face Normals)]
      [Toggle(_FACENORMALS)] _FaceNormals("Enabled", Float) = 1.0
      _FaceNormalsColor("Color", Color) = (1 ,0 ,0 ,1)
      _FaceNormalsLength("Length", Float) = 0.4

      [Header(Vertex Normals)]
      [Toggle(_VERTEXNORMALS)] _VertexNormals("Enabled", Float) = 0.0
      _VertexNormalsColor("Color", Color) = (0 ,1 ,0 ,1)
      _VertexNormalsLength("Length", Float) = 0.4

      [Header(Vertex Tangents)]
      [Toggle(_VERTEXTANGENTS)] _VertexTangents("Enabled", Float) = 0.0
      _VertexTangentsColor("Color", Color) = (0 ,0 ,1 ,1)
      _VertexTangentsLength("Length", Float) = 0.4
   }

   SubShader
   {
      Tags { "Queue" = "Transparent" "RenderType" = "Transparent" "IgnoreProjector" = "True" }

      Pass {
         Name "Mask"

         Cull Back
         ZWrite On
         ColorMask 0
      }

      Pass
      {
         Name "Density"

         Cull Back
         ZWrite On
         Blend SrcAlpha OneMinusSrcAlpha

         CGPROGRAM

         #pragma vertex vert
         #pragma fragment frag
         #pragma geometry geom
         #include "UnityCG.cginc"
         #include "UnityLightingCommon.cginc"

         #pragma multi_compile __ _FACENORMALS
         #pragma multi_compile __ _VERTEXNORMALS
         #pragma multi_compile __ _VERTEXTANGENTS

         struct v2g
         {
            float2 uv : TEXCOORD0;
            float4 pos_local : POSITION;
            float3 pos_world : VECTOR3;
            float3 normal : NORMAL;
            float3 tangent : TANGENT;
         };

         struct g2f
         {
            float4 pos_clip : SV_POSITION;
            half4 color : VECTOR4;
         };

         v2g vert(appdata_full v)
         {
            v2g o;
            o.pos_world = mul(unity_ObjectToWorld, v.vertex);
            o.pos_local = v.vertex;
            o.uv = v.texcoord;
            o.normal = v.normal;
            o.tangent = v.tangent;
            return o;
         }

#ifdef _FACENORMALS
         half4 _FaceNormalsColor;
         float _FaceNormalsLength;
#endif

#ifdef _VERTEXNORMALS
         half4 _VertexNormalsColor;
         float _VertexNormalsLength;
#endif

#ifdef _VERTEXTANGENTS
         half4 _VertexTangentsColor;
         float _VertexTangentsLength;
#endif

         [maxvertexcount(10)]
         void geom(triangle v2g i[3], inout LineStream<g2f> tristream)
         {
            g2f o;

            float3 AB = i[1].pos_local.xyz - i[0].pos_local.xyz;
            float3 AC = i[2].pos_local.xyz - i[0].pos_local.xyz;
            float3 BC = i[2].pos_local.xyz - i[1].pos_local.xyz;
            float3 normal = normalize(cross(AB, AC));

            float3 normal_view = UNITY_MATRIX_IT_MV[2].xyz;

            float3 center = (i[0].pos_local + i[1].pos_local + i[2].pos_local) / 3;

            float3 tangent = cross(normal_view, normal);

#ifdef _FACENORMALS
            o.pos_clip = UnityObjectToClipPos(center);
            o.color = _FaceNormalsColor;
            tristream.Append(o);

            o.pos_clip = UnityObjectToClipPos(center + normal * _FaceNormalsLength);
            o.color = _FaceNormalsColor;
            tristream.Append(o);

            tristream.RestartStrip();
#endif

#ifdef _VERTEXNORMALS
            o.pos_clip = UnityObjectToClipPos(i[0].pos_local);
            o.color = _VertexNormalsColor;
            tristream.Append(o);

            o.pos_clip = UnityObjectToClipPos(i[0].pos_local + i[0].normal * _VertexNormalsLength);
            o.color = _VertexNormalsColor;
            tristream.Append(o);

            tristream.RestartStrip();

            o.pos_clip = UnityObjectToClipPos(i[1].pos_local);
            o.color = _VertexNormalsColor;
            tristream.Append(o);

            o.pos_clip = UnityObjectToClipPos(i[1].pos_local + i[1].normal * _VertexNormalsLength);
            o.color = _VertexNormalsColor;
            tristream.Append(o);

            tristream.RestartStrip();

            o.pos_clip = UnityObjectToClipPos(i[2].pos_local);
            o.color = _VertexNormalsColor;
            tristream.Append(o);

            o.pos_clip = UnityObjectToClipPos(i[2].pos_local + i[2].normal * _VertexNormalsLength);
            o.color = _VertexNormalsColor;
            tristream.Append(o);

            tristream.RestartStrip();
#endif

#ifdef _VERTEXTANGENTS
            o.pos_clip = UnityObjectToClipPos(i[0].pos_local);
            o.color = _VertexTangentsColor;
            tristream.Append(o);

            o.pos_clip = UnityObjectToClipPos(i[0].pos_local + i[0].tangent * _VertexTangentsLength);
            o.color = _VertexTangentsColor;
            tristream.Append(o);

            tristream.RestartStrip();

            o.pos_clip = UnityObjectToClipPos(i[1].pos_local);
            o.color = _VertexTangentsColor;
            tristream.Append(o);

            o.pos_clip = UnityObjectToClipPos(i[1].pos_local + i[1].tangent * _VertexTangentsLength);
            o.color = _VertexTangentsColor;
            tristream.Append(o);

            tristream.RestartStrip();

            o.pos_clip = UnityObjectToClipPos(i[2].pos_local);
            o.color = _VertexTangentsColor;
            tristream.Append(o);

            o.pos_clip = UnityObjectToClipPos(i[2].pos_local + i[2].tangent * _VertexTangentsLength);
            o.color = _VertexTangentsColor;
            tristream.Append(o);

            tristream.RestartStrip();
#endif
         }

         half4 frag(g2f input) : SV_Target
         {
            return input.color;
         }
         ENDCG
      }
   }
}