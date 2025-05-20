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
        public Vector4 Sphere;
        public uint NormalCone;
        public float ApexOffset;
    }

    [Serializable]
    [StructLayout(LayoutKind.Sequential)]
    public class MeshletCollection
    {
        [HideInInspector] public uint[] triangles;
        [HideInInspector] public uint[] vertices;
        public Meshlet[] meshlets;
        public BoundsData[] boundsDataArray;
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

    public struct InstancePara
    {
        public Matrix4x4 ModelMatrix;
        public Color InstanceColor;
        public const int SIZE = sizeof(float) * 16 + sizeof(float) * 4;
    }

    public struct BoundsData
    {
        public Vector4 BoundingSphere;
        public uint NormalCone;
        public float ApexOffset;
    }
}