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
                uint index : TEXCOORD1;
            };

            StructuredBuffer<Vertex> _VerticesBuffer;
            StructuredBuffer<uint> _IndicesBuffer;
            StructuredBuffer<uint> _VisibleMeshletIndicesBuffer;

            static const float3 _Colors[8] = {
                float3(0.85, 0.35, 0.05),
                float3(0.10, 0.35, 0.90),
                float3(0.15, 0.80, 0.20),
                float3(0.90, 0.15, 0.15),
                float3(0.60, 0.10, 0.80),
                float3(0.50, 0.40, 0.30),
                float3(0.90, 0.75, 0.05),
                float3(0.05, 0.70, 0.70)
            };

            v2f vert(appdata v)
            {
                uint visibleMeshletIndex = v.instanceID;
                uint triangleIndex = v.vertexID / 3;
                uint vertexInTriangle = v.vertexID % 3;

                uint currentIndex = 3 * (MAX_PRIMS * visibleMeshletIndex + triangleIndex) + vertexInTriangle;
                uint vertexIndex = _IndicesBuffer[currentIndex];
                float3 position = _VerticesBuffer[vertexIndex].Position;

                v2f o;
                o.vertex = UnityObjectToClipPos(position);
                uint globalIndex = _VisibleMeshletIndicesBuffer[visibleMeshletIndex];
                o.index = globalIndex;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float4 col = float4(_Colors[i.index % 8], 1);
                return col;
            }
            ENDCG

        }
    }
}