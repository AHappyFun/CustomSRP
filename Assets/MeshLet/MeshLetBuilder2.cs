using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 优化Cluster分割
/// </summary>
public class MeshLetBuilder2 : MonoBehaviour
{
    class Cluster
    {
        public List<int> triangles = new List<int>();
    }

    struct Edge
    {
        public int v0;
        public int v1;

        public Edge(int a, int b)
        {
            if (a < b)
            {
                v0 = a;
                v1 = b;
            }
            else
            {
                v0 = b;
                v1 = a;
            }
        }

        public override int GetHashCode()
        {
            return v0 * 73856093 ^ v1 * 19349663;
        }

        public override bool Equals(object obj)
        {
            if (!(obj is Edge)) return false;
            Edge e = (Edge)obj;
            return v0 == e.v0 && v1 == e.v1;
        }
    }

    void AddEdge(Dictionary<Edge, List<int>> map, Edge edge, int tri)
    {
        if (!map.TryGetValue(edge, out var list))
        {
            list = new List<int>();
            map[edge] = list;
        }

        list.Add(tri);
    }

    //建立邻边列表
    List<int>[] BuildAdjacency(Mesh mesh)
    {
        int[] tris = mesh.triangles;
        int triCount = tris.Length / 3;

        // adjacency result
        List<int>[] adjacency = new List<int>[triCount];

        for (int i = 0; i < triCount; i++)
            adjacency[i] = new List<int>();

        // edge -> triangles
        Dictionary<Edge, List<int>> edgeMap = new Dictionary<Edge, List<int>>();

        for (int tri = 0; tri < triCount; tri++)
        {
            int i0 = tris[tri * 3 + 0];
            int i1 = tris[tri * 3 + 1];
            int i2 = tris[tri * 3 + 2];

            Edge e0 = new Edge(i0, i1);
            Edge e1 = new Edge(i1, i2);
            Edge e2 = new Edge(i2, i0);

            AddEdge(edgeMap, e0, tri);
            AddEdge(edgeMap, e1, tri);
            AddEdge(edgeMap, e2, tri);
        }

        // build adjacency
        foreach (var kv in edgeMap)
        {
            List<int> trisSharingEdge = kv.Value;

            if (trisSharingEdge.Count < 2)
                continue;

            for (int i = 0; i < trisSharingEdge.Count; i++)
            {
                for (int j = i + 1; j < trisSharingEdge.Count; j++)
                {
                    int a = trisSharingEdge[i];
                    int b = trisSharingEdge[j];

                    adjacency[a].Add(b);
                    adjacency[b].Add(a);
                }
            }
        }

        return adjacency;
    }


    //优化版本建立分块
    List<Cluster> BuildClusters(Mesh mesh)
    {
        int triCount = mesh.triangles.Length / 3;

        bool[] used = new bool[triCount];

        var adjacency = BuildAdjacency(mesh);

        List<Cluster> clusters = new List<Cluster>();

        for(int seed=0; seed<triCount; seed++)
        {
            if(used[seed])
                continue;

            Cluster cluster = new Cluster();

            Queue<int> queue = new Queue<int>();
            queue.Enqueue(seed);

            while(queue.Count > 0)
            {
                int tri = queue.Dequeue();

                if(used[tri])
                    continue;

                if(cluster.triangles.Count >= 64)
                    break;

                cluster.triangles.Add(tri);
                used[tri] = true;

                foreach(var n in adjacency[tri])
                    queue.Enqueue(n);
            }

            clusters.Add(cluster);
        }

        return clusters;
    }


    Mesh BuildDebugMesh(Mesh srcMesh, List<Cluster> clusters)
    {
        Vector3[] srcVertices = srcMesh.vertices;
        int[] srcTris = srcMesh.triangles;

        List<Vector3> vertices = new List<Vector3>();
        List<int> triangles = new List<int>();
        List<Color> colors = new List<Color>();

        int vertOffset = 0;

        for (int c = 0; c < clusters.Count; c++)
        {
            Color col = Random.ColorHSV();

            foreach (int triIndex in clusters[c].triangles)
            {
                int i0 = srcTris[triIndex * 3 + 0];
                int i1 = srcTris[triIndex * 3 + 1];
                int i2 = srcTris[triIndex * 3 + 2];

                vertices.Add(srcVertices[i0]);
                vertices.Add(srcVertices[i1]);
                vertices.Add(srcVertices[i2]);

                colors.Add(col);
                colors.Add(col);
                colors.Add(col);

                triangles.Add(vertOffset + 0);
                triangles.Add(vertOffset + 1);
                triangles.Add(vertOffset + 2);

                vertOffset += 3;
            }
        }

        Mesh mesh = new Mesh();

        mesh.SetVertices(vertices);
        mesh.SetTriangles(triangles, 0);
        mesh.SetColors(colors);

        mesh.RecalculateNormals();

        return mesh;
    }


    private void Start()
    {
        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;

        if (mesh)
        {
            List<Cluster> clusters = BuildClusters(mesh);

            Mesh debugMesh = BuildDebugMesh(mesh, clusters);

            GetComponent<MeshFilter>().sharedMesh = debugMesh;
        }
    }
}
