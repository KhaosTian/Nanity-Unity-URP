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
        private ComputeBuffer m_VerticesBuffer;

        // Meshlet 数据
        private static readonly int MeshletsBufferID = Shader.PropertyToID("_MeshletsBuffer");
        private static readonly int MeshletVertexIndicesBufferID = Shader.PropertyToID("_MeshletVertexIndicesBuffer");

        private static readonly int MeshletPrimitiveIndicesBufferID =
            Shader.PropertyToID("_MeshletPrimitiveIndicesBuffer");

        private ComputeBuffer m_MeshletsBuffer;
        private ComputeBuffer m_MeshletVertexIndicesBuffer;
        private ComputeBuffer m_MeshletPrimitiveIndicesBuffer;

        // 可见实例数据（Meshlet 索引）
        private static readonly int VisibleMeshletIndicesBufferID = Shader.PropertyToID("_VisibleMeshletIndicesBuffer");
        private ComputeBuffer m_VisibleMeshletIndicesBuffer;

        // 可见实例的索引缓冲区
        private static readonly int IndicesBufferID = Shader.PropertyToID("_IndicesBuffer");
        private ComputeBuffer m_IndicesBuffer;

        // 间接参数缓冲区
        private static readonly int DispatchArgsBufferID = Shader.PropertyToID("_DispatchArgsBuffer");
        private static readonly int DrawArgsBufferID = Shader.PropertyToID("_DrawArgsBuffer");
        private ComputeBuffer m_DrawArgsBuffer;
        private ComputeBuffer m_DispatchArgsBuffer;

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
            m_DrawArgsBuffer = new ComputeBuffer(1, 5 * sizeof(uint), ComputeBufferType.IndirectArguments);
            m_DrawArgsBuffer.name = nameof(m_DrawArgsBuffer);

            m_DispatchArgsBuffer = new ComputeBuffer(1, 3 * sizeof(uint), ComputeBufferType.IndirectArguments);
            m_DispatchArgsBuffer.name = nameof(m_DispatchArgsBuffer);

            // 输入顶点坐标缓冲区
            m_VerticesBuffer = new ComputeBuffer(m_SourceMesh.vertices.Length,
                sizeof(float) * 3, ComputeBufferType.Structured);
            m_VerticesBuffer.name = nameof(m_VerticesBuffer);
            m_VerticesBuffer.SetData(m_SourceMesh.vertices);

            // 输出索引缓冲区
            m_IndicesBuffer =
                new ComputeBuffer(MAX_PRIMS * m_MeshletCount * 3, sizeof(uint), ComputeBufferType.Structured);
            m_IndicesBuffer.name = nameof(m_IndicesBuffer);
            m_IndicesBuffer.SetData(new int[MAX_PRIMS * m_MeshletCount]);

            // Meshlet缓冲区
            m_MeshletsBuffer = new ComputeBuffer(m_MeshletCount, sizeof(uint) * 4, ComputeBufferType.Structured);
            m_MeshletsBuffer.name = nameof(m_MeshletsBuffer);
            m_MeshletsBuffer.SetData(m_Collection.meshlets);

            // Meshlet Vertices索引缓冲区
            m_MeshletVertexIndicesBuffer =
                new ComputeBuffer(m_Collection.vertices.Length, sizeof(uint), ComputeBufferType.Structured);
            m_MeshletVertexIndicesBuffer.name = nameof(m_MeshletVertexIndicesBuffer);
            m_MeshletVertexIndicesBuffer.SetData(m_Collection.vertices);

            // Meshlet Triangles索引缓冲区
            m_MeshletPrimitiveIndicesBuffer = new ComputeBuffer(m_Collection.triangles.Length, sizeof(uint),
                ComputeBufferType.Structured);
            m_MeshletPrimitiveIndicesBuffer.name = nameof(m_MeshletPrimitiveIndicesBuffer);
            m_MeshletPrimitiveIndicesBuffer.SetData(m_Collection.triangles);

            // 可见 Meshlet 索引缓冲区
            m_VisibleMeshletIndicesBuffer =
                new ComputeBuffer(m_MeshletCount, sizeof(uint), ComputeBufferType.Structured);
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
        }

        private void Update()
        {
            if (!SelectedMeshletAsset) return;

            // dispatchArgs 存储了所需的线程 x,y,z 维度
            var dispatchArgs = new uint[3] { 0, 1, 1 };
            m_DispatchArgsBuffer.SetData(dispatchArgs);

            // visible count 存储在 dispatchArgs 的 x 维度
            CullingCompute.Dispatch(m_CullingKernelID, m_KernelGroupX, 1, 1);
 
            // 获取 dispatchArgs 在GPU上的结果
            m_DispatchArgsBuffer.GetData(dispatchArgs);

            
            // drawArgs 存储了实例索引数、实例个数（visible count ）等参数
            var drawArgs = new uint[5] { MAX_PRIMS * 3, dispatchArgs[0], 0, 0, 0 };
            m_DrawArgsBuffer.SetData(drawArgs);
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