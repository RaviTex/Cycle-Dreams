using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MeshSplineGenerator))]
public class MeshSplineGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        MeshSplineGenerator generator =
            (MeshSplineGenerator)target;

        GUILayout.Space(10);

        if (GUILayout.Button("Generate Spline"))
        {
            generator.GenerateSpline();

            EditorUtility.SetDirty(generator);

            if (generator.splineContainer != null)
                EditorUtility.SetDirty(generator.splineContainer);
        }
    }
}
