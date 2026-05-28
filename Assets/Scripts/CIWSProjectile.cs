using UnityEngine;

public class CIWSProjectile : MonoBehaviour
{
    [Header("Runtime State")]
    public float speed = 120f;
    public float maxDistance = 60f;
    public float lifeTime = 3f;
    public bool destroyTargetOnHit = true;

    [Header("Target Filtering")]
    public string usvTag = "USV";
    public bool useTagFilter = true;
    public LayerMask targetLayerMask = ~0;

    [Header("Debug")]
    public bool drawDebugPath = false;
    public Color debugPathColor = Color.yellow;

    private Vector3 direction = Vector3.forward;
    private Vector3 startPosition;
    private float spawnTime;
    private bool initialized;

    public void Initialize(
        Vector3 fireDirection,
        float projectileSpeed,
        float projectileMaxDistance,
        float projectileLifeTime,
        LayerMask projectileTargetMask,
        string projectileTargetTag,
        bool projectileUseTagFilter,
        bool projectileDestroyTargetOnHit)
    {
        direction = fireDirection.sqrMagnitude > 0.001f ? fireDirection.normalized : transform.forward;
        speed = projectileSpeed;
        maxDistance = projectileMaxDistance;
        lifeTime = projectileLifeTime;
        targetLayerMask = projectileTargetMask;
        usvTag = projectileTargetTag;
        useTagFilter = projectileUseTagFilter;
        destroyTargetOnHit = projectileDestroyTargetOnHit;

        startPosition = transform.position;
        spawnTime = Time.time;
        initialized = true;

        if (direction.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
    }

    private void Start()
    {
        if (initialized)
            return;

        Initialize(transform.forward, speed, maxDistance, lifeTime, targetLayerMask, usvTag, useTagFilter, destroyTargetOnHit);
    }

    private void Update()
    {
        float stepDistance = speed * Time.deltaTime;
        Vector3 currentPosition = transform.position;
        Vector3 nextPosition = currentPosition + direction * stepDistance;

        if (Physics.Raycast(currentPosition, direction, out RaycastHit hit, stepDistance, targetLayerMask, QueryTriggerInteraction.Collide))
        {
            HitCollider(hit.collider);
            return;
        }

        if (drawDebugPath)
            Debug.DrawLine(currentPosition, nextPosition, debugPathColor, Time.deltaTime);

        transform.position = nextPosition;

        if (Vector3.Distance(startPosition, transform.position) >= maxDistance || Time.time >= spawnTime + lifeTime)
            Destroy(gameObject);
    }

    private void OnTriggerEnter(Collider other)
    {
        HitCollider(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        HitCollider(collision.collider);
    }

    private void HitCollider(Collider hitCollider)
    {
        Transform target = GetValidTargetTransform(hitCollider);
        if (target == null)
            return;

        if (destroyTargetOnHit)
            Destroy(target.gameObject);

        Destroy(gameObject);
    }

    private Transform GetValidTargetTransform(Collider hitCollider)
    {
        if (!IsLayerInMask(hitCollider.gameObject.layer, targetLayerMask))
            return null;

        Transform target = hitCollider.attachedRigidbody != null
            ? hitCollider.attachedRigidbody.transform
            : hitCollider.transform.root;

        if (!IsUSVTarget(hitCollider, target))
            return null;

        return target;
    }

    private bool IsUSVTarget(Collider hitCollider, Transform target)
    {
        if (!useTagFilter)
            return true;

        if (hitCollider.gameObject.tag == usvTag)
            return true;

        return target != null && target.gameObject.tag == usvTag;
    }

    private bool IsLayerInMask(int layer, LayerMask layerMask)
    {
        return (layerMask.value & (1 << layer)) != 0;
    }
}
