using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Small, absolute mouse-position look around the authored desk view.</summary>
[DisallowMultipleComponent]
public sealed class DeskMouseLook : MonoBehaviour
{
    [Tooltip("Maximum horizontal and vertical rotation from the starting view, in degrees.")]
    public Vector2 angleLimits = new Vector2(.85f, .5f);

    [Tooltip("Central dead-zone width and height as fractions of the game viewport.")]
    public Vector2 deadZone = Vector2.zero;

    [Min(0.01f)] public float smoothTime = 0.06f;
    public bool lookEnabled = true;
    public bool holdLook;
    float impactUntil;
    public void StampImpact(){impactUntil=Time.unscaledTime+.12f;}

    private Quaternion startingRotation;
    private Vector2 angles;
    private Vector2 targetAngles;
    private Vector2 velocity;
    private Camera viewCamera;

    private void OnEnable()
    {
        startingRotation = transform.localRotation;
        angles = targetAngles = velocity = Vector2.zero;
        viewCamera = GetComponent<Camera>();
        if (viewCamera == null) viewCamera = Camera.main;
    }

    private void Update()
    {
        if (viewCamera == null) return;
        if (holdLook && lookEnabled) return;
        targetAngles = Vector2.zero;
        if (lookEnabled && Application.isFocused && Mouse.current != null)
        {
            Rect viewport = viewCamera.pixelRect;
            Vector2 mouse = Mouse.current.position.ReadValue();
            if (viewport.width > 0 && viewport.height > 0 && viewport.Contains(mouse))
            {
                Vector2 position = new Vector2(
                    (mouse.x - viewport.xMin) / viewport.width * 2f - 1f,
                    (mouse.y - viewport.yMin) / viewport.height * 2f - 1f);
                targetAngles = Vector2.Scale(position, angleLimits);
            }
        }

        angles.x = Mathf.SmoothDamp(angles.x, targetAngles.x, ref velocity.x, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        angles.y = Mathf.SmoothDamp(angles.y, targetAngles.y, ref velocity.y, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
        float remaining=Mathf.Clamp01((impactUntil-Time.unscaledTime)/.12f);
        float impact=lookEnabled?Mathf.Sin(Time.unscaledTime*120)*remaining*.12f:0;
        transform.localRotation = startingRotation * Quaternion.Euler(-angles.y+impact, angles.x, 0f);
    }

    public static float MapOutsideDeadZone(float position, float zone)
    {
        zone = Mathf.Clamp(zone, 0f, 0.95f);
        float amount = Mathf.Clamp01((Mathf.Abs(position) - zone) / (1f - zone));
        // Ease the response at the dead-zone boundary instead of abruptly starting.
        return Mathf.Sign(position) * Mathf.SmoothStep(0f, 1f, amount);
    }

    private void OnDisable()
    {
        transform.localRotation = startingRotation;
    }

    private void OnValidate()
    {
        angleLimits = new Vector2(Mathf.Clamp(angleLimits.x, 0f, 15f), Mathf.Clamp(angleLimits.y, 0f, 15f));
        deadZone = new Vector2(Mathf.Clamp(deadZone.x, 0f, 0.95f), Mathf.Clamp(deadZone.y, 0f, 0.95f));
        smoothTime = Mathf.Max(0.01f, smoothTime);
    }
}
