using UnityEngine;
using UnityEngine.Splines;

public class AnimalController : MonoBehaviour
{
    [SerializeField] private float timeToFinish = 5f;
    [SerializeField] private bool loop = false;
    [SerializeField] private bool isLoopReverse = false;
    [SerializeField] private SplineContainer splineContainer;
    private float elapsedTime = 0f;

    void Update()
    {
        if (splineContainer == null || splineContainer.Spline.Count == 0)
            return;

        elapsedTime += Time.deltaTime;
        float t = Mathf.Clamp01(elapsedTime / timeToFinish);
        Vector3 position = (Vector3)splineContainer.Spline.EvaluatePosition(t) + splineContainer.transform.position;
        Vector3 rotation = splineContainer.Spline.EvaluateTangent(t);
        rotation.Normalize();
        transform.rotation = Quaternion.LookRotation(rotation);
        transform.position = position;

        if (t >= 1f && loop)
        {
            if (isLoopReverse)
            {
                for (int i = 0; i < splineContainer.Spline.Count / 2; i++)
                {
                    var temp = splineContainer.Spline[i];
                    splineContainer.Spline[i] = splineContainer.Spline[splineContainer.Spline.Count - 1 - i];
                    splineContainer.Spline[splineContainer.Spline.Count - 1 - i] = temp;
                }
            }
            elapsedTime = 0f;
        }
    }
}
