using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Nanity
{
    public class NanityPlugin
    {
        // DLL Import statements
        [DllImport("NanityPlugin")]
        private static extern IntPtr BuildMeshlets(
            [In] uint[] indices, uint indicesCount,
            [In] float[] positions, uint positionsCount);

        [DllImport("NanityPlugin")]
        private static extern void DestroyMeshletsContext(IntPtr context);

        [DllImport("NanityPlugin")]
        private static extern uint GetMeshletsCount(IntPtr context);

        [DllImport("NanityPlugin")]
        private static extern bool GetMeshlets(IntPtr context, [Out] Meshlet[] meshlets, uint bufferSize);

        [DllImport("NanityPlugin")]
        private static extern uint GetVerticesCount(IntPtr context);

        [DllImport("NanityPlugin")]
        private static extern bool GetVertices(IntPtr context, [Out] uint[] indices, uint bufferSize);

        [DllImport("NanityPlugin")]
        private static extern uint GetTriangleCount(IntPtr context);

        [DllImport("NanityPlugin")]
        private static extern bool GetTriangles(IntPtr context, [Out] uint[] primitives, uint bufferSize);

        [DllImport("NanityPlugin")]
        private static extern uint GetBoundsCount(IntPtr context);

        [DllImport("NanityPlugin")]
        private static extern bool GetBounds(IntPtr context, [Out] BoundsData[] boundsDataArray, uint bufferSize);

        [DllImport("NanityPlugin")]
        private static extern uint GetOptimizedVertexCount(IntPtr context);

        [DllImport("NanityPlugin")]
        private static extern bool
            GetOptimizedVertexPositions(IntPtr context, [Out] float[] positions, uint bufferSize);

        public static MeshletCollection ProcessMesh(uint[] indices, Vector3[] vertices)
        {
            // Convert Vector3 array to float array
            var positions = new float[vertices.Length * 3];
            for (var i = 0; i < vertices.Length; i++)
            {
                positions[i * 3] = vertices[i].x;
                positions[i * 3 + 1] = vertices[i].y;
                positions[i * 3 + 2] = vertices[i].z;
            }

            // 直接调用BuildMeshlets
            var context = BuildMeshlets(indices, (uint)indices.Length, positions, (uint)positions.Length);
            if (context == IntPtr.Zero)
                throw new Exception("Failed to build meshlets");
            
            try
            {
                var collection = new MeshletCollection();
                
                // Get meshlets
                var meshletCount = GetMeshletsCount(context);
                collection.meshlets = new Meshlet[meshletCount];
                if (!GetMeshlets(context, collection.meshlets, meshletCount))
                    throw new Exception("Failed to get meshlets data");

                // Get vertices
                var verticesCount = GetVerticesCount(context);
                collection.vertices = new uint[verticesCount];
                if (!GetVertices(context, collection.vertices, verticesCount))
                    throw new Exception("Failed to get vertices data");

                // Get triangles
                var triangleCount = GetTriangleCount(context);
                collection.triangles = new uint[triangleCount];
                if (!GetTriangles(context, collection.triangles, triangleCount))
                    throw new Exception("Failed to get triangles data");

                // Get bounds data array
                var boundsCount = GetBoundsCount(context);
                collection.boundsDataArray = new BoundsData[boundsCount];
                if (!GetBounds(context, collection.boundsDataArray, boundsCount))
                    throw new Exception("Failed to get bounds data");
                
                // 获取优化后的顶点数据
                uint optimizedVertexCount = GetOptimizedVertexCount(context);
                float[] rawPositions = new float[optimizedVertexCount * 3];
                if (!GetOptimizedVertexPositions(context, rawPositions, optimizedVertexCount * 3))
                    throw new Exception("Failed to get optimized vertex positions");
                
                collection.optimizedVertices = new Vector3[optimizedVertexCount];
                for (int i = 0; i < optimizedVertexCount; i++)
                {
                    collection.optimizedVertices[i] = new Vector3(
                        rawPositions[i * 3],
                        rawPositions[i * 3 + 1],
                        rawPositions[i * 3 + 2]
                    );
                }
                
                return collection;
            }
            finally
            {
                DestroyMeshletsContext(context);
            }
        }
    }
}