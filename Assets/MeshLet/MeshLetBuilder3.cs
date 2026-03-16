using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 增加ClusterTree，类似Nanite
/// </summary>
public class MeshLetBuilder3 : MonoBehaviour
{
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

    // 一个 Cluster Node
    public class ClusterNode
    {
        public List<int> triangles = new List<int>(); // triangle indices
        public Vector3 center;   // bounding center
        public float radius;     // bounding radius
        public ClusterNode parent;
        public List<ClusterNode> children = new List<ClusterNode>();
        public int level = 0;    // 0 leaf
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

    const int MAX_TRIANGLES_PER_CLUSTER = 64;

    //优化版本建立分块
    public List<ClusterNode> BuildLeafClusters(Mesh mesh)
    {
        int triCount = mesh.triangles.Length / 3;
        bool[] used = new bool[triCount];
        List<int>[] adjacency = BuildAdjacency(mesh);
        List<ClusterNode> leafClusters = new List<ClusterNode>();

        for (int seed = 0; seed < triCount; seed++)
        {
            if (used[seed]) continue;

            ClusterNode cluster = new ClusterNode();
            Queue<int> queue = new Queue<int>();
            queue.Enqueue(seed);

            while (queue.Count > 0)
            {
                int tri = queue.Dequeue();
                if (used[tri]) continue;
                if (cluster.triangles.Count >= MAX_TRIANGLES_PER_CLUSTER) break;

                cluster.triangles.Add(tri);
                used[tri] = true;

                foreach (int n in adjacency[tri])
                    queue.Enqueue(n);
            }

            ComputeClusterBounds(mesh, cluster);
            leafClusters.Add(cluster);
        }

        return leafClusters;
    }


    // ===================== Compute bounds =====================
    public void ComputeClusterBounds(Mesh mesh, ClusterNode cluster)
    {
        Vector3[] verts = mesh.vertices;
        Vector3 center = Vector3.zero;
        int count = 0;
        foreach (int tri in cluster.triangles)
        {
            int i0 = mesh.triangles[tri * 3 + 0];
            int i1 = mesh.triangles[tri * 3 + 1];
            int i2 = mesh.triangles[tri * 3 + 2];

            center += verts[i0] + verts[i1] + verts[i2];
            count += 3;
        }
        center /= count;
        cluster.center = center;

        float radius = 0f;
        foreach (int tri in cluster.triangles)
        {
            int i0 = mesh.triangles[tri * 3 + 0];
            int i1 = mesh.triangles[tri * 3 + 1];
            int i2 = mesh.triangles[tri * 3 + 2];

            radius = Mathf.Max(radius, (verts[i0] - center).magnitude);
            radius = Mathf.Max(radius, (verts[i1] - center).magnitude);
            radius = Mathf.Max(radius, (verts[i2] - center).magnitude);
        }
        cluster.radius = radius;
    }

    // ===================== Build cluster hierarchy =====================
    public ClusterNode BuildClusterHierarchy(List<ClusterNode> leafClusters)
    {
        List<ClusterNode> currentLevel = new List<ClusterNode>(leafClusters);
        int level = 0;

        while (currentLevel.Count > 1)
        {
            List<ClusterNode> nextLevel = new List<ClusterNode>();
            bool[] used = new bool[currentLevel.Count];

            for (int i = 0; i < currentLevel.Count; i++)
            {
                if (used[i]) continue;

                ClusterNode a = currentLevel[i];
                ClusterNode best = null;
                int bestIndex = -1;
                float bestDist = float.MaxValue;

                for (int j = i + 1; j < currentLevel.Count; j++)
                {
                    if (used[j]) continue;
                    float d = Vector3.Distance(a.center, currentLevel[j].center);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = currentLevel[j];
                        bestIndex = j;
                    }
                }

                ClusterNode parent = new ClusterNode();
                parent.level = level + 1;
                parent.children.Add(a);
                a.parent = parent;

                if (best != null)
                {
                    parent.children.Add(best);
                    best.parent = parent;
                    used[bestIndex] = true;
                }

                // 合并三角形
                parent.triangles.AddRange(a.triangles);
                if (best != null)
                    parent.triangles.AddRange(best.triangles);

                // 计算 bounds
                ComputeClusterBoundsFromChildren(parent);

                nextLevel.Add(parent);
                used[i] = true;
            }

            currentLevel = nextLevel;
            level++;
        }

        return currentLevel[0]; // root
    }

    void ComputeClusterBoundsFromChildren(ClusterNode parent)
    {
        Vector3 center = Vector3.zero;
        int count = 0;
        foreach (var child in parent.children)
        {
            center += child.center * child.triangles.Count;
            count += child.triangles.Count;
        }
        center /= count;
        parent.center = center;

        float radius = 0f;
        foreach (var child in parent.children)
        {
            radius = Mathf.Max(radius, (child.center - center).magnitude + child.radius);
        }
        parent.radius = radius;
    }

    public void SelectVisibleClusters(ClusterNode node, Camera cam, float screenErrorThreshold, List<ClusterNode> visible)
    {
        float distance = Vector3.Distance(cam.transform.position, node.center);
        float screenSize = node.radius / distance; // 简化版屏幕误差

        if (screenSize < screenErrorThreshold || node.children.Count == 0)
        {
            visible.Add(node);
        }
        else
        {
            foreach (var child in node.children)
                SelectVisibleClusters(child, cam, screenErrorThreshold, visible);
        }
    }

    private List<ClusterNode> visibleClusters = new List<ClusterNode>();
    private ClusterNode root;

    public Camera cam;
    public float screenError = 0.05f;

    private Mesh DebugMesh;
    private Color[] vertexColors;
    private int[] tris;
    private void Start()
    {
        Mesh mesh = GetComponent<MeshFilter>().sharedMesh;
        tris = mesh.triangles;
        vertexColors = new Color[mesh.vertexCount];

        if (mesh)
        {
            var leafs = BuildLeafClusters(mesh);

            root = BuildClusterHierarchy(leafs);

            DebugMesh = Instantiate(mesh);
            GetComponent<MeshFilter>().sharedMesh = DebugMesh;

            // 初始化颜色
            for (int i = 0; i < vertexColors.Length; i++)
                vertexColors[i] = Color.gray;

            DebugMesh.colors = vertexColors;
            visibleClusters = new List<ClusterNode>();
        }
    }

    private void Update()
    {
        visibleClusters.Clear();
        SelectVisibleClusters(root, cam, screenError, visibleClusters);

        UpdateVertexColors();
    }
    int GetMaxLevel(ClusterNode node)
    {
        if (node.children.Count == 0) return node.level;
        int max = node.level;
        foreach (var c in node.children)
            max = Mathf.Max(max, GetMaxLevel(c));
        return max;
    }

    void UpdateVertexColors()
    {
        // 默认灰色
        for (int i = 0; i < vertexColors.Length; i++)
            vertexColors[i] = Color.gray;

        // 定义固定颜色表（可以根据层级增加颜色）
        Color[] levelColors = new Color[]
        {
            Color.green,     // Level 0
            Color.white,    // Level 1
            Color.blue,   // Level 2
            Color.yellow,  // Level 3
            Color.cyan,    // Level 4
            Color.red  // Level 5
        };

        foreach (var cluster in visibleClusters)
        {
            // 层级索引不超过颜色表长度
            int colorIndex = Mathf.Min(cluster.level, levelColors.Length - 1);
            Color col = levelColors[colorIndex];

            foreach (int tri in cluster.triangles)
            {
                int i0 = tris[tri * 3 + 0];
                int i1 = tris[tri * 3 + 1];
                int i2 = tris[tri * 3 + 2];

                vertexColors[i0] = col;
                vertexColors[i1] = col;
                vertexColors[i2] = col;
            }
        }

        DebugMesh.colors = vertexColors;
    }

    void OnDrawGizmos()
    {
        //if (visibleClusters == null) return;
//
        //foreach (var cluster in visibleClusters)
        //{
        //    Gizmos.color = Color.Lerp(Color.red, Color.green, cluster.level / 5f);
        //    Gizmos.DrawWireSphere(cluster.center, cluster.radius);
        //}
    }
}
