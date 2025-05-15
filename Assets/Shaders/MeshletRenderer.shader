Shader "Nanite/MeshletRendering"
{
    SubShader
    {
        Tags
        {
            "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"
        }
        LOD 200
        Pass
        {
            Tags
            {
                "LightMode"="UniversalForward"
            }
            Cull Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #define MAX_PRIMS 126

            #include "UnityCG.cginc"

            struct Meshlet
            {
                uint VertOffset;
                uint PrimOffset;
                uint VertCount;
                uint PrimCount;
            };

            struct Vertex
            {
                float3 Position;
            };

            struct appdata
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 color : COLOR;
            };

            StructuredBuffer<Vertex> _VerticesBuffer;
            StructuredBuffer<uint> _IndicesBuffer;

            v2f vert(appdata v)
            {
                uint meshletIndex = v.instanceID;
                uint triangleIndex = v.vertexID / 3;
                uint vertexInTriangle = v.vertexID % 3;

                uint currentIndex = 3 * (MAX_PRIMS * meshletIndex + triangleIndex) + vertexInTriangle;
                uint vertexIndex = _IndicesBuffer[currentIndex];
                float3 position = _VerticesBuffer[vertexIndex].Position;

                v2f o;
                o.vertex = UnityObjectToClipPos(position);
                o.color = fixed3(0.5, 0.5, 0.5);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = fixed4(i.color, 1);
                return col;
            }
            ENDCG

        }
    }
}