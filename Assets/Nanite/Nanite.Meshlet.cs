using System;
using System.Runtime.InteropServices;
using UnityEngine;
using Vector3 = System.Numerics.Vector3;

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
        [HideInInspector] public uint[] triangles;
        [HideInInspector] public uint[] vertices;
        [HideInInspector] public Meshlet[] meshlets;
    }

    public struct Vertex
    {
        public Vector3 Position;

        public static int SIZE = sizeof(float) * 3;
    }
}