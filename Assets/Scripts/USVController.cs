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

    [Header("Swarm Evasive Maneuver")]
    [Tooltip("Side-to-side zig-zag strength while approaching the target.")]
    public float evasiveStrength = 0.65f;
    [Tooltip("How many times per second the USV chooses a new evasive side force.")]
    public float evasiveFrequency = 0.6f;
    [Tooltip("Distance from the target where evasive motion starts fading into a straight final charge.")]
    public float finalChargeDistance = 20f;
    [Tooltip("How fast the current evasive offset blends toward the newly chosen random offset.")]
    public float evasiveBlendSpeed = 2.5f;

    [Header("Swarm Separation")]
    [Tooltip("USVs closer than this distance push away from each other.")]
    public float separationDistance = 5f;
    [Tooltip("Strength of the local separation force used to avoid overlap in the swarm.")]
    public float separationForce = 1.2f;
    [Tooltip("Layer mask used when searching nearby USVs for separation.")]
    public LayerMask separationLayerMask = ~0;
    [Tooltip("Optional tag check for nearby USVs. Disable this if you want Layer-only separation.")]
    public bool useSeparationTagFilter = true;
    public string usvTag = "USV";

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
    public Color separationColor = new Color(1f, 0.5f, 0f, 0.25f);
    public Color desiredDirectionColor = Color.magenta;

    private const int MaxNearbyUSVs = 32;

    private Rigidbody rb;
    private Waves waves;
    private WaterFloat waterFloat;
    private Vector3 desiredHorizontalVelocity;
    private Vector3 desiredForward;
    private Vector3 lastDesiredMoveDirection;
    private float targetEvasiveOffset;
    private float currentEvasiveOffset;
    private float nextEvasiveChangeTime;
    private readonly Collider[] separationHits = new Collider[MaxNearbyUSVs];

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
        lastDesiredMoveDirection = transform.forward;
        PickNewEvasiveOffset();
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
        float distanceToTarget = toTarget.magnitude;

        if (distanceToTarget <= stopDistance)
        {
            desiredHorizontalVelocity = Vector3.zero;
            desiredForward = transform.forward;
            ApplyHorizontalVelocity();
            return;
        }

        Vector3 attackDirection = toTarget.normalized;
        Vector3 desiredMoveDirection = CalculateSwarmAttackDirection(attackDirection, distanceToTarget);

        desiredHorizontalVelocity = desiredMoveDirection * moveSpeed;
        lastDesiredMoveDirection = desiredMoveDirection;

        if (faceTargetWhileMoving)
            desiredForward = desiredMoveDirection;
        else
            desiredForward = transform.forward;

        ApplyHorizontalVelocity();
        RotateTowardDesiredDirection(desiredMoveDirection);
    }

    private Vector3 CalculateSwarmAttackDirection(Vector3 attackDirection, float distanceToTarget)
    {
        // Final-charge factor keeps the swarm aggressive: far away USVs zig-zag,
        // but near the ship they reduce lateral movement and commit to the target.
        float evasiveScale = Mathf.Clamp01((distanceToTarget - stopDistance) / Mathf.Max(finalChargeDistance - stopDistance, 0.01f));
        Vector3 lateralDirection = Vector3.Cross(Vector3.up, attackDirection).normalized;
        Vector3 evasiveDirection = lateralDirection * GetSmoothedEvasiveOffset() * evasiveStrength * evasiveScale;

        // Separation is local and cheap: only nearby colliders inside a small sphere are checked.
        // It prevents spawned USVs from stacking while preserving the main attack vector.
        Vector3 separationDirection = CalculateSeparationDirection() * separationForce;

        Vector3 desiredDirection = attackDirection + evasiveDirection + separationDirection;
        desiredDirection.y = 0f;

        if (desiredDirection.sqrMagnitude < 0.001f)
            return attackDirection;

        return desiredDirection.normalized;
    }

    private float GetSmoothedEvasiveOffset()
    {
        if (Time.time >= nextEvasiveChangeTime)
            PickNewEvasiveOffset();

        // SmoothDamp/MoveTowards style blending avoids instant snap-turns when the random side changes.
        currentEvasiveOffset = Mathf.MoveTowards(
            currentEvasiveOffset,
            targetEvasiveOffset,
            evasiveBlendSpeed * Time.fixedDeltaTime);

        return currentEvasiveOffset;
    }

    private void PickNewEvasiveOffset()
    {
        targetEvasiveOffset = Random.Range(-1f, 1f);
        float interval = evasiveFrequency > 0f ? 1f / evasiveFrequency : float.MaxValue;
        nextEvasiveChangeTime = Time.time + interval;
    }

    private Vector3 CalculateSeparationDirection()
    {
        if (separationDistance <= 0f || separationForce <= 0f)
            return Vector3.zero;

        int hitCount = Physics.OverlapSphereNonAlloc(
            rb.position,
            separationDistance,
            separationHits,
            separationLayerMask,
            QueryTriggerInteraction.Ignore);

        Vector3 separation = Vector3.zero;

        for (int i = 0; i < hitCount; i++)
        {
            Collider hit = separationHits[i];
            if (hit == null)
                continue;

            Transform otherUSV = GetUSVRoot(hit);
            if (otherUSV == null || otherUSV == transform)
                continue;

            Vector3 away = rb.position - otherUSV.position;
            away.y = 0f;
            float distance = away.magnitude;

            if (distance <= 0.001f || distance > separationDistance)
                continue;

            // Stronger push when another USV is very close, weaker near the edge of the radius.
            float strengthByDistance = 1f - (distance / separationDistance);
            separation += away.normalized * strengthByDistance;
        }

        return separation.sqrMagnitude > 0.001f ? separation.normalized : Vector3.zero;
    }

    private Transform GetUSVRoot(Collider hit)
    {
        Transform candidate = hit.attachedRigidbody != null ? hit.attachedRigidbody.transform : hit.transform.root;

        if (!useSeparationTagFilter)
            return candidate;

        if (hit.gameObject.tag == usvTag)
            return candidate;

        return candidate != null && candidate.gameObject.tag == usvTag ? candidate : null;
    }

    private void RotateTowardDesiredDirection(Vector3 desiredMoveDirection)
    {
        if (!faceTargetWhileMoving || desiredMoveDirection.sqrMagnitude <= 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(desiredMoveDirection, transform.up);
        Quaternion yawRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
        rb.MoveRotation(yawRotation);
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

        Gizmos.color = desiredDirectionColor;
        Gizmos.DrawRay(transform.position, lastDesiredMoveDirection * 4f);

        Gizmos.color = separationColor;
        Gizmos.DrawWireSphere(transform.position, separationDistance);

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
