using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Splines;
using System.Linq;

[ExecuteAlways]
public class MeshSplineGenerator : MonoBehaviour
{
    public MeshFilter meshFilter;
    public SplineContainer splineContainer;

    [Range(0.1f, 5f)]
    public float knotSpacing = 2f;

    public void GenerateSpline()
    {
        if (!meshFilter || !splineContainer)
            return;

        Mesh mesh = meshFilter.sharedMesh;
        Vector3[] vertices = mesh.vertices;
        int[] triangles = mesh.triangles;

        Dictionary<Edge, int> edgeUseCount = new();

        void AddEdge(int a, int b)
        {
            Edge e = new Edge(a, b);

            if (edgeUseCount.ContainsKey(e))
                edgeUseCount[e]++;
            else
                edgeUseCount[e] = 1;
        }

        for (int i = 0; i < triangles.Length; i += 3)
        {
            int a = triangles[i];
            int b = triangles[i + 1];
            int c = triangles[i + 2];

            AddEdge(a, b);
            AddEdge(b, c);
            AddEdge(c, a);
        }

        List<Edge> boundaryEdges =
            edgeUseCount
                .Where(x => x.Value == 1)
                .Select(x => x.Key)
                .ToList();

        Dictionary<int, List<int>> boundaryGraph = new();

        foreach (Edge edge in boundaryEdges)
        {
            if (!boundaryGraph.ContainsKey(edge.A))
                boundaryGraph[edge.A] = new List<int>();

            if (!boundaryGraph.ContainsKey(edge.B))
                boundaryGraph[edge.B] = new List<int>();

            boundaryGraph[edge.A].Add(edge.B);
            boundaryGraph[edge.B].Add(edge.A);
        }

        List<int> endpoints = boundaryGraph
            .Where(x => x.Value.Count == 2)
            .Select(x => x.Key)
            .ToList();

        float maxDistance = 0;
        int start = -1;
        int end = -1;

        for (int i = 0; i < endpoints.Count; i++)
        {
            for (int j = i + 1; j < endpoints.Count; j++)
            {
                float d =
                    Vector3.Distance(
                        vertices[endpoints[i]],
                        vertices[endpoints[j]]);

                if (d > maxDistance)
                {
                    maxDistance = d;
                    start = endpoints[i];
                    end = endpoints[j];
                }
            }
        }

        if (start == -1 || end == -1)
        {
            Debug.LogError("Could not determine road endpoints.");
            return;
        }

        List<int> path = FindBoundaryPath(
            boundaryGraph,
            start,
            end);

        if (path.Count < 2)
        {
            Debug.LogError("Failed to find path.");
            return;
        }

        List<Vector3> sampledPoints = new();

        float accumulated = 0f;

        sampledPoints.Add(vertices[path[0]]);

        for (int i = 1; i < path.Count; i++)
        {
            Vector3 p0 = vertices[path[i - 1]];
            Vector3 p1 = vertices[path[i]];

            accumulated += Vector3.Distance(p0, p1);

            if (accumulated >= knotSpacing)
            {
                accumulated = 0f;
                sampledPoints.Add(p1);
            }
        }

        Spline spline = splineContainer.Spline;
        spline.Clear();

        foreach (Vector3 p in sampledPoints)
        {
            spline.Add(new BezierKnot(p));
        }

        Debug.Log($"Generated spline with {sampledPoints.Count} knots.");
    }

    private List<int> FindBoundaryPath(
        Dictionary<int, List<int>> graph,
        int start,
        int end)
    {
        Queue<int> queue = new();
        Dictionary<int, int> previous = new();

        queue.Enqueue(start);
        previous[start] = -1;

        while (queue.Count > 0)
        {
            int current = queue.Dequeue();

            if (current == end)
                break;

            foreach (int next in graph[current])
            {
                if (previous.ContainsKey(next))
                    continue;

                previous[next] = current;
                queue.Enqueue(next);
            }
        }

        if (!previous.ContainsKey(end))
            return new List<int>();

        List<int> path = new();

        int node = end;

        while (node != -1)
        {
            path.Add(node);
            node = previous[node];
        }

        path.Reverse();

        return path;
    }

    struct Edge
    {
        public int A;
        public int B;

        public Edge(int a, int b)
        {
            if (a < b)
            {
                A = a;
                B = b;
            }
            else
            {
                A = b;
                B = a;
            }
        }

        public override int GetHashCode()
        {
            return A * 73856093 ^ B * 19349663;
        }

        public override bool Equals(object obj)
        {
            if (obj is not Edge other)
                return false;

            return A == other.A && B == other.B;
        }
    }
}