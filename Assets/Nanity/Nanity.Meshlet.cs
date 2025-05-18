using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Vector3 = System.Numerics.Vector3;

namespace Nanity
{
    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public struct Meshlet
    {
        public uint VertOffset;
        public uint PrimOffset;
        public uint VertCount;
        public uint PrimCount;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public class MeshletCollection
    {
        [HideInInspector] public uint[] triangles;
        [HideInInspector] public uint[] vertices;
        public Meshlet[] meshlets;
    }

    public struct Vertex
    {
        public Vector3 Position;
        public Vector3 Normal;
        public static int SIZE = sizeof(float) * 3;
    }

    public struct MeshInfo
    {
        public uint IndexSize;
        public uint MeshletCount;
    }

    public struct EntityPara
    {
        public Matrix4x4 ModelMatrix; 
    }

    public struct CullData
    {
        public Vector4 BoundingSphere;
        public uint NormalCone;
        public float ApexOffset;
    }
}