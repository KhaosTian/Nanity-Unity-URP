using UnityEngine;

namespace Nanite
{
    public class NaniteRendering : MonoBehaviour
    {
        private const int MAX_VERTS = 64;
        private const int MAX_PRIMS = 126;
        private const int KERNEL_SIZE_X = 64;

        // Shader 相关
        private Material m_MeshletMaterial;
        public ComputeShader CullingCompute;
        public ComputeShader ProcessingCompute;
        private int m_CullingKernelID;
        private int m_ProcessingKernelID;

        // Meshlet 资产相关
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

        // 可见实例的索引缓冲区
        private static readonly int IndicesBufferID = Shader.PropertyToID("_IndicesBuffer");
        private GraphicsBuffer m_IndicesBuffer;

        // 间接参数缓冲区
        private static readonly int DispatchArgsBufferID = Shader.PropertyToID("_DispatchArgsBuffer");
        private static readonly int DrawArgsBufferID = Shader.PropertyToID("_DrawArgsBuffer");
        private GraphicsBuffer m_DrawArgsBuffer;
        private GraphicsBuffer m_DispatchArgsBuffer;
        private readonly uint[] m_DispatchArgs = new uint[3] { 0, 1, 1 };
        private readonly uint[] m_DrawArgs = new uint[5] { MAX_PRIMS * 3, 0, 0, 0, 0 };

        // Meshlet 总数
        private static readonly int MeshletCountID = Shader.PropertyToID("_MeshletCount");
        private int m_MeshletCount;

        private Bounds m_ProxyBounds;
        private int m_KernelGroupX;

        private void Start()
        {
            if (!SelectedMeshletAsset) return;
            InitAssets();
            InitParas();
            InitBuffers();
            SetupShaders();
        }

        private void InitAssets()
        {
            m_Collection = SelectedMeshletAsset.Collection;
            m_SourceMesh = SelectedMeshletAsset.SourceMesh;
        }

        private void InitParas()
        {
            m_MeshletCount = SelectedMeshletAsset.Collection.meshlets.Length;
            m_ProxyBounds = new Bounds(Vector3.zero, 1000.0f * Vector3.one);
            m_KernelGroupX = Mathf.CeilToInt(1.0f * m_MeshletCount / KERNEL_SIZE_X); // 计算剔除所需的线程组数
        }


        private void InitBuffers()
        {
            // 间接参数缓冲区
            m_DrawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 5 * sizeof(uint));
            m_DrawArgsBuffer.name = nameof(m_DrawArgsBuffer);
            m_DrawArgsBuffer.SetData(m_DrawArgs);

            m_DispatchArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 3 * sizeof(uint));
            m_DispatchArgsBuffer.name = nameof(m_DispatchArgsBuffer);
            m_DispatchArgsBuffer.SetData(m_DispatchArgs);

            // 输入顶点坐标缓冲区
            m_VerticesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, m_SourceMesh.vertices.Length,
                sizeof(float) * 3);
            m_VerticesBuffer.name = nameof(m_VerticesBuffer);
            m_VerticesBuffer.SetData(m_SourceMesh.vertices);

            // 输出索引缓冲区
            m_IndicesBuffer =
                new GraphicsBuffer(GraphicsBuffer.Target.Raw, MAX_PRIMS * m_MeshletCount * 3, sizeof(uint));
            m_IndicesBuffer.name = nameof(m_IndicesBuffer);
            m_IndicesBuffer.SetData(new int[MAX_PRIMS * m_MeshletCount]);

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
        }

        private void SetupShaders()
        {
            m_CullingKernelID = CullingCompute.FindKernel("CullingMain");
            CullingCompute.SetInt(MeshletCountID, m_MeshletCount);
            CullingCompute.SetBuffer(m_CullingKernelID, DispatchArgsBufferID, m_DispatchArgsBuffer);
            CullingCompute.SetBuffer(m_CullingKernelID, VisibleMeshletIndicesBufferID, m_VisibleMeshletIndicesBuffer);

            m_ProcessingKernelID = ProcessingCompute.FindKernel("ProcessingMain");
            ProcessingCompute.SetBuffer(m_ProcessingKernelID, DrawArgsBufferID, m_DrawArgsBuffer);
            ProcessingCompute.SetBuffer(m_ProcessingKernelID, VisibleMeshletIndicesBufferID,
                m_VisibleMeshletIndicesBuffer);
            ProcessingCompute.SetBuffer(m_ProcessingKernelID, IndicesBufferID, m_IndicesBuffer);
            ProcessingCompute.SetBuffer(m_ProcessingKernelID, MeshletsBufferID, m_MeshletsBuffer);
            ProcessingCompute.SetBuffer(m_ProcessingKernelID, MeshletVertexIndicesBufferID,
                m_MeshletVertexIndicesBuffer);
            ProcessingCompute.SetBuffer(m_ProcessingKernelID, MeshletPrimitiveIndicesBufferID,
                m_MeshletPrimitiveIndicesBuffer);

            m_MeshletMaterial = new Material(Shader.Find("Nanite/MeshletRendering"));
            m_MeshletMaterial.SetBuffer(VerticesBufferID, m_VerticesBuffer);
            m_MeshletMaterial.SetBuffer(IndicesBufferID, m_IndicesBuffer);
            m_MeshletMaterial.SetBuffer(VisibleMeshletIndicesBufferID, m_VisibleMeshletIndicesBuffer);
        }

        private void Update()
        {
            if (!SelectedMeshletAsset) return;

            m_VisibleMeshletIndicesBuffer.SetCounterValue(0);

            CullingCompute.Dispatch(m_CullingKernelID, m_KernelGroupX, 1, 1);

            GraphicsBuffer.CopyCount(m_VisibleMeshletIndicesBuffer, m_DispatchArgsBuffer, sizeof(uint) * 0);
            GraphicsBuffer.CopyCount(m_VisibleMeshletIndicesBuffer, m_DrawArgsBuffer, sizeof(uint) * 1);

            ProcessingCompute.DispatchIndirect(m_ProcessingKernelID, m_DispatchArgsBuffer);
            Graphics.DrawProceduralIndirect(m_MeshletMaterial, m_ProxyBounds, MeshTopology.Triangles, m_DrawArgsBuffer);
        }

        private void OnDestroy()
        {
            m_VerticesBuffer?.Release();

            m_MeshletsBuffer?.Release();
            m_MeshletVertexIndicesBuffer?.Release();
            m_MeshletPrimitiveIndicesBuffer?.Release();

            m_VisibleMeshletIndicesBuffer?.Release();

            m_IndicesBuffer?.Release();

            m_DrawArgsBuffer?.Release();
            m_DispatchArgsBuffer?.Release();
        }
    }
}