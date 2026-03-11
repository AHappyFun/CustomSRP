using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public struct Triangle
{
    public Vector3 v0;
    public Vector3 v1;
    public Vector3 v2;

    public Vector3 Center()
    {
        return (v0 + v1 + v2) / 3f;
    }
}

public class BVHNode
{

    public Bounds bounds;

    public BVHNode leftNode;
    public BVHNode rightNode;

    //三角形范围
    public int start;
    public int count;

    public bool IsLeaf()
    {
        return leftNode == null && rightNode == null;
    }

}


public class BVHBuilder : MonoBehaviour
{

    public Mesh mesh;

    private BVHNode root;

    [Range(0,100)]
    public int ShowDepth = 0;

    private void Start()
    {
        if(mesh == null)
            return;

        List<Triangle> triangles = BuildTriangles(mesh).ToList();
        root = Build(triangles, 0, triangles.Count);

    }

    void OnDrawGizmos()
    {
        if(root == null)
            return;

        DrawNode(root, 0);
    }

    void DrawNode(BVHNode node, int depth)
    {
        Gizmos.color = Color.Lerp(Color.red, Color.blue, depth * 0.1f);

        if(node.IsLeaf())
            Gizmos.color = Color.green;

        if (depth == ShowDepth)
        {
            Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);
            return;
        }
        else
        {
            //Gizmos.DrawWireCube(node.bounds.center, node.bounds.size);
        }

        if (node.leftNode != null)
            DrawNode(node.leftNode, depth + 1);

        if (node.rightNode != null)
            DrawNode(node.rightNode, depth + 1);
    }

    //Mesh读取三角形
    Triangle[] BuildTriangles(Mesh mesh)
    {
        var vertices = mesh.vertices;
        var indices = mesh.triangles;

        Triangle[] tris = new Triangle[indices.Length / 3];

        for (int i = 0; i < tris.Length; i++)
        {
            tris[i] = new Triangle
            {
                v0 = vertices[indices[i * 3 + 0]],
                v1 = vertices[indices[i * 3 + 1]],
                v2 = vertices[indices[i * 3 + 2]],
            };
        }

        return tris;
    }

    //三角形列表构建AABB
    Bounds ComputeBounds(List<Triangle> tris, int start, int count)
    {
        Bounds b = new Bounds(tris[start].v0, Vector3.zero);

        for (int i = start; i < start + count; i++)
        {
            b.Encapsulate(tris[i].v0);
            b.Encapsulate(tris[i].v1);
            b.Encapsulate(tris[i].v2);
        }

        return b;
    }

    //BVH构建
    BVHNode Build(List<Triangle> tris, int start, int count)
    {
        BVHNode node = new BVHNode();
        node.start = start;
        node.count = count;

        node.bounds = ComputeBounds(tris, start, count);

        if (count <= 4)
            return node;

        int axis = LongestAxis(node.bounds);

        tris.Sort(start, count, new TriangleCenterComparer(axis));

        int mid = count / 2;

        node.leftNode = Build(tris, start, mid);
        node.rightNode = Build(tris, start + mid, count - mid);

        return node;
    }
    int LongestAxis(Bounds b)
    {
        Vector3 size = b.size;

        if (size.x > size.y && size.x > size.z) return 0;
        if (size.y > size.z) return 1;
        return 2;
    }


    class TriangleCenterComparer : IComparer<Triangle>
    {
        int axis;

        public TriangleCenterComparer(int axis)
        {
            this.axis = axis;
        }

        public int Compare(Triangle a, Triangle b)
        {
            return a.Center()[axis].CompareTo(b.Center()[axis]);
        }
    }

}
