using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class USVController : MonoBehaviour
{
    [Header("Target")]
    public Transform target;
    public float stopDistance = 2f;

    [Header("Movement")]
    public float moveSpeed = 6f;
    public float acceleration = 12f;
    public float turnSpeed = 120f;
    public bool faceTargetWhileMoving = true;

    [Header("Floating")]
    public bool useWaveFloating = true;
    public bool preferExistingWaterFloat = true;
    public float waterHeightOffset = 0f;
    public float floatHeightSmooth = 8f;
    public float floatRotationSmooth = 4f;
    public Vector3 frontProbeOffset = new Vector3(0f, 0f, 2f);
    public Vector3 backProbeOffset = new Vector3(0f, 0f, -2f);
    public Vector3 leftProbeOffset = new Vector3(-1.5f, 0f, 0f);
    public Vector3 rightProbeOffset = new Vector3(1.5f, 0f, 0f);

    [Header("Debug")]
    public bool drawDebugGizmos = true;
    public Color targetLineColor = Color.red;
    public Color floatProbeColor = Color.cyan;

    private Rigidbody rb;
    private Waves waves;
    private WaterFloat waterFloat;
    private Vector3 desiredHorizontalVelocity;
    private Vector3 desiredForward;

    public void SetTarget(Transform newTarget)
    {
        target = newTarget;
    }

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        waves = FindObjectOfType<Waves>();
        waterFloat = GetComponent<WaterFloat>();

        rb.useGravity = false;
        desiredForward = transform.forward;
    }

    private void FixedUpdate()
    {
        MoveTowardTarget();
        ApplyWaveFloating();
    }

    private void MoveTowardTarget()
    {
        if (target == null)
        {
            desiredHorizontalVelocity = Vector3.zero;
            desiredForward = transform.forward;
            ApplyHorizontalVelocity();
            return;
        }

        Vector3 toTarget = target.position - rb.position;
        toTarget.y = 0f;

        if (toTarget.magnitude <= stopDistance)
        {
            desiredHorizontalVelocity = Vector3.zero;
            desiredForward = transform.forward;
            ApplyHorizontalVelocity();
            return;
        }

        Vector3 moveDirection = toTarget.normalized;
        desiredHorizontalVelocity = moveDirection * moveSpeed;
        if (faceTargetWhileMoving)
            desiredForward = moveDirection;
        else
            desiredForward = transform.forward;

        ApplyHorizontalVelocity();

        if (faceTargetWhileMoving && moveDirection.sqrMagnitude > 0.001f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection, transform.up);
            Quaternion yawRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(yawRotation);
        }
    }

    private void ApplyHorizontalVelocity()
    {
        Vector3 currentVelocity = rb.linearVelocity;
        Vector3 currentHorizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        Vector3 smoothedHorizontalVelocity = Vector3.MoveTowards(
            currentHorizontalVelocity,
            desiredHorizontalVelocity,
            acceleration * Time.fixedDeltaTime);

        rb.linearVelocity = new Vector3(smoothedHorizontalVelocity.x, currentVelocity.y, smoothedHorizontalVelocity.z);
    }

    private void ApplyWaveFloating()
    {
        if (!useWaveFloating || !CanSampleWaves())
            return;

        if (preferExistingWaterFloat && waterFloat != null && waterFloat.enabled)
            return;

        Vector3 position = rb.position;
        float waterHeight = waves.GetHeight(position) + waterHeightOffset;
        position.y = Mathf.Lerp(position.y, waterHeight, floatHeightSmooth * Time.fixedDeltaTime);
        rb.MovePosition(position);

        Vector3 front = GetWaterPoint(frontProbeOffset);
        Vector3 back = GetWaterPoint(backProbeOffset);
        Vector3 left = GetWaterPoint(leftProbeOffset);
        Vector3 right = GetWaterPoint(rightProbeOffset);

        Vector3 forwardSlope = (front - back).normalized;
        Vector3 rightSlope = (right - left).normalized;
        Vector3 waterNormal = Vector3.Cross(forwardSlope, rightSlope).normalized;

        if (waterNormal.y < 0f)
            waterNormal = -waterNormal;

        Vector3 forwardOnWater = Vector3.ProjectOnPlane(desiredForward, waterNormal).normalized;

        if (forwardOnWater.sqrMagnitude < 0.001f || waterNormal.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(forwardOnWater, waterNormal);
        Quaternion smoothedRotation = Quaternion.Slerp(rb.rotation, targetRotation, floatRotationSmooth * Time.fixedDeltaTime);
        rb.MoveRotation(smoothedRotation);
    }

    private bool CanSampleWaves()
    {
        if (waves == null)
            return false;

        MeshFilter meshFilter = waves.GetComponent<MeshFilter>();
        return meshFilter != null && meshFilter.sharedMesh != null;
    }

    private Vector3 GetWaterPoint(Vector3 localOffset)
    {
        Vector3 worldPoint = transform.TransformPoint(localOffset);
        worldPoint.y = waves.GetHeight(worldPoint) + waterHeightOffset;
        return worldPoint;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawDebugGizmos)
            return;

        if (target != null)
        {
            Gizmos.color = targetLineColor;
            Gizmos.DrawLine(transform.position, target.position);
        }

        Gizmos.color = floatProbeColor;
        DrawProbe(frontProbeOffset);
        DrawProbe(backProbeOffset);
        DrawProbe(leftProbeOffset);
        DrawProbe(rightProbeOffset);
    }

    private void DrawProbe(Vector3 localOffset)
    {
        Gizmos.DrawWireSphere(transform.TransformPoint(localOffset), 0.25f);
    }
}
