using System.Collections.Generic;
using UnityEngine;

namespace Nanity
{
    public class NanityRendering : MonoBehaviour
    {
        private const int MAX_VERTS = 64;
        private const int MAX_PRIMS = 126;
        private const int KERNEL_SIZE_X = 64;

        // Shader 相关
        private Material m_MeshletMaterial;
        public ComputeShader CullingCompute;
        private int m_CullingKernelID;

        // Meshlet 资产相关
        public List<MeshletAsset> MeshletAssets = new List<MeshletAsset>();
        public MeshletAsset SelectedMeshletAsset;
        private MeshletCollection m_Collection;
        private Mesh m_SourceMesh;

        // 原始顶点数据
        private static readonly int VerticesBufferID = Shader.PropertyToID("_VerticesBuffer");
        private GraphicsBuffer m_VerticesBuffer;

        // Meshlet 数据
        private static readonly int MeshletsBufferID = Shader.PropertyToID("_MeshletsBuffer");
        private static readonly int MeshletVertexIndicesBufferID = Shader.PropertyToID("_MeshletVertexIndicesBuffer");

        private static readonly int MeshletPrimitiveIndicesBufferID =
            Shader.PropertyToID("_MeshletPrimitiveIndicesBuffer");

        private GraphicsBuffer m_MeshletsBuffer;
        private GraphicsBuffer m_MeshletVertexIndicesBuffer;
        private GraphicsBuffer m_MeshletPrimitiveIndicesBuffer;

        // 可见实例数据（Meshlet 索引）
        private static readonly int VisibleMeshletIndicesBufferID = Shader.PropertyToID("_VisibleMeshletIndicesBuffer");
        private GraphicsBuffer m_VisibleMeshletIndicesBuffer;


        // 间接参数缓冲区
        private static readonly int DrawArgsBufferID = Shader.PropertyToID("_DrawArgsBuffer");
        private GraphicsBuffer m_DrawArgsBuffer;
        private readonly uint[] m_DrawArgs = new uint[5] { MAX_PRIMS * 3, 0, 0, 0, 0 };

        // Mesh缓冲区
        private static readonly int MeshInfosBufferID = Shader.PropertyToID("_MeshInfosBuffer");
        private GraphicsBuffer m_MeshInfosBuffer;

        // Instance缓冲区
        private static readonly int InstanceParasBufferID = Shader.PropertyToID("_InstanceParasBuffer");
        private GraphicsBuffer m_InstanceParasBuffer;

        // InstanceRef
        private static readonly int InstanceRefsBufferID = Shader.PropertyToID("_InstanceRefsBuffer");
        private GraphicsBuffer m_InstanceRefsBuffer;

        // 包围体缓冲区
        private static readonly int BoundsDataBufferID = Shader.PropertyToID("_BoundsDataBuffer");
        private GraphicsBuffer m_BoundsDataBuffer;

        // Meshlet 总数
        private static readonly int MeshletCountID = Shader.PropertyToID("_MeshletCount");
        private static readonly int InstanceCountID = Shader.PropertyToID("_InstanceCount");
        private static readonly int MeshletCountPerInstanceID = Shader.PropertyToID("_MeshletCountPerInstance");
        private int m_MeshletCount;
        private int m_InstanceCount;
        private int m_MeshletCountPerInstance;

        private Bounds m_ProxyBounds;
        private int m_KernelGroupX;

        public int Row = 3;
        public int Column = 3;

        private void Start()
        {
            if (!IsValid()) return;

            m_Collection = SelectedMeshletAsset.Collection;
            m_SourceMesh = SelectedMeshletAsset.SourceMesh;

            m_InstanceCount = Row * Column;
            m_MeshletCountPerInstance = SelectedMeshletAsset.Collection.meshlets.Length;
            m_MeshletCount = m_MeshletCountPerInstance * m_InstanceCount;
            m_ProxyBounds = new Bounds(Vector3.zero, 1000.0f * Vector3.one);
            m_KernelGroupX = Mathf.CeilToInt(1.0f / KERNEL_SIZE_X * m_MeshletCount); // 计算剔除所需的线程组数

            InitBuffers();
            SetupShaders();
        }

        private bool IsValid()
        {
            if (!SelectedMeshletAsset)
            {
                Debug.LogWarning("Selected meshlet asset is missing.");
                return false;
            }

            if (!SelectedMeshletAsset.SourceMesh)
            {
                Debug.LogWarning("Source mesh is missing.");
                return false;
            }

            if (!SelectedMeshletAsset.SourceMesh.isReadable)
            {
                Debug.LogWarning("Source mesh is not readable.");
                return false;
            }

            return true;
        }

        private void InitInstanceParasBuffer()
        {
            var instanceParas = new InstancePara[m_InstanceCount];
            var parentPosition = transform.position;
            for (int r = 0; r < Row; r++)
            {
                for (int c = 0; c < Column; c++)
                {
                    int index = r * Column + c;

                    var modelMatrix = Matrix4x4.TRS(
                        parentPosition + new Vector3(c, r, 0),
                        Quaternion.identity, // No rotation
                        Vector3.one // No scaling
                    );

                    instanceParas[index].ModelMatrix = modelMatrix;
                    instanceParas[index].InstanceColor = Random.ColorHSV();
                }
            }

            m_InstanceParasBuffer =
                new GraphicsBuffer(GraphicsBuffer.Target.Structured, m_InstanceCount, InstancePara.SIZE);
            m_InstanceParasBuffer.name = nameof(m_InstanceParasBuffer);
            m_InstanceParasBuffer.SetData(instanceParas);
        }

        private void InitBuffers()
        {
            // 间接参数缓冲区
            m_DrawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 5 * sizeof(uint));
            m_DrawArgsBuffer.name = nameof(m_DrawArgsBuffer);
            m_DrawArgsBuffer.SetData(m_DrawArgs);

            // 输入顶点坐标缓冲区
            m_VerticesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, m_SourceMesh.vertices.Length,
                sizeof(float) * 3);
            m_VerticesBuffer.name = nameof(m_VerticesBuffer);
            m_VerticesBuffer.SetData(m_SourceMesh.vertices);

            // Meshlet缓冲区
            m_MeshletsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, m_MeshletCount, sizeof(uint) * 4);
            m_MeshletsBuffer.name = nameof(m_MeshletsBuffer);
            m_MeshletsBuffer.SetData(m_Collection.meshlets);

            // Meshlet Vertices索引缓冲区
            m_MeshletVertexIndicesBuffer =
                new GraphicsBuffer(GraphicsBuffer.Target.Structured, m_Collection.vertices.Length, sizeof(uint));
            m_MeshletVertexIndicesBuffer.name = nameof(m_MeshletVertexIndicesBuffer);
            m_MeshletVertexIndicesBuffer.SetData(m_Collection.vertices);

            // Meshlet Triangles索引缓冲区
            m_MeshletPrimitiveIndicesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured,
                m_Collection.triangles.Length, sizeof(uint));
            m_MeshletPrimitiveIndicesBuffer.name = nameof(m_MeshletPrimitiveIndicesBuffer);
            m_MeshletPrimitiveIndicesBuffer.SetData(m_Collection.triangles);

            // 可见 Meshlet 索引缓冲区
            m_VisibleMeshletIndicesBuffer =
                new GraphicsBuffer(GraphicsBuffer.Target.Append, m_MeshletCount, sizeof(uint));
            m_VisibleMeshletIndicesBuffer.name = nameof(m_VisibleMeshletIndicesBuffer);
            m_VisibleMeshletIndicesBuffer.SetData(new int[m_MeshletCount]);


            InitInstanceParasBuffer();
        }

        private void SetupShaders()
        {
            m_CullingKernelID = CullingCompute.FindKernel("CullingMain");

            CullingCompute.SetInt(MeshletCountID, m_MeshletCount);
            CullingCompute.SetBuffer(m_CullingKernelID, VisibleMeshletIndicesBufferID, m_VisibleMeshletIndicesBuffer);


            m_MeshletMaterial = new Material(Shader.Find("Nanity/MeshletRendering"));

            m_MeshletMaterial.SetInt(MeshletCountID, m_MeshletCount);
            m_MeshletMaterial.SetInt(InstanceCountID, m_InstanceCount);
            m_MeshletMaterial.SetInt(MeshletCountPerInstanceID, m_MeshletCountPerInstance);

            m_MeshletMaterial.SetBuffer(VerticesBufferID, m_VerticesBuffer);

            m_MeshletMaterial.SetBuffer(VisibleMeshletIndicesBufferID, m_VisibleMeshletIndicesBuffer);

            m_MeshletMaterial.SetBuffer(MeshletsBufferID, m_MeshletsBuffer);
            m_MeshletMaterial.SetBuffer(MeshletVertexIndicesBufferID, m_MeshletVertexIndicesBuffer);
            m_MeshletMaterial.SetBuffer(MeshletPrimitiveIndicesBufferID, m_MeshletPrimitiveIndicesBuffer);

            m_MeshletMaterial.SetBuffer(InstanceParasBufferID, m_InstanceParasBuffer);
        }

        private void Update()
        {
            if (!IsValid()) return;
            m_VisibleMeshletIndicesBuffer.SetCounterValue(0);
            CullingCompute.Dispatch(m_CullingKernelID, m_KernelGroupX, 1, 1);
            GraphicsBuffer.CopyCount(m_VisibleMeshletIndicesBuffer, m_DrawArgsBuffer, sizeof(uint) * 1);
            Graphics.DrawProceduralIndirect(m_MeshletMaterial, m_ProxyBounds, MeshTopology.Triangles, m_DrawArgsBuffer);
        }

        private void OnDestroy()
        {
            m_VerticesBuffer?.Release();

            m_MeshletsBuffer?.Release();
            m_MeshletVertexIndicesBuffer?.Release();
            m_MeshletPrimitiveIndicesBuffer?.Release();

            m_VisibleMeshletIndicesBuffer?.Release();

            m_DrawArgsBuffer?.Release();

            m_InstanceParasBuffer?.Release();
        }
    }
}