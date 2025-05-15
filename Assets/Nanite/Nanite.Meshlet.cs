using System;
using System.Numerics;
using System.Runtime.InteropServices;

namespace Nanite
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
        public uint[] triangles;
        public uint[] vertices;
        public Meshlet[] meshlets;
    }

    public struct Vertex
    {
        public Vector3 Position;
        
        public static int SIZE = sizeof(float) * 3;
    }
}