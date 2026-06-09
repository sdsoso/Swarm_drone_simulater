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
    public bool showDebugLog = false;

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

        RegisterWithManagerIfPossible();
    }

    private void Start()
    {
        RegisterWithManagerIfPossible();
    }

    public void MarkIntercepted()
    {
        if (isCounted)
            return;

        isCounted = true;
        if (registerWithExperimentManager && EnsureResultManager())
            resultManager.ReportIntercepted(gameObject);
    }

    public void MarkFailed()
    {
        if (isCounted)
            return;

        isCounted = true;

        // Report the failure before destroying the USV so the result manager can
        // still use this GameObject instance ID for duplicate-count protection.
        if (registerWithExperimentManager && EnsureResultManager())
        {
            resultManager.ReportFailed(gameObject);

            if (showDebugLog)
                Debug.Log($"USV failed by ship collision: {name}", this);
        }
        else if (showDebugLog)
        {
            Debug.LogWarning($"USV collided with ship but no ExperimentResultManager is available or registration is disabled: {name}", this);
        }

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

    private void RegisterWithManagerIfPossible()
    {
        if (!registerWithExperimentManager)
            return;

        if (!EnsureResultManager())
        {
            if (showDebugLog)
                Debug.LogWarning($"USVExperimentTracker could not find ExperimentResultManager for {name}.", this);

            return;
        }

        resultManager.RegisterUSV(gameObject);
    }

    private bool EnsureResultManager()
    {
        if (resultManager != null)
            return true;

        resultManager = ExperimentResultManager.FindActiveManager();
        return resultManager != null;
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
