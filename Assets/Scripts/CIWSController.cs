using UnityEngine;

public class CIWSController : MonoBehaviour
{
    [Header("Target Filtering")]
    public string usvTag = "USV";
    public bool useTagFilter = true;
    public LayerMask usvLayerMask = ~0;

    [Header("Ranges")]
    public float detectRange = 80f;
    public float fireRange = 60f;

    [Header("Firing")]
    public float fireRate = 5f;
    public bool destroyTargetOnHit = true;

    [Header("Turret References")]
    public Transform turretHead;
    public Transform firePoint;

    [Header("Rotation")]
    public float rotateSpeed = 180f;
    public bool yawOnlyRotation = false;

    [Header("Debug")]
    public bool drawGizmos = true;
    public Color detectRangeColor = new Color(0f, 1f, 1f, 0.25f);
    public Color fireRangeColor = new Color(1f, 0f, 0f, 0.25f);
    public Color aimLineColor = Color.red;
    public float debugFireLineDuration = 0.1f;

    private Transform currentTarget;
    private float nextFireTime;
    private readonly Collider[] detectedColliders = new Collider[64];

    private void Reset()
    {
        turretHead = transform;
        firePoint = transform;
    }

    private void Awake()
    {
        if (turretHead == null)
            turretHead = transform;

        if (firePoint == null)
            firePoint = turretHead;
    }

    private void Update()
    {
        currentTarget = FindClosestTarget();

        if (currentTarget == null)
            return;

        RotateTurretToward(currentTarget);

        if (IsTargetInFireRange(currentTarget) && Time.time >= nextFireTime)
        {
            FireAt(currentTarget);
            nextFireTime = Time.time + GetFireInterval();
        }
    }

    private Transform FindClosestTarget()
    {
        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            detectRange,
            detectedColliders,
            usvLayerMask,
            QueryTriggerInteraction.Ignore);

        Transform closestTarget = null;
        float closestDistanceSqr = float.MaxValue;

        for (int i = 0; i < hitCount; i++)
        {
            Collider detectedCollider = detectedColliders[i];
            if (detectedCollider == null)
                continue;

            Transform target = GetValidTargetTransform(detectedCollider);
            if (target == null)
                continue;

            float distanceSqr = (target.position - transform.position).sqrMagnitude;
            if (distanceSqr < closestDistanceSqr)
            {
                closestDistanceSqr = distanceSqr;
                closestTarget = target;
            }
        }

        return closestTarget;
    }

    private Transform GetValidTargetTransform(Collider detectedCollider)
    {
        Transform target = detectedCollider.attachedRigidbody != null
            ? detectedCollider.attachedRigidbody.transform
            : detectedCollider.transform.root;

        if (!IsUSVTarget(detectedCollider, target))
            return null;

        return target;
    }

    private bool IsUSVTarget(Collider detectedCollider, Transform target)
    {
        if (!useTagFilter)
            return true;

        if (detectedCollider.gameObject.tag == usvTag)
            return true;

        return target != null && target.gameObject.tag == usvTag;
    }

    private void RotateTurretToward(Transform target)
    {
        Vector3 aimDirection = GetAimPoint(target) - turretHead.position;

        if (yawOnlyRotation)
            aimDirection.y = 0f;

        if (aimDirection.sqrMagnitude < 0.001f)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(aimDirection.normalized, Vector3.up);
        turretHead.rotation = Quaternion.RotateTowards(
            turretHead.rotation,
            targetRotation,
            rotateSpeed * Time.deltaTime);
    }

    private bool IsTargetInFireRange(Transform target)
    {
        return (GetAimPoint(target) - firePoint.position).sqrMagnitude <= fireRange * fireRange;
    }

    private void FireAt(Transform target)
    {
        Vector3 origin = firePoint.position;
        Vector3 direction = (GetAimPoint(target) - origin).normalized;

        Debug.DrawRay(origin, direction * fireRange, aimLineColor, debugFireLineDuration);

        RaycastHit[] hits = Physics.RaycastAll(origin, direction, fireRange, usvLayerMask, QueryTriggerInteraction.Ignore);
        Transform hitTarget = GetClosestValidHit(hits);

        if (hitTarget != null && destroyTargetOnHit)
            Destroy(hitTarget.gameObject);
    }

    private Transform GetClosestValidHit(RaycastHit[] hits)
    {
        Transform closestTarget = null;
        float closestDistance = float.MaxValue;

        for (int i = 0; i < hits.Length; i++)
        {
            Transform hitTarget = GetValidTargetTransform(hits[i].collider);
            if (hitTarget == null)
                continue;

            if (hits[i].distance < closestDistance)
            {
                closestDistance = hits[i].distance;
                closestTarget = hitTarget;
            }
        }

        return closestTarget;
    }

    private Vector3 GetAimPoint(Transform target)
    {
        Collider targetCollider = target.GetComponentInChildren<Collider>();
        if (targetCollider != null)
            return targetCollider.bounds.center;

        return target.position;
    }

    private float GetFireInterval()
    {
        return fireRate > 0f ? 1f / fireRate : float.MaxValue;
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos)
            return;

        Gizmos.color = detectRangeColor;
        Gizmos.DrawWireSphere(transform.position, detectRange);

        Gizmos.color = fireRangeColor;
        Gizmos.DrawWireSphere(transform.position, fireRange);

        if (currentTarget != null)
        {
            Transform origin = firePoint != null ? firePoint : transform;
            Gizmos.color = aimLineColor;
            Gizmos.DrawLine(origin.position, GetAimPoint(currentTarget));
        }
    }
}
