using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

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
        public Camera RenderingCamera;
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

        // 包围体缓冲区
        private static readonly int MeshletBoundsDataBufferID = Shader.PropertyToID("_MeshletBoundsDataBuffer");
        private GraphicsBuffer m_MeshletBoundsDataBuffer;

        // Meshlet 总数
        
        private static readonly int ConstantBufferID = Shader.PropertyToID("_ConstantBuffer");
        private GraphicsBuffer m_ConstantBuffer;
        
        private static readonly int MeshletCountID = Shader.PropertyToID("_MeshletCount");
        private static readonly int InstanceCountID = Shader.PropertyToID("_InstanceCount");
        private static readonly int MeshletCountPerInstanceID = Shader.PropertyToID("_MeshletCountPerInstance");
        private static readonly int ViewPosID = Shader.PropertyToID("_ViewPos");
        private static readonly int CullingPlaneVectorArrayID = Shader.PropertyToID("_CullingPlaneVectorArray");
        
        private readonly Vector4[] m_CullingPlaneVectorArray = new Vector4[6];
        private readonly Plane[] m_CullingPlanes = new Plane[6];
        private Vector3 m_ViewPos;
        private int m_MeshletCount;
        private int m_MeshletCountPerInstance;
        private int m_InstanceCount;

        private Bounds m_ProxyBounds;
        private int m_KernelGroupX;

        public int Row = 3;
        public int Column = 3;

        private void Start()
        {
            if (!IsValid()) return;

            m_Collection = SelectedMeshletAsset.Collection;
            m_SourceMesh = SelectedMeshletAsset.SourceMesh;
            m_ProxyBounds = new Bounds(Vector3.zero, 1000.0f * Vector3.one);
            
            m_InstanceCount = Row * Column;
            m_MeshletCountPerInstance = SelectedMeshletAsset.Collection.meshlets.Length;
            m_MeshletCount = m_MeshletCountPerInstance *  m_InstanceCount;
            m_KernelGroupX = Mathf.CeilToInt(1.0f / KERNEL_SIZE_X * m_MeshletCount);
            
            InitBuffers();
            InitShaders();
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

            if (!RenderingCamera)
            {
                Debug.LogWarning("Camera is missing.");
            }

            return true;
        }

        private void InitBuffers()
        {
            // 间接参数缓冲区
            m_DrawArgsBuffer = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, 5 * sizeof(uint));
            m_DrawArgsBuffer.name = nameof(m_DrawArgsBuffer);
            m_DrawArgsBuffer.SetData(m_DrawArgs);

            // 输入顶点坐标缓冲区
            m_VerticesBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, m_Collection.optimizedVertices.Length,
                sizeof(float) * 3);
            m_VerticesBuffer.name = nameof(m_VerticesBuffer);
            m_VerticesBuffer.SetData(m_Collection.optimizedVertices);

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
            
            // Meshlet BoundsData缓冲区
            m_MeshletBoundsDataBuffer = new GraphicsBuffer(GraphicsBuffer.Target.Structured, m_MeshletCountPerInstance,
                BoundsData.SIZE);
            m_MeshletBoundsDataBuffer.name = nameof(m_MeshletBoundsDataBuffer);
            m_MeshletBoundsDataBuffer.SetData(m_Collection.boundsDataArray);

            // 可见 Meshlet 索引缓冲区
            m_VisibleMeshletIndicesBuffer =
                new GraphicsBuffer(GraphicsBuffer.Target.Append, m_MeshletCount, sizeof(uint));
            m_VisibleMeshletIndicesBuffer.name = nameof(m_VisibleMeshletIndicesBuffer);
            m_VisibleMeshletIndicesBuffer.SetData(new int[m_MeshletCount]);
            
            var instanceParas = new InstancePara[m_InstanceCount];
            var parentPosition = transform.position;
            for (int r = 0; r < Row; r++)
            {
                for (int c = 0; c < Column; c++)
                {
                    int index = r * Column + c;

                    var modelToWorld = Matrix4x4.TRS(
                        parentPosition + new Vector3(c, r, 0),
                        Quaternion.identity,
                        Vector3.one
                    );

                    instanceParas[index].ModelToWorld = modelToWorld;
                    instanceParas[index].InstanceColor = Random.ColorHSV();
                }
            }

            m_InstanceParasBuffer =
                new GraphicsBuffer(GraphicsBuffer.Target.Structured, m_InstanceCount, InstancePara.SIZE);
            m_InstanceParasBuffer.name = nameof(m_InstanceParasBuffer);
            m_InstanceParasBuffer.SetData(instanceParas);
        }

        private void InitShaders()
        {
            m_CullingKernelID = CullingCompute.FindKernel("CullingMain");
            m_MeshletMaterial = new Material(Shader.Find("Nanity/MeshletRendering"));
            
            CullingCompute.SetInt(MeshletCountID, m_MeshletCount);
            CullingCompute.SetInt(MeshletCountPerInstanceID, m_MeshletCountPerInstance);
            CullingCompute.SetBuffer(m_CullingKernelID, VisibleMeshletIndicesBufferID, m_VisibleMeshletIndicesBuffer);
            CullingCompute.SetBuffer(m_CullingKernelID, MeshletBoundsDataBufferID, m_MeshletBoundsDataBuffer);
            CullingCompute.SetBuffer(m_CullingKernelID, InstanceParasBufferID, m_InstanceParasBuffer);
            
            m_MeshletMaterial.SetInt(MeshletCountPerInstanceID, m_MeshletCountPerInstance);
            m_MeshletMaterial.SetBuffer(VerticesBufferID, m_VerticesBuffer);
            m_MeshletMaterial.SetBuffer(VisibleMeshletIndicesBufferID, m_VisibleMeshletIndicesBuffer);
            m_MeshletMaterial.SetBuffer(MeshletsBufferID, m_MeshletsBuffer);
            m_MeshletMaterial.SetBuffer(MeshletVertexIndicesBufferID, m_MeshletVertexIndicesBuffer);
            m_MeshletMaterial.SetBuffer(MeshletPrimitiveIndicesBufferID, m_MeshletPrimitiveIndicesBuffer);
            m_MeshletMaterial.SetBuffer(InstanceParasBufferID, m_InstanceParasBuffer);
        }

        private void UpdateFrame()
        {
            // Global constants
            m_ViewPos = RenderingCamera.transform.position;
            GeometryUtility.CalculateFrustumPlanes(RenderingCamera, m_CullingPlanes);
            for (int i = 0; i < 6; i++)
            {
                var normal = m_CullingPlanes[i].normal;
                m_CullingPlaneVectorArray[i] = new Vector4(normal.x, normal.y, normal.z, m_CullingPlanes[i].distance);
            }
            CullingCompute.SetVector(ViewPosID, m_ViewPos);
            CullingCompute.SetVectorArray(CullingPlaneVectorArrayID, m_CullingPlaneVectorArray);
        }
        private void Update()
        {
            if (!IsValid()) return;
            UpdateFrame();
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
            m_MeshletBoundsDataBuffer?.Release();
            m_VisibleMeshletIndicesBuffer?.Release();
            m_DrawArgsBuffer?.Release();
            m_InstanceParasBuffer?.Release();
        }
    }
}