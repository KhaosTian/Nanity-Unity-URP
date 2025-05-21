Shader "Nanity/MeshletRendering"
{
    Properties
    {
        _BackFaceColor("Back Face Color", Color) = (0,0,0,1)
    }
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

            struct InstancePara
            {
                float4x4 model;
                float4 color;
            };

            struct appdata
            {
                uint vertexID : SV_VertexID;
                uint instanceID : SV_InstanceID;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
                uint index: TEXCOORD0;
            };

            StructuredBuffer<Vertex> _VerticesBuffer;
            StructuredBuffer<uint> _VisibleMeshletIndicesBuffer;
            int _MeshletCountPerInstance;

            StructuredBuffer<Meshlet> _MeshletsBuffer;
            StructuredBuffer<uint> _MeshletPrimitiveIndicesBuffer;
            StructuredBuffer<uint> _MeshletVertexIndicesBuffer;
            StructuredBuffer<InstancePara> _InstanceParasBuffer;

            float4 _BackfaceColor;

            uint3 UnpackPrimitive(uint primitive)
            {
                return uint3((primitive >> 0) & 0xFF, (primitive >> 8) & 0xFF, (primitive >> 16) & 0xFF);
            }

            uint3 GetPrimitive(Meshlet m, uint index)
            {
                return UnpackPrimitive(_MeshletPrimitiveIndicesBuffer[m.PrimOffset + index]);
            }

            v2f vert(appdata v)
            {
                uint visibleMeshletIndex = v.instanceID;

                uint globalMeshletIndex = _VisibleMeshletIndicesBuffer[visibleMeshletIndex];
                uint instanceIndex = globalMeshletIndex / _MeshletCountPerInstance;
                uint meshletIndex = globalMeshletIndex % _MeshletCountPerInstance;

                Meshlet m = _MeshletsBuffer[meshletIndex];
                uint primitiveIndex = v.vertexID / 3; // meshlet的第primitiveIndex个三角形
                uint vertexInPrimitive = v.vertexID % 3; // 局部三角形的第vertexInPrimitive个顶点
                // 获取局部三角形的三个索引，对于超过meshlet索引数量的，按照第一个三角形算（退化三角形）
                uint3 localTri = v.vertexID < m.PrimCount * 3 ? GetPrimitive(m, primitiveIndex) : GetPrimitive(m, 0);

                uint vertexIndex = _MeshletVertexIndicesBuffer[m.VertOffset + localTri[vertexInPrimitive]];
                float3 position = _VerticesBuffer[vertexIndex].Position;

                InstancePara para = _InstanceParasBuffer[instanceIndex];

                v2f o;
                unity_ObjectToWorld = para.model;
                o.vertex = UnityObjectToClipPos(position);
                o.color = para.color;
                o.index = globalMeshletIndex;
                return o;
            }

            float4 frag(v2f i, bool facing : SV_IsFrontFace) : SV_Target
            {
                float4 col = float4(
                    float(i.index & 1),
                    float(i.index & 3) / 4,
                    float(i.index & 7) / 8,
                    1
                );
                return facing ? col : _BackfaceColor;
            }
            ENDCG

        }
    }
}