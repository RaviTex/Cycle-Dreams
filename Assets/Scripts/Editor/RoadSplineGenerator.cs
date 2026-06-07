// =============================================================================
// Road Spline Generator v2 — Unity Editor Tool
// =============================================================================
// Generates a Unity.Splines spline that follows the centerline of a road mesh.
// Handles forked roads (splits that merge back) by routing through one branch
// via Dijkstra shortest-path through the top-surface triangle graph.
//
// Algorithm:
//   1. Collect world-space mesh data
//   2. Detect top-surface triangles (face normal within Max Bank Angle of up)
//   3. Build triangle-adjacency graph of the top surface
//   4. Find road endpoints (farthest-apart boundary vertex clusters)
//   5. Dijkstra shortest path from start triangle → end triangle
//   6. Resample the triangle-centroid path uniformly
//   7. Cross-section refinement: at each sample, re-centre using a plane ⊥
//      to the local tangent, clustering to handle forked sections
//   8. Smooth + Y offset
//   9. Output a SplineContainer with AutoSmooth knots
//
// Requirements:
//   - Unity 2022+ with the Splines package (com.unity.splines) installed
//   - Place this file in any Editor folder (e.g. Assets/Editor/)
//
// Usage:
//   Tools > Road Spline Generator
// =============================================================================

using UnityEngine;
using UnityEngine.Splines;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;

public class RoadSplineGeneratorWindow : EditorWindow
{
    // ── Fork mode ───────────────────────────────────────────────────────────
    private enum ForkMode
    {
        Auto,       // follow the branch most aligned with current direction
        Left,       // follow the leftmost branch
        Right       // follow the rightmost branch
    }

    // ── Settings ────────────────────────────────────────────────────────────
    private GameObject _roadObject;
    private float _maxBankAngle = 45f;
    private int _sampleCount = 100;
    private int _refinementPasses = 2;
    private int _smoothPasses = 3;
    private float _splineYOffset = 0.15f;
    private ForkMode _forkMode = ForkMode.Auto;
    private bool _includeChildren = true;
    private bool _parentToRoad = true;

    // ── Debug ───────────────────────────────────────────────────────────────
    private bool _showDebugViz;
    private bool _showTopSurface;
    private bool _showDijkstraPath;
    private bool _showCrossSections;

    // ── Debug data ──────────────────────────────────────────────────────────
    private List<Vector3> _dbgTopVerts;
    private List<Vector3> _dbgDijkstraPath;
    private List<Vector3> _dbgCenterline;
    private List<(Vector3 left, Vector3 right)> _dbgCrossSections;

    // ══════════════════════════════════════════════════════════════════════════
    //  MENU & WINDOW
    // ══════════════════════════════════════════════════════════════════════════
    [MenuItem("Tools/Road Spline Generator")]
    public static void ShowWindow()
    {
        var w = GetWindow<RoadSplineGeneratorWindow>("Road Spline Gen");
        w.minSize = new Vector2(330, 480);
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  GUI
    // ══════════════════════════════════════════════════════════════════════════
    private void OnGUI()
    {
        EditorGUILayout.LabelField("Road Spline Generator v2", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox(
            "Assign the road GameObject, configure settings, then click Generate.\n" +
            "Supports forked roads (splits that merge back).",
            MessageType.Info);
        EditorGUILayout.Space(4);

        _roadObject = (GameObject)EditorGUILayout.ObjectField(
            "Road Object", _roadObject, typeof(GameObject), true);
        _includeChildren = EditorGUILayout.Toggle("Include Child Meshes", _includeChildren);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Detection", EditorStyles.miniBoldLabel);
        _maxBankAngle = EditorGUILayout.Slider("Max Bank Angle", _maxBankAngle, 5f, 85f);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Sampling", EditorStyles.miniBoldLabel);
        _sampleCount = EditorGUILayout.IntSlider("Sample Count", _sampleCount, 20, 500);
        _refinementPasses = EditorGUILayout.IntSlider("Refinement Passes", _refinementPasses, 0, 5);
        _smoothPasses = EditorGUILayout.IntSlider("Smooth Passes", _smoothPasses, 0, 10);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Fork Handling", EditorStyles.miniBoldLabel);
        _forkMode = (ForkMode)EditorGUILayout.EnumPopup("Fork Mode", _forkMode);
        EditorGUILayout.HelpBox(
            "Auto: follow the branch most aligned with current direction.\n" +
            "Left / Right: force the left or right branch at every fork.",
            MessageType.None);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Output", EditorStyles.miniBoldLabel);
        _splineYOffset = EditorGUILayout.FloatField("Y Offset", _splineYOffset);
        _parentToRoad = EditorGUILayout.Toggle("Parent to Road", _parentToRoad);

        EditorGUILayout.Space(4);
        EditorGUILayout.LabelField("Debug Visualisation", EditorStyles.miniBoldLabel);
        _showDebugViz = EditorGUILayout.Toggle("Enabled", _showDebugViz);
        if (_showDebugViz)
        {
            _showTopSurface = EditorGUILayout.Toggle("  Top Surface", _showTopSurface);
            _showDijkstraPath = EditorGUILayout.Toggle("  Dijkstra Path", _showDijkstraPath);
            _showCrossSections = EditorGUILayout.Toggle("  Cross-Sections", _showCrossSections);
        }

        EditorGUILayout.Space(8);
        EditorGUI.BeginDisabledGroup(_roadObject == null);
        if (GUILayout.Button("Generate Spline", GUILayout.Height(32)))
        {
            try
            {
                Generate();
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[RoadSplineGenerator] {e.Message}\n{e.StackTrace}");
                EditorUtility.DisplayDialog("Road Spline Generator \u2014 Error", e.Message, "OK");
            }
        }
        EditorGUI.EndDisabledGroup();

        if (GUILayout.Button("Clear Debug Data"))
            ClearDebug();

        EditorGUILayout.Space(4);
        if (_dbgCenterline != null)
            EditorGUILayout.LabelField($"Centerline: {_dbgCenterline.Count} pts");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  MAIN GENERATION PIPELINE
    // ══════════════════════════════════════════════════════════════════════════
    private void Generate()
    {
        // ── Step 0: Collect world-space mesh ─────────────────────────────────
        CollectWorldMesh(_roadObject, _includeChildren, out var verts, out var tris);
        if (verts.Length < 3 || tris.Length < 3)
            throw new System.Exception("Mesh has insufficient geometry.");

        // ── Step 1: Top-surface triangles ────────────────────────────────────
        float cosThresh = Mathf.Cos(_maxBankAngle * Mathf.Deg2Rad);
        var topTriIdx = FindTopSurfaceTriangles(verts, tris, cosThresh);
        if (topTriIdx.Count == 0)
            throw new System.Exception(
                "No top-surface triangles detected. Try increasing Max Bank Angle.");

        int numTopTris = topTriIdx.Count / 3;
        Debug.Log($"[RoadSplineGenerator] Top-surface triangles: {numTopTris}");

        // ── Step 2: Triangle adjacency graph ─────────────────────────────────
        var triCentroids = ComputeTriCentroids(verts, topTriIdx);
        var triAdj = BuildTriangleAdjacency(topTriIdx, triCentroids);

        // ── Step 3: Find road endpoints ──────────────────────────────────────
        var boundaryEdges = FindBoundaryEdges(topTriIdx);
        var (startPt, endPt) = FindRoadEndpoints(verts, boundaryEdges);

        Debug.Log($"[RoadSplineGenerator] Endpoints: {startPt} → {endPt}, " +
                  $"distance = {Vector3.Distance(startPt, endPt):F1}m");

        // ── Step 4: Dijkstra shortest path through triangle graph ────────────
        int startTri = FindClosestTriTo(triCentroids, startPt);
        int endTri = FindClosestTriTo(triCentroids, endPt);

        var triPath = DijkstraShortestPath(triAdj, startTri, endTri, numTopTris);
        if (triPath == null || triPath.Count == 0)
            throw new System.Exception(
                "Dijkstra could not find a path through the top surface.\n" +
                "The mesh may have disconnected components or the bank angle " +
                "is too low.");

        // Convert triangle path to centroid positions
        var dijkstraPath = new List<Vector3>(triPath.Count);
        foreach (int t in triPath)
            dijkstraPath.Add(triCentroids[t]);

        Debug.Log($"[RoadSplineGenerator] Dijkstra path: {triPath.Count} triangles, " +
                  $"arc length = {PolylineArcLength(dijkstraPath):F1}m");

        // ── Step 5: Resample path uniformly ──────────────────────────────────
        var resampled = ResamplePolyline(dijkstraPath, _sampleCount);

        // ── Step 6: Estimate road width ──────────────────────────────────────
        float roadWidth = EstimateRoadWidth(verts, topTriIdx);
        Debug.Log($"[RoadSplineGenerator] Estimated road width: {roadWidth:F2}m");

        // ── Step 7: Collect unique top-surface vertex positions for queries ──
        var topVertSet = new HashSet<int>();
        for (int i = 0; i < topTriIdx.Count; i++)
            topVertSet.Add(topTriIdx[i]);
        var topVerts = new List<Vector3>(topVertSet.Count);
        foreach (int idx in topVertSet)
            topVerts.Add(verts[idx]);

        // ── Step 8: Cross-section refinement (handles forks) ─────────────────
        var centerline = resampled;
        for (int p = 0; p < _refinementPasses; p++)
            centerline = RefineWithCrossSections(
                centerline, topVerts, roadWidth, _forkMode);

        // ── Step 9: Smoothing ────────────────────────────────────────────────
        centerline = SmoothCenterline(centerline, _smoothPasses);

        // ── Step 10: Y offset ────────────────────────────────────────────────
        if (!Mathf.Approximately(_splineYOffset, 0f))
            for (int i = 0; i < centerline.Count; i++)
                centerline[i] += Vector3.up * _splineYOffset;

        // ── Step 11: Debug data ──────────────────────────────────────────────
        _dbgTopVerts = topVerts;
        _dbgDijkstraPath = dijkstraPath;
        _dbgCenterline = centerline;

        // ── Step 12: Create spline ───────────────────────────────────────────
        CreateSplineObject(centerline, false);

        Debug.Log($"[RoadSplineGenerator] Done — {centerline.Count} knots, " +
                  $"road width ≈ {roadWidth:F1}m");
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  MESH COLLECTION
    // ══════════════════════════════════════════════════════════════════════════
    private static void CollectWorldMesh(
        GameObject root, bool includeChildren, out Vector3[] verts, out int[] tris)
    {
        var allVerts = new List<Vector3>();
        var allTris = new List<int>();

        MeshFilter[] mfs = includeChildren
            ? root.GetComponentsInChildren<MeshFilter>()
            : root.GetComponents<MeshFilter>();

        if (mfs.Length == 0)
            throw new System.Exception("No MeshFilter found on the road object.");

        foreach (var mf in mfs)
        {
            Mesh m = mf.sharedMesh;
            if (m == null) continue;
            if (!m.isReadable)
                throw new System.Exception(
                    $"Mesh \"{m.name}\" is not readable. Enable Read/Write " +
                    $"Enabled in the model import settings.");

            int baseIdx = allVerts.Count;
            var localVerts = m.vertices;
            for (int i = 0; i < localVerts.Length; i++)
                allVerts.Add(mf.transform.TransformPoint(localVerts[i]));

            for (int s = 0; s < m.subMeshCount; s++)
            {
                var subTris = m.GetTriangles(s);
                for (int i = 0; i < subTris.Length; i++)
                    allTris.Add(baseIdx + subTris[i]);
            }
        }

        verts = allVerts.ToArray();
        tris = allTris.ToArray();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TOP SURFACE DETECTION
    // ══════════════════════════════════════════════════════════════════════════
    private static List<int> FindTopSurfaceTriangles(
        Vector3[] verts, int[] tris, float cosThresh)
    {
        var result = new List<int>();
        for (int t = 0; t < tris.Length; t += 3)
        {
            int i0 = tris[t], i1 = tris[t + 1], i2 = tris[t + 2];
            Vector3 n = TriangleNormal(verts[i0], verts[i1], verts[i2]);
            if (n.y > cosThresh)
            {
                result.Add(i0);
                result.Add(i1);
                result.Add(i2);
            }
        }
        return result;
    }

    private static Vector3 TriangleNormal(Vector3 a, Vector3 b, Vector3 c)
    {
        var n = Vector3.Cross(b - a, c - a);
        float len = n.magnitude;
        return len > 1e-8f ? n / len : Vector3.up;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TRIANGLE GRAPH
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Compute the centroid of each top-surface triangle.
    /// </summary>
    private static Vector3[] ComputeTriCentroids(Vector3[] verts, List<int> topTriIdx)
    {
        int n = topTriIdx.Count / 3;
        var centroids = new Vector3[n];
        for (int t = 0; t < n; t++)
        {
            int i0 = topTriIdx[t * 3], i1 = topTriIdx[t * 3 + 1], i2 = topTriIdx[t * 3 + 2];
            centroids[t] = (verts[i0] + verts[i1] + verts[i2]) / 3f;
        }
        return centroids;
    }

    /// <summary>
    /// Build an adjacency graph where nodes are top-surface triangle indices
    /// and edges connect triangles that share a vertex-pair (mesh edge).
    /// Weight = distance between centroids.
    /// </summary>
    private static Dictionary<int, List<(int neighbour, float cost)>> BuildTriangleAdjacency(
        List<int> topTriIdx, Vector3[] centroids)
    {
        int numTris = topTriIdx.Count / 3;

        // Map: encoded edge → triangle index that owns it
        var edgeOwner = new Dictionary<long, int>();

        var adj = new Dictionary<int, List<(int, float)>>();
        for (int t = 0; t < numTris; t++)
            adj[t] = new List<(int, float)>(6);

        for (int t = 0; t < numTris; t++)
        {
            int i0 = topTriIdx[t * 3], i1 = topTriIdx[t * 3 + 1], i2 = topTriIdx[t * 3 + 2];

            TryLink(edgeOwner, adj, t, i0, i1, centroids);
            TryLink(edgeOwner, adj, t, i1, i2, centroids);
            TryLink(edgeOwner, adj, t, i2, i0, centroids);
        }

        return adj;
    }

    private static void TryLink(
        Dictionary<long, int> edgeOwner,
        Dictionary<int, List<(int, float)>> adj,
        int triIdx, int va, int vb, Vector3[] centroids)
    {
        long key = EncodeEdge(va, vb);
        if (edgeOwner.TryGetValue(key, out int otherTri))
        {
            float cost = Vector3.Distance(centroids[triIdx], centroids[otherTri]);
            adj[triIdx].Add((otherTri, cost));
            adj[otherTri].Add((triIdx, cost));
        }
        else
        {
            edgeOwner[key] = triIdx;
        }
    }

    private static long EncodeEdge(int a, int b)
    {
        if (a > b) (a, b) = (b, a);
        return ((long)a << 32) | (uint)b;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  BOUNDARY & ENDPOINT DETECTION
    // ══════════════════════════════════════════════════════════════════════════
    private static List<(int a, int b)> FindBoundaryEdges(List<int> topTriIdx)
    {
        var edgeCount = new Dictionary<long, int>();
        var edgeVerts = new Dictionary<long, (int, int)>();

        for (int t = 0; t < topTriIdx.Count; t += 3)
        {
            int i0 = topTriIdx[t], i1 = topTriIdx[t + 1], i2 = topTriIdx[t + 2];
            CountEdge(i0, i1, edgeCount, edgeVerts);
            CountEdge(i1, i2, edgeCount, edgeVerts);
            CountEdge(i2, i0, edgeCount, edgeVerts);
        }

        var boundary = new List<(int, int)>();
        foreach (var kv in edgeCount)
            if (kv.Value == 1)
                boundary.Add(edgeVerts[kv.Key]);
        return boundary;
    }

    private static void CountEdge(
        int a, int b, Dictionary<long, int> edgeCount,
        Dictionary<long, (int, int)> edgeVerts)
    {
        long key = EncodeEdge(a, b);
        if (!edgeCount.ContainsKey(key))
        {
            edgeCount[key] = 0;
            edgeVerts[key] = (a, b);
        }
        edgeCount[key]++;
    }

    /// <summary>
    /// Find the two road endpoints by locating the farthest-apart pair of
    /// boundary vertices, then returning the centroids of their local clusters.
    /// This works for forked roads because the fork is short compared to the
    /// total road length — the farthest pair is always at the road ends.
    /// </summary>
    private static (Vector3 start, Vector3 end) FindRoadEndpoints(
        Vector3[] verts, List<(int a, int b)> boundaryEdges)
    {
        // Collect unique boundary vertex indices
        var bndSet = new HashSet<int>();
        foreach (var (a, b) in boundaryEdges) { bndSet.Add(a); bndSet.Add(b); }
        var bndList = bndSet.ToList();

        if (bndList.Count < 2)
            throw new System.Exception("Not enough boundary vertices to find endpoints.");

        // Coarse O(n) farthest-pair search via sampling
        int step = Mathf.Max(1, bndList.Count / 200);
        float maxDist = 0f;
        int bestI = 0, bestJ = 0;

        for (int i = 0; i < bndList.Count; i += step)
        {
            for (int j = i + bndList.Count / 4; j < bndList.Count; j += step)
            {
                float d = Vector3.Distance(verts[bndList[i]], verts[bndList[j]]);
                if (d > maxDist) { maxDist = d; bestI = i; bestJ = j; }
            }
        }

        // Refine around the coarse result
        int r = step * 4 + 1;
        for (int i = Mathf.Max(0, bestI - r); i <= Mathf.Min(bndList.Count - 1, bestI + r); i++)
        {
            for (int j = Mathf.Max(0, bestJ - r); j <= Mathf.Min(bndList.Count - 1, bestJ + r); j++)
            {
                if (i == j) continue;
                float d = Vector3.Distance(verts[bndList[i]], verts[bndList[j]]);
                if (d > maxDist) { maxDist = d; bestI = i; bestJ = j; }
            }
        }

        int vertA = bndList[bestI], vertB = bndList[bestJ];

        // Cluster nearby boundary vertices around each endpoint
        float clusterRadius = maxDist * 0.05f; // 5 % of road length
        var startCluster = ClusterAround(verts, bndList, verts[vertA], clusterRadius);
        var endCluster = ClusterAround(verts, bndList, verts[vertB], clusterRadius);

        Vector3 startPt = Centroid(startCluster);
        Vector3 endPt = Centroid(endCluster);

        return (startPt, endPt);
    }

    private static List<Vector3> ClusterAround(
        Vector3[] verts, List<int> candidates, Vector3 centre, float radius)
    {
        var result = new List<Vector3>();
        foreach (int idx in candidates)
        {
            if (Vector3.Distance(verts[idx], centre) <= radius)
                result.Add(verts[idx]);
        }
        return result.Count > 0 ? result : new List<Vector3> { centre };
    }

    private static Vector3 Centroid(List<Vector3> pts)
    {
        Vector3 sum = Vector3.zero;
        foreach (var p in pts) sum += p;
        return sum / pts.Count;
    }

    private static int FindClosestTriTo(Vector3[] triCentroids, Vector3 point)
    {
        int best = 0;
        float bestD = float.MaxValue;
        for (int i = 0; i < triCentroids.Length; i++)
        {
            float d = Vector3.Distance(triCentroids[i], point);
            if (d < bestD) { bestD = d; best = i; }
        }
        return best;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DIJKSTRA SHORTEST PATH
    // ══════════════════════════════════════════════════════════════════════════
    private static List<int> DijkstraShortestPath(
        Dictionary<int, List<(int neighbour, float cost)>> adj,
        int start, int end, int numNodes)
    {
        var dist = new float[numNodes];
        var prev = new int[numNodes];
        var visited = new bool[numNodes];

        for (int i = 0; i < numNodes; i++)
        {
            dist[i] = float.MaxValue;
            prev[i] = -1;
        }
        dist[start] = 0f;

        var heap = new MinHeap();
        heap.Push(0f, start);

        while (heap.Count > 0)
        {
            var (d, u) = heap.Pop();
            if (visited[u]) continue;
            visited[u] = true;

            if (u == end) break;

            if (!adj.ContainsKey(u)) continue;
            foreach (var (v, w) in adj[u])
            {
                float nd = d + w;
                if (nd < dist[v])
                {
                    dist[v] = nd;
                    prev[v] = u;
                    heap.Push(nd, v);
                }
            }
        }

        if (!visited[end])
            return null; // no path

        // Reconstruct path
        var path = new List<int>();
        int cur = end;
        while (cur != -1)
        {
            path.Add(cur);
            cur = prev[cur];
        }
        path.Reverse();
        return path;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  MIN-HEAP  (for Dijkstra)
    // ══════════════════════════════════════════════════════════════════════════
    private class MinHeap
    {
        private readonly List<(float key, int val)> _data =
            new List<(float, int)>(256);

        public int Count => _data.Count;

        public void Push(float key, int val)
        {
            _data.Add((key, val));
            int i = _data.Count - 1;
            while (i > 0)
            {
                int p = (i - 1) >> 1;
                if (_data[p].key <= _data[i].key) break;
                (_data[p], _data[i]) = (_data[i], _data[p]);
                i = p;
            }
        }

        public (float key, int val) Pop()
        {
            var root = _data[0];
            _data[0] = _data[_data.Count - 1];
            _data.RemoveAt(_data.Count - 1);

            int i = 0;
            while (true)
            {
                int l = 2 * i + 1, r = 2 * i + 2, s = i;
                if (l < _data.Count && _data[l].key < _data[s].key) s = l;
                if (r < _data.Count && _data[r].key < _data[s].key) s = r;
                if (s == i) break;
                (_data[s], _data[i]) = (_data[i], _data[s]);
                i = s;
            }
            return root;
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  POLYLINE UTILITIES
    // ══════════════════════════════════════════════════════════════════════════
    private static float PolylineArcLength(List<Vector3> pts)
    {
        float len = 0f;
        for (int i = 1; i < pts.Count; i++)
            len += Vector3.Distance(pts[i - 1], pts[i]);
        return len;
    }

    /// <summary>
    /// Resample a polyline to <paramref name="count"/> equidistant points.
    /// </summary>
    private static List<Vector3> ResamplePolyline(List<Vector3> pts, int count)
    {
        if (pts.Count < 2)
            return Enumerable.Repeat(pts[0], count).ToList();

        var cum = new float[pts.Count];
        for (int i = 1; i < pts.Count; i++)
            cum[i] = cum[i - 1] + Vector3.Distance(pts[i - 1], pts[i]);

        float totalLen = cum[pts.Count - 1];
        if (totalLen < 1e-6f)
            return Enumerable.Repeat(pts[0], count).ToList();

        var result = new List<Vector3>(count);
        for (int s = 0; s < count; s++)
        {
            float t = (float)s / (count - 1);
            float target = t * totalLen;

            int lo = 0, hi = pts.Count - 1;
            while (lo < hi - 1)
            {
                int mid = (lo + hi) >> 1;
                if (cum[mid] < target) lo = mid; else hi = mid;
            }

            float segLen = cum[hi] - cum[lo];
            float frac = segLen > 1e-8f ? (target - cum[lo]) / segLen : 0f;
            result.Add(Vector3.Lerp(pts[lo], pts[hi], frac));
        }
        return result;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  ROAD WIDTH ESTIMATION
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Estimate the road width by sampling boundary-edge pairs that lie on
    /// opposite sides of the road (perpendicular to the road axis).
    /// </summary>
    private static float EstimateRoadWidth(Vector3[] verts, List<int> topTriIdx)
    {
        // Quick estimate: sample a few cross-sections
        var topSet = new HashSet<int>();
        for (int i = 0; i < topTriIdx.Count; i++)
            topSet.Add(topTriIdx[i]);
        var topVerts = new List<Vector3>(topSet.Count);
        foreach (int idx in topSet)
            topVerts.Add(verts[idx]);

        if (topVerts.Count < 2) return 5f;

        // Use PCA to find the two principal axes (road direction + width)
        Vector3 mean = Centroid(topVerts);
        float cxx = 0, cxy = 0, cxz = 0, cyy = 0, cyz = 0, czz = 0;
        foreach (var v in topVerts)
        {
            var d = v - mean;
            cxx += d.x * d.x; cxy += d.x * d.y; cxz += d.x * d.z;
            cyy += d.y * d.y; cyz += d.y * d.z; czz += d.z * d.z;
        }
        int n = topVerts.Count;
        cxx /= n; cxy /= n; cxz /= n; cyy /= n; cyz /= n; czz /= n;

        // Width ≈ 2 * standard deviation along the minor horizontal axis
        // Rough but good enough for setting search thresholds
        float maxSpread = Mathf.Sqrt(Mathf.Max(cxx, cyy, czz));

        // Sample random pairs to find a representative width
        float widthSum = 0f;
        int widthSamples = 0;
        var rng = new System.Random(42);
        int trials = Mathf.Min(500, topVerts.Count * (topVerts.Count - 1) / 2);

        for (int t = 0; t < trials; t++)
        {
            int a = rng.Next(topVerts.Count);
            int b = rng.Next(topVerts.Count);
            if (a == b) continue;

            float d = Vector3.Distance(topVerts[a], topVerts[b]);
            // Only count "short" distances (likely width, not length)
            if (d < maxSpread * 0.3f)
            {
                widthSum += d;
                widthSamples++;
            }
        }

        return widthSamples > 0 ? widthSum / widthSamples : maxSpread * 0.1f;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  CROSS-SECTION REFINEMENT  (fork-aware)
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// For each centreline point, creates a plane perpendicular to the local
    /// tangent, finds all top-surface vertices in that cross-section, clusters
    /// them (to handle forks), and recenters on the chosen cluster's centroid.
    /// </summary>
    private List<Vector3> RefineWithCrossSections(
        List<Vector3> centerline, List<Vector3> topVerts,
        float roadWidth, ForkMode forkMode)
    {
        // Average spacing between samples
        float spacing = 0f;
        for (int i = 1; i < centerline.Count; i++)
            spacing += Vector3.Distance(centerline[i - 1], centerline[i]);
        spacing /= Mathf.Max(1, centerline.Count - 1);

        float sliceTolerance = spacing * 2.5f;    // along-tangent range
        float maxHalfWidth = roadWidth * 2f;       // across-tangent range
        float clusterMerge = roadWidth * 0.4f;     // min gap between clusters
        float verticalTolerance = roadWidth * 0.8f; // ignore high/low outliers

        var refined = new List<Vector3>(centerline.Count);
        var crossDbg = new List<(Vector3, Vector3)>();

        for (int ci = 0; ci < centerline.Count; ci++)
        {
            Vector3 C = centerline[ci];
            Vector3 T = ComputeTangent(centerline, ci);

            // Horizontal perpendicular to the tangent
            Vector3 perp = Vector3.Cross(T, Vector3.up);
            if (perp.sqrMagnitude < 0.001f)
                perp = Vector3.Cross(T, Vector3.forward);
            perp = perp.normalized;

            // Collect points in this cross-section
            var slicePoints = new List<Vector3>();
            foreach (var V in topVerts)
            {
                float along = Vector3.Dot(V - C, T);
                if (Mathf.Abs(along) > sliceTolerance) continue;

                float vertOff = Mathf.Abs(Vector3.Dot(V - C, Vector3.up));
                if (vertOff > verticalTolerance) continue;

                float across = Mathf.Abs(Vector3.Dot(V - C, perp));
                if (across > maxHalfWidth) continue;

                slicePoints.Add(V);
            }

            if (slicePoints.Count == 0)
            {
                refined.Add(C);
                continue;
            }

            // Cluster the slice points
            var clusters = ClusterPoints(slicePoints, clusterMerge);

            Vector3 newCentre;

            if (clusters.Count == 1)
            {
                // Simple case: no fork here — use the cross-section's centroid
                newCentre = CrossSectionCentre(clusters[0], T, perp, C);
            }
            else
            {
                // Fork detected! Pick the appropriate cluster.
                newCentre = PickForkCluster(clusters, T, perp, C, ci, centerline, forkMode);
            }

            refined.Add(newCentre);

            // Debug: record cross-section extents
            if (clusters.Count >= 1)
            {
                var mainCluster = clusters[0];
                float minA = float.MaxValue, maxA = float.MinValue;
                Vector3 left = C, right = C;
                foreach (var V in mainCluster)
                {
                    float a = Vector3.Dot(V - C, perp);
                    if (a < minA) { minA = a; left = V; }
                    if (a > maxA) { maxA = a; right = V; }
                }
                crossDbg.Add((left, right));
            }
        }

        _dbgCrossSections = crossDbg;
        return refined;
    }

    /// <summary>
    /// Compute the centre of a cross-section cluster as the midpoint between
    /// its leftmost and rightmost points (perpendicular to the tangent).
    /// </summary>
    private static Vector3 CrossSectionCentre(
        List<Vector3> cluster, Vector3 tangent, Vector3 perp, Vector3 reference)
    {
        float minA = float.MaxValue, maxA = float.MinValue;
        Vector3 left = reference, right = reference;

        foreach (var V in cluster)
        {
            float a = Vector3.Dot(V - reference, perp);
            if (a < minA) { minA = a; left = V; }
            if (a > maxA) { maxA = a; right = V; }
        }

        return (left + right) * 0.5f;
    }

    /// <summary>
    /// Pick which cluster to follow at a fork.
    /// </summary>
    private static Vector3 PickForkCluster(
        List<List<Vector3>> clusters, Vector3 tangent, Vector3 perp,
        Vector3 reference, int ci, List<Vector3> centerline, ForkMode forkMode)
    {
        // Compute each cluster's centroid
        var centroids = clusters.Select(cl => Centroid(cl)).ToList();

        // The "forward" direction: use previous centreline direction if available
        Vector3 forward = tangent;
        if (ci > 0)
            forward = (centerline[ci] - centerline[ci - 1]).normalized;

        // Perpendicular horizontal direction
        Vector3 right = Vector3.Cross(forward, Vector3.up);
        if (right.sqrMagnitude < 0.001f)
            right = Vector3.Cross(forward, Vector3.forward);
        right = right.normalized;

        switch (forkMode)
        {
            case ForkMode.Left:
                {
                    // Pick the cluster with the most negative dot product with "right"
                    int best = 0;
                    float bestDot = float.MaxValue;
                    for (int i = 0; i < centroids.Count; i++)
                    {
                        float d = Vector3.Dot(centroids[i] - reference, right);
                        if (d < bestDot) { bestDot = d; best = i; }
                    }
                    return CrossSectionCentre(clusters[best], tangent, perp, reference);
                }

            case ForkMode.Right:
                {
                    // Pick the cluster with the most positive dot product with "right"
                    int best = 0;
                    float bestDot = float.MinValue;
                    for (int i = 0; i < centroids.Count; i++)
                    {
                        float d = Vector3.Dot(centroids[i] - reference, right);
                        if (d > bestDot) { bestDot = d; best = i; }
                    }
                    return CrossSectionCentre(clusters[best], tangent, perp, reference);
                }

            default: // ForkMode.Auto
                {
                    // Pick the cluster whose centroid is most aligned with the
                    // current travel direction (i.e. most "straight ahead")
                    int best = 0;
                    float bestDot = float.MinValue;
                    for (int i = 0; i < centroids.Count; i++)
                    {
                        Vector3 toCluster = (centroids[i] - reference).normalized;
                        float d = Vector3.Dot(toCluster, forward);
                        if (d > bestDot) { bestDot = d; best = i; }
                    }
                    return CrossSectionCentre(clusters[best], tangent, perp, reference);
                }
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SIMPLE CLUSTERING  (single-linkage, distance threshold)
    // ══════════════════════════════════════════════════════════════════════════
    /// <summary>
    /// Cluster points using single-linkage agglomerative clustering.
    /// Two points in the same cluster if connected by a chain where each
    /// link is shorter than <paramref name="mergeDistance"/>.
    /// </summary>
    private static List<List<Vector3>> ClusterPoints(
        List<Vector3> points, float mergeDistance)
    {
        int n = points.Count;
        var parent = new int[n];
        for (int i = 0; i < n; i++) parent[i] = i;

        int Find(int x)
        {
            while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; }
            return x;
        }

        void Union(int a, int b) { parent[Find(a)] = Find(b); }

        float mergeSq = mergeDistance * mergeDistance;

        // Brute-force O(n^2) — fine for cross-section slices (typically < 500 pts)
        for (int i = 0; i < n; i++)
        {
            for (int j = i + 1; j < n; j++)
            {
                if ((points[i] - points[j]).sqrMagnitude < mergeSq)
                    Union(i, j);
            }
        }

        var groups = new Dictionary<int, List<Vector3>>();
        for (int i = 0; i < n; i++)
        {
            int root = Find(i);
            if (!groups.ContainsKey(root)) groups[root] = new List<Vector3>();
            groups[root].Add(points[i]);
        }

        // Return clusters sorted by size (largest first)
        return groups.Values.OrderByDescending(g => g.Count).ToList();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  TANGENT COMPUTATION
    // ══════════════════════════════════════════════════════════════════════════
    private static Vector3 ComputeTangent(List<Vector3> pts, int idx)
    {
        if (pts.Count < 2) return Vector3.forward;
        if (idx == 0) return (pts[1] - pts[0]).normalized;
        if (idx == pts.Count - 1)
            return (pts[pts.Count - 1] - pts[pts.Count - 2]).normalized;
        return (pts[idx + 1] - pts[idx - 1]).normalized;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SMOOTHING  (Laplacian, anchors endpoints)
    // ══════════════════════════════════════════════════════════════════════════
    private static List<Vector3> SmoothCenterline(List<Vector3> pts, int passes)
    {
        if (passes <= 0 || pts.Count < 3) return pts;

        var cur = new List<Vector3>(pts);
        for (int p = 0; p < passes; p++)
        {
            var next = new List<Vector3>(cur.Count);
            next.Add(cur[0]);
            for (int i = 1; i < cur.Count - 1; i++)
                next.Add((cur[i - 1] + cur[i] * 2f + cur[i + 1]) / 4f);
            next.Add(cur[cur.Count - 1]);
            cur = next;
        }
        return cur;
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  SPLINE CREATION
    // ══════════════════════════════════════════════════════════════════════════
    private void CreateSplineObject(List<Vector3> centerline, bool closed)
    {
        // Remove any existing RoadSpline child
        if (_parentToRoad && _roadObject != null)
        {
            var existing = _roadObject.transform.Find("RoadSpline");
            if (existing != null)
                Undo.DestroyObjectImmediate(existing.gameObject);
        }

        var go = new GameObject("RoadSpline");

        if (_parentToRoad && _roadObject != null)
        {
            go.transform.SetParent(_roadObject.transform, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;
        }

        var container = go.AddComponent<SplineContainer>();
        var xform = go.transform;

        var knots = new List<BezierKnot>(centerline.Count);
        foreach (var wp in centerline)
        {
            Vector3 local = xform.InverseTransformPoint(wp);
            knots.Add(new BezierKnot(local));
        }

        var spline = new Spline(knots, closed);
        for (int i = 0; i < spline.Count; i++)
            spline.SetTangentMode(i, TangentMode.AutoSmooth);

        container.Spline = spline;

        Undo.RegisterCreatedObjectUndo(go, "Create Road Spline");
        Selection.activeGameObject = go;
        SceneView.RepaintAll();
    }

    // ══════════════════════════════════════════════════════════════════════════
    //  DEBUG VISUALISATION
    // ══════════════════════════════════════════════════════════════════════════
    private void OnSceneGUI(SceneView sv)
    {
        if (!_showDebugViz) return;

        // Top surface dots
        if (_showTopSurface && _dbgTopVerts != null)
        {
            Handles.color = new Color(1f, 0.8f, 0.2f, 0.25f);
            foreach (var v in _dbgTopVerts)
                Handles.DotHandleCap(0, v, Quaternion.identity, 0.25f, EventType.Repaint);
        }

        // Dijkstra path (raw triangle centroids)
        if (_showDijkstraPath && _dbgDijkstraPath != null && _dbgDijkstraPath.Count > 1)
        {
            Handles.color = new Color(0f, 0.6f, 1f, 0.6f);
            Handles.DrawPolyLine(_dbgDijkstraPath.ToArray());
        }

        // Centreline
        if (_dbgCenterline != null && _dbgCenterline.Count > 1)
        {
            Handles.color = Color.green;
            Handles.DrawPolyLine(_dbgCenterline.ToArray());
        }

        // Cross-section lines
        if (_showCrossSections && _dbgCrossSections != null)
        {
            Handles.color = new Color(1f, 1f, 0f, 0.4f);
            int step = Mathf.Max(1, _dbgCrossSections.Count / 40);
            for (int i = 0; i < _dbgCrossSections.Count; i += step)
                Handles.DrawLine(_dbgCrossSections[i].left, _dbgCrossSections[i].right);
        }
    }

    private void OnEnable() => SceneView.duringSceneGui += OnSceneGUI;
    private void OnDisable() => SceneView.duringSceneGui -= OnSceneGUI;

    private void ClearDebug()
    {
        _dbgTopVerts = null;
        _dbgDijkstraPath = null;
        _dbgCenterline = null;
        _dbgCrossSections = null;
        SceneView.RepaintAll();
    }
}
