using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vertigo2;

/// <summary>
/// Adds ripples to the water wave simulation
/// </summary>
public class WaterDynamicFlowSource : MonoBehaviour
{
    public bool flowOnStart = true;

    [Tooltip("Set to zero for permanent")]
    public float duration = 0;

    public float positionNoise = 0;
    public float noiseSpeed = 10;

    public float radius = 0.25f;

    public float amplitude = 1f;

    public int priority = 0;

    Coroutine flowRoutine;


    private void OnEnable()
    {
        if (flowOnStart) Flow();
    }

    public void Flow()
    {
        if (flowRoutine != null) StopCoroutine(flowRoutine);

        flowRoutine = StartCoroutine(DoFlow());
    }


    public void StopFlow()
    {
        if (flowRoutine != null) StopCoroutine(flowRoutine);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.DrawWireSphere(transform.position, radius);
    }

    IEnumerator DoFlow()
    {
        yield return null; // wait a frame for safety

        if (duration <= 0)
        {
            // add flow indefinitely
            while (this.enabled)
            {
                AddFlow();
                yield return null;
            }
        }
        else
        { 
            // add flow for <duration>
            float t = 0;
            while (t < duration)
            {
                AddFlow();
                t += Time.deltaTime;
                yield return null;
            }
        }
    }

    void AddFlow()
    {
        if (VWaterDynamicsManager.instance.IsPositionInUniverse(transform.position))  // don't add this flow source if it's outside universe
        {
            VWaterDynamicsManager.instance.AddFlow(GetPosition(), radius, amplitude, priority);
        }
    }

    Vector3 GetPosition()
    {
        if(positionNoise == 0)
        {
            return transform.position;
        }
        else
        {
            Vector2 r = new Vector2(Mathf.PerlinNoise(-12, Time.time * noiseSpeed + 3) - 0.5f,
                                    Mathf.PerlinNoise(54, Time.time * noiseSpeed - 10) - 0.5f) * positionNoise;
            return transform.position + new Vector3(r.x, 0, r.y);
        }
    }
}
