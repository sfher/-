using UnityEngine;

public class CoopCamera : MonoBehaviour
{
    [SerializeField] Transform a;
    [SerializeField] Transform b;
    [SerializeField] float minSize = 5f;
    [SerializeField] float maxSize = 9f;
    [SerializeField] float sizePerWorldUnit = 0.42f;
    [SerializeField] float smoothing = 8f;
    [SerializeField] Vector3 offset = new Vector3(0f, 1f, 0f);

    Camera cam;

    void Awake() => cam = GetComponent<Camera>();

    void LateUpdate()
    {
        if (a == null || b == null || cam == null) return;

        float t = 1f - Mathf.Exp(-smoothing * Time.deltaTime);

        Vector3 mid = (a.position + b.position) * 0.5f;
        mid.z = transform.position.z;
        transform.position = Vector3.Lerp(transform.position, mid + offset, t);

        float spread = Mathf.Abs(a.position.x - b.position.x);
        float targetSize = Mathf.Clamp(minSize + spread * sizePerWorldUnit, minSize, maxSize);
        cam.orthographicSize = Mathf.Lerp(cam.orthographicSize, targetSize, t);
    }
}
