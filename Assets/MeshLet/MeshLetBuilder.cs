using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random;

/// <summary>
/// 基础的按三角形平均分割Cluster
/// </summary>
public class MeshLetBuilder : MonoBehaviour
{
    class Cluster
    {
        public List<int> triangles = new List<int>();
    }

    //平均分割Cluster
    List<Cluster> BuilderClusters(Mesh mesh, int triPerCluster)
    {
        int[] tris = mesh.triangles;

        int triCount = tris.Length / 3;

        List<Cluster> clusters = new List<Cluster>();

        Cluster current = new Cluster();

        for (int i = 0; i < triCount; i++)
        {
            if (current.triangles.Count >= triPerCluster)
            {
                clusters.Add(current);
                current = new Cluster();
            }

            current.triangles.Add(i);
        }

        if (current.triangles.Count > 0)
            clusters.Add(current);

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
            List<Cluster> clusters = BuilderClusters(mesh, 64);

            Mesh debugMesh = BuildDebugMesh(mesh, clusters);

            GetComponent<MeshFilter>().sharedMesh = debugMesh;
        }
    }
}
