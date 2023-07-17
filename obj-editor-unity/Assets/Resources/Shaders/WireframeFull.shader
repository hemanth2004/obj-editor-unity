/*
Copyright 2020 PROTOZOA LLC

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

CGINCLUDE
#include "UnityCG.cginc"

struct v2g
{
    float4 vertex : POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
};

struct g2f
{
    float4 vertex : SV_POSITION;
    float3 normal : NORMAL;
    float2 uv : TEXCOORD0;
    float3 wpos : TEXCOORD1;
    float vis : TEXCOORD2;
};


sampler2D _MainTex;
float4 _MainTex_ST;

half4 _Color;
half _Bloom, _Shade;
float _FrontVisibility, _BackVisibility, _EdgeVisibility, _CrossVisibility;
half _Padding, _Extrude;

v2g vert(v2g v){return v;}

g2f getg2f(v2g v,float vis = 1.0)//single vertex
{
    g2f o;
    v.vertex.xyz += v.normal.xyz * _Extrude;
    o.vertex = UnityObjectToClipPos(v.vertex);
    o.normal = UnityObjectToWorldNormal(v.normal);
    o.uv = v.uv;
    o.wpos = mul(unity_ObjectToWorld,v.vertex).xyz;
    o.vis = vis;
    return o;
}

g2f getg2f(v2g v[3],float vis = 1.0)//triangle center point
{
    g2f o;
    float3 lp = (v[0].vertex + v[1].vertex + v[2].vertex) * 0.3333333;
    float3 ln = normalize(v[0].normal + v[1].normal + v[2].normal);
    lp.xyz += ln.xyz * _Extrude;
    o.vertex = UnityObjectToClipPos(lp);
    o.normal = UnityObjectToWorldNormal(ln);
    o.uv = (v[0].uv + v[1].uv + v[2].uv) * 0.3333333;
    o.wpos = mul(unity_ObjectToWorld,float4(lp,1)).xyz;
    o.vis = vis;
    return o;
}

half4 frag (g2f i) : SV_Target
{
    float3 vd = normalize(i.wpos - _WorldSpaceCameraPos);
    float facing = -dot(i.normal.xyz , vd);
    float vis = smoothstep(-0.1,0.5,facing) * _FrontVisibility + smoothstep(0.1,-0.5,facing) * _BackVisibility;
    vis = saturate(vis) * i.vis;
    if(vis < 0.001) discard;
    half4 col = tex2D(_MainTex, i.uv * _MainTex_ST.xy + _MainTex_ST.zw);
    col.a = 1.0;
    col = col * half4(_Color.rgb , _Color.a * vis);
    col.rgb *= 1.0 + 10.0 * _Bloom * saturate(facing);

    float light = 0.5 + 0.5 * dot(i.normal.xyz,float3(0,1,0));
    return col * lerp(1.0,light,_Shade);
}
ENDCG

Shader "Protozoa/Wireframe/Full"
{
    Properties
    {
        [Header(RGB)]
        _Color ("Color", Color) = (1,1,1,1)
        _Bloom ("Bloom", Range(0.0,1.0)) = 0.0
        _Shade ("Shade", Range(0.0,1.0)) = 0.0
        _MainTex ("Texture", 2D) = "white" {}

        [Header(Alpha)]
        _FrontVisibility ("Front Visibility", Range(0.0,1.0)) = 1.0
        _BackVisibility ("Back Visibility", Range(0.0,1.0)) = 0.1
        _EdgeVisibility ("Edge Visibility", Range(0.0,1.0)) = 0.1
        _CrossVisibility ("Cross Visibility", Range(0.0,1.0)) = 0.1

        [Header(Form)]
        _Padding ("Padding", Range(0.0,1.0)) = 0.1
        _Extrude ("Extrude", Range(0.0,0.1)) = 0.0
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry+5" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZTest LEqual
        ZWrite Off
        Cull Off

        Pass
        {
            Name "LINES"
            CGPROGRAM
            #pragma vertex vert
            #pragma geometry geom
            #pragma fragment frag

            [maxvertexcount(32)]
            void geom (triangle v2g v[3] , uint pid : SV_PRIMITIVEID , inout LineStream<g2f> stream)
            {
                g2f gc = getg2f(v,_CrossVisibility);
                g2f g0 = getg2f(v[0],_EdgeVisibility);
                g2f g1 = getg2f(v[1],_EdgeVisibility);
                g2f g2 = getg2f(v[2],_EdgeVisibility);

                g0.vertex = lerp(g0.vertex,gc.vertex,_Padding);
                g1.vertex = lerp(g1.vertex,gc.vertex,_Padding);
                g2.vertex = lerp(g2.vertex,gc.vertex,_Padding);

                stream.Append(g0);
                stream.Append(g1);
                stream.Append(g2);
                stream.Append(g0);
                stream.RestartStrip();

                g0.vis = _CrossVisibility;
                g1.vis = _CrossVisibility;
                g2.vis = _CrossVisibility;

                stream.Append(g0);
                stream.Append(gc);
                stream.RestartStrip();
                stream.Append(g1);
                stream.Append(gc);
                stream.RestartStrip();
                stream.Append(g2);
                stream.Append(gc);
                stream.RestartStrip();
            }
            ENDCG
        }
    }
}