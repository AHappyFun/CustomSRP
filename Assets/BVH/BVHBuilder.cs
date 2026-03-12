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

    private List<Triangle> triangles = new List<Triangle>();

    private int triangleTests = 0;

    private void Start()
    {
        if(mesh == null)
            return;

        triangles = BuildTriangles(mesh).ToList();
        root = Build(triangles, 0, triangles.Count);

    }

    private void Update()
    {
        if (Input.GetMouseButtonDown(0))
        {


            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            ray.direction = ray.direction.normalized;

            //BVH相交测试
            triangleTests = 0;
            float t = float.MaxValue;
            bool isHit = Traverse(root, ray, ref t);

            Debug.Log("三角形测试数量：" + triangleTests);
            Debug.Log("t = " + t);
            Debug.Log("是否击中 " + isHit);

            //暴力三角形测试
            //bool hit = false;
            //foreach(var tri in triangles)
            //{
            //    if(RayTriangle(ray, tri, out float t))
            //    {
            //        hit = true;
            //        break;
            //    }
            //}
            //Debug.Log("是否击中 " + hit);

        }
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
        else
        {
            Gizmos.color = Color.gray;
        }

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
                v0 = transform.TransformPoint(vertices[indices[i * 3 + 0]]) ,
                v1 = transform.TransformPoint(vertices[indices[i * 3 + 1]]) ,
                v2 = transform.TransformPoint(vertices[indices[i * 3 + 2]]) ,
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

    //光线相交
    //与AABB
    bool RayAABB(Ray ray, Bounds box)
    {
        float tmin = (box.min.x - ray.origin.x) / ray.direction.x;
        float tmax = (box.max.x - ray.origin.x) / ray.direction.x;

        if (tmin > tmax)
            (tmin, tmax) = (tmax, tmin);

        float tymin = (box.min.y - ray.origin.y) / ray.direction.y;
        float tymax = (box.max.y - ray.origin.y) / ray.direction.y;

        if (tymin > tymax)
            (tymin, tymax) = (tymax, tymin);

        if ((tmin > tymax) || (tymin > tmax))
            return false;

        return true;
    }

    //与三角形
    bool RayTriangle(Ray ray, Triangle tri, out float t)
    {
        t = 0;

        Vector3 edge1 = tri.v1 - tri.v0;
        Vector3 edge2 = tri.v2 - tri.v0;

        Vector3 pvec = Vector3.Cross(ray.direction, edge2);

        float det = Vector3.Dot(edge1, pvec);

        if (Mathf.Abs(det) < 1e-6f)
            return false;

        float invDet = 1f / det;

        Vector3 tvec = ray.origin - tri.v0;

        float u = Vector3.Dot(tvec, pvec) * invDet;

        if (u < 0 || u > 1)
            return false;

        Vector3 qvec = Vector3.Cross(tvec, edge1);

        float v = Vector3.Dot(ray.direction, qvec) * invDet;

        if (v < 0 || u + v > 1)
            return false;

        t = Vector3.Dot(edge2, qvec) * invDet;

        return t > 0;
    }


    //遍历BVH
    bool Traverse(BVHNode node, Ray ray, ref float closestT)
    {
        if (!RayAABB(ray, node.bounds))
            return false;

        bool hit = false;

        if (node.IsLeaf())
        {
            for (int i = node.start; i < node.start + node.count; i++)
            {
                triangleTests++;

                if (RayTriangle(ray, triangles[i], out float t))
                {
                    if (t < closestT)
                    {
                        closestT = t;
                        hit = true;
                    }
                }
            }
        }
        else
        {
            hit |= Traverse(node.leftNode, ray, ref closestT);
            hit |= Traverse(node.rightNode, ray, ref closestT);
        }

        return hit;
    }


}
