Shader "Nanity/MeshletRendering"
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
            ByteAddressBuffer _IndicesBuffer;
            StructuredBuffer<uint> _VisibleMeshletIndicesBuffer;

            v2f vert(appdata v)
            {
                uint visibleMeshletIndex = v.instanceID;

                uint currentIndex = 3 * (MAX_PRIMS * visibleMeshletIndex) + v.vertexID;
                uint vertexIndex = _IndicesBuffer.Load(currentIndex * 4);
                float3 position = _VerticesBuffer[vertexIndex].Position;

                v2f o;
                o.vertex = UnityObjectToClipPos(position);
                uint globalIndex = _VisibleMeshletIndicesBuffer[visibleMeshletIndex];
                o.index = globalIndex;
                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                float3 col = float3(
                    float(i.index & 1),
                    float(i.index & 3) / 4,
                    float(i.index & 7) / 8
                );
                return float4(col, 1);
            }
            ENDCG

        }
    }
}