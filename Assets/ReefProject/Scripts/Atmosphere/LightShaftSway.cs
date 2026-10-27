using UnityEngine;

namespace Reef
{
    /// <summary>Slowly sways a light shaft, like sunlight through a moving surface.</summary>
    public class LightShaftSway : MonoBehaviour
    {
        public float swayDegrees = 2.5f;
        public float period = 9f;

        Quaternion baseRotation;
        float phase;

        void Start()
        {
            baseRotation = transform.localRotation;
            phase = Random.value * 100f;
        }

        void Update()
        {
            float t = (Time.time + phase) * 2f * Mathf.PI / Mathf.Max(0.5f, period);
            transform.localRotation = baseRotation * Quaternion.Euler(Mathf.Sin(t) * swayDegrees, 0f, Mathf.Sin(t * 0.7f + 1.3f) * swayDegrees);
        }
    }
}
