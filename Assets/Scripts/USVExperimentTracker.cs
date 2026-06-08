using UnityEngine;

public class USVExperimentTracker : MonoBehaviour
{
    [Header("Failure Detection")]
    public Transform friendlyShipTarget;
    public string friendlyShipTag = "Ship";
    public bool useShipTagFallback = true;
    public bool registerWithExperimentManager = true;
    public bool destroyOnFailure = true;
    public float destroyDelay = 0f;

    private bool isCounted;
    private ExperimentResultManager resultManager;

    public void Initialize(Transform shipTarget)
    {
        Initialize(shipTarget, ExperimentResultManager.FindActiveManager());
    }

    public void Initialize(Transform shipTarget, ExperimentResultManager manager)
    {
        friendlyShipTarget = shipTarget;
        resultManager = manager;

        if (registerWithExperimentManager && resultManager != null)
            resultManager.RegisterUSV(gameObject);
    }

    private void Start()
    {
        if (resultManager == null)
            resultManager = ExperimentResultManager.FindActiveManager();

        if (registerWithExperimentManager && resultManager != null)
            resultManager.RegisterUSV(gameObject);
    }

    public void MarkIntercepted()
    {
        if (isCounted)
            return;

        isCounted = true;
        if (registerWithExperimentManager && resultManager != null)
            resultManager.ReportIntercepted(gameObject);
    }

    public void MarkFailed()
    {
        if (isCounted)
            return;

        isCounted = true;

        // Report the failure before destroying the USV so the result manager can
        // still use this GameObject instance ID for duplicate-count protection.
        if (registerWithExperimentManager && resultManager != null)
            resultManager.ReportFailed(gameObject);

        if (destroyOnFailure)
            Destroy(gameObject, Mathf.Max(0f, destroyDelay));
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (IsFriendlyShip(collision.collider))
            MarkFailed();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsFriendlyShip(other))
            MarkFailed();
    }

    private bool IsFriendlyShip(Collider other)
    {
        // Prefer the exact ship Transform from the spawner; tag fallback supports manual scene setup.
        if (other == null)
            return false;

        Transform otherRoot = other.attachedRigidbody != null ? other.attachedRigidbody.transform : other.transform.root;

        if (friendlyShipTarget != null &&
            (other.transform == friendlyShipTarget ||
             otherRoot == friendlyShipTarget ||
             other.transform.IsChildOf(friendlyShipTarget) ||
             (otherRoot != null && otherRoot.IsChildOf(friendlyShipTarget))))
        {
            return true;
        }

        if (!useShipTagFallback)
            return false;

        if (other.gameObject.tag == friendlyShipTag)
            return true;

        return otherRoot != null && otherRoot.gameObject.tag == friendlyShipTag;
    }
}
