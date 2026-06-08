using System.Collections.Generic;
using UnityEngine;

public class CIWSController : MonoBehaviour
{
    [System.Serializable]
    public class ThreatTarget
    {
        public Transform target;
        public Rigidbody rb;
        public float distance;
        public float closingSpeed;
        public float tti;
        public float alignment;
        public float threatScore;
    }

    [Header("Target Filtering")]
    public string usvTag = "USV";
    public bool useTagFilter = true;
    public LayerMask usvLayerMask = ~0;

    [Header("Threat Selection")]
    public Transform defendedShip;
    public float ttiWeight = 0.7f;
    public float alignmentWeight = 0.3f;
    public float epsilon = 0.01f;
    public bool useThreatPriorityQueue = true;
    public bool showThreatDebugLog = false;

    [Header("Ranges")]
    public float detectRange = 80f;
    public float fireRange = 60f;

    [Header("Firing")]
    public float fireRate = 5f;
    public bool destroyTargetOnHit = true;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public float projectileSpeed = 120f;
    public float projectileLifeTime = 3f;
    public float projectileSize = 0.25f;

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
    private ThreatTarget currentThreat;
    private float nextFireTime;
    private readonly Collider[] detectedColliders = new Collider[64];
    private readonly List<ThreatTarget> threatQueue = new List<ThreatTarget>(64);
    private readonly List<Transform> uniqueTargets = new List<Transform>(64);

    private void Reset()
    {
        turretHead = transform;
        firePoint = transform;
        defendedShip = transform.root;
    }

    private void Awake()
    {
        if (turretHead == null)
            turretHead = transform;

        if (firePoint == null)
            firePoint = turretHead;

        if (defendedShip == null)
            defendedShip = transform.root;

        ExperimentResultManager manager = ExperimentResultManager.FindActiveManager();
        if (manager != null)
            manager.SetThreatWeights(ttiWeight, alignmentWeight);
    }

    private void Update()
    {
        RefreshThreatQueue();
        currentThreat = SelectTargetFromThreatQueue();
        currentTarget = currentThreat != null ? currentThreat.target : null;

        if (currentTarget == null)
            return;

        RotateTurretToward(currentTarget);

        if (IsTargetInFireRange(currentTarget) && Time.time >= nextFireTime)
        {
            FireAt(currentTarget);
            nextFireTime = Time.time + GetFireInterval();
        }
    }

    private void RefreshThreatQueue()
    {
        // Build a lightweight priority queue every frame from currently detected USVs.
        // List sorting is used instead of PriorityQueue for broad Unity/C# compatibility.
        threatQueue.Clear();
        uniqueTargets.Clear();

        int hitCount = Physics.OverlapSphereNonAlloc(
            transform.position,
            detectRange,
            detectedColliders,
            usvLayerMask,
            QueryTriggerInteraction.Ignore);

        for (int i = 0; i < hitCount; i++)
        {
            Collider detectedCollider = detectedColliders[i];
            if (!IsValidTarget(detectedCollider, out Transform target, out Rigidbody targetRb))
                continue;

            if (uniqueTargets.Contains(target))
                continue;

            uniqueTargets.Add(target);

            ThreatTarget threat = new ThreatTarget
            {
                target = target,
                rb = targetRb
            };

            // Threat metrics are measured relative to the defended ship, not just the turret.
            threat.distance = defendedShip != null
                ? Vector3.Distance(target.position, defendedShip.position)
                : Vector3.Distance(target.position, transform.position);
            threat.closingSpeed = CalculateClosingSpeed(target, targetRb);
            threat.tti = CalculateTTI(threat.distance, threat.closingSpeed);
            threat.alignment = CalculateAlignment(target, targetRb);
            threat.threatScore = CalculateThreatScore(threat.tti, threat.alignment, threat.closingSpeed);

            threatQueue.Add(threat);

            if (showThreatDebugLog)
            {
                Debug.Log(
                    $"[CIWS Threat] target={target.name}, distance={threat.distance:F2}, " +
                    $"closingSpeed={threat.closingSpeed:F2}, TTI={threat.tti:F2}, " +
                    $"alignment={threat.alignment:F2}, threatScore={threat.threatScore:F3}", this);
            }
        }

        if (useThreatPriorityQueue)
            threatQueue.Sort((a, b) => b.threatScore.CompareTo(a.threatScore));
        else
            threatQueue.Sort((a, b) => a.distance.CompareTo(b.distance));
    }

    private ThreatTarget SelectTargetFromThreatQueue()
    {
        // Only fire-range targets can be engaged; detect-range targets stay in the queue
        // until they become close enough to shoot.
        for (int i = 0; i < threatQueue.Count; i++)
        {
            ThreatTarget threat = threatQueue[i];
            if (!IsTargetInFireRange(threat))
                continue;

            if (showThreatDebugLog)
                Debug.Log($"[CIWS Selected] target={threat.target.name}, threatScore={threat.threatScore:F3}", this);

            return threat;
        }

        return null;
    }

    private float CalculateThreatScore(float tti, float alignment, float closingSpeed)
    {
        // ThreatScore = ttiWeight * (1 / TTI) + alignmentWeight * Alignment.
        // Non-closing targets are deliberately pushed to the bottom of the queue.
        if (closingSpeed <= epsilon)
            return -1f + alignmentWeight * alignment;

        return ttiWeight * (1f / Mathf.Max(tti, epsilon)) + alignmentWeight * alignment;
    }

    private float CalculateTTI(float distance, float closingSpeed)
    {
        return distance / Mathf.Max(closingSpeed, epsilon);
    }

    private float CalculateAlignment(Transform target, Rigidbody targetRb)
    {
        if (target == null || defendedShip == null)
            return 0f;

        Vector3 velocity = targetRb != null ? targetRb.linearVelocity : Vector3.zero;
        velocity.y = 0f;

        Vector3 moveDirection = velocity.sqrMagnitude > 0.01f ? velocity.normalized : target.forward;
        moveDirection.y = 0f;

        Vector3 toShip = defendedShip.position - target.position;
        toShip.y = 0f;

        if (moveDirection.sqrMagnitude < 0.001f || toShip.sqrMagnitude < 0.001f)
            return 0f;

        return Vector3.Dot(moveDirection.normalized, toShip.normalized);
    }

    private float CalculateClosingSpeed(Transform target, Rigidbody targetRb)
    {
        // Closing speed is the velocity component along the target-to-ship vector.
        // This avoids treating fast side-slipping USVs as high TTI threats.
        if (target == null || targetRb == null || defendedShip == null)
            return 0f;

        Vector3 toShip = defendedShip.position - target.position;
        toShip.y = 0f;

        if (toShip.sqrMagnitude < 0.001f)
            return 0f;

        Vector3 velocity = targetRb.linearVelocity;
        velocity.y = 0f;

        return Vector3.Dot(velocity, toShip.normalized);
    }

    private bool IsTargetInFireRange(ThreatTarget threat)
    {
        return threat != null && IsTargetInFireRange(threat.target);
    }

    private bool IsTargetInFireRange(Transform target)
    {
        if (target == null || firePoint == null)
            return false;

        return (GetAimPoint(target) - firePoint.position).sqrMagnitude <= fireRange * fireRange;
    }

    private bool IsValidTarget(Collider detectedCollider, out Transform target, out Rigidbody targetRb)
    {
        target = null;
        targetRb = null;

        if (detectedCollider == null)
            return false;

        targetRb = detectedCollider.attachedRigidbody;
        target = targetRb != null ? targetRb.transform : detectedCollider.transform.root;

        if (target == null)
            return false;

        if (useTagFilter && detectedCollider.gameObject.tag != usvTag && target.gameObject.tag != usvTag)
            return false;

        return true;
    }

    private void RotateTurretToward(Transform target)
    {
        if (target == null || turretHead == null)
            return;

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

    private void FireAt(Transform target)
    {
        if (target == null || firePoint == null)
            return;

        Vector3 origin = firePoint.position;
        Vector3 direction = (GetAimPoint(target) - origin).normalized;

        Debug.DrawRay(origin, direction * fireRange, aimLineColor, debugFireLineDuration);
        SpawnProjectile(origin, direction);
    }

    private void SpawnProjectile(Vector3 origin, Vector3 direction)
    {
        GameObject projectile = projectilePrefab != null
            ? Instantiate(projectilePrefab, origin, Quaternion.LookRotation(direction, Vector3.up))
            : CreateDefaultProjectile(origin, direction);

        CIWSProjectile ciwsProjectile = projectile.GetComponent<CIWSProjectile>();
        if (ciwsProjectile == null)
            ciwsProjectile = projectile.AddComponent<CIWSProjectile>();

        ciwsProjectile.Initialize(
            direction,
            projectileSpeed,
            fireRange,
            projectileLifeTime,
            usvLayerMask,
            usvTag,
            useTagFilter,
            destroyTargetOnHit);
    }

    private GameObject CreateDefaultProjectile(Vector3 origin, Vector3 direction)
    {
        GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        projectile.name = "CIWS_Projectile";
        projectile.transform.position = origin;
        projectile.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        projectile.transform.localScale = Vector3.one * projectileSize;

        Collider projectileCollider = projectile.GetComponent<Collider>();
        if (projectileCollider != null)
            projectileCollider.isTrigger = true;

        return projectile;
    }

    private Vector3 GetAimPoint(Transform target)
    {
        if (target == null)
            return transform.position;

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
