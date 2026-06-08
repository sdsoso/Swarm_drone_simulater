using UnityEngine;

public class USVExperimentTracker : MonoBehaviour
{
    [Header("Failure Detection")]
    public Transform friendlyShipTarget;
    public string friendlyShipTag = "Ship";
    public bool useShipTagFallback = true;

    private bool isCounted;

    public void Initialize(Transform shipTarget)
    {
        friendlyShipTarget = shipTarget;
        ExperimentResultManager.GetOrCreate().RegisterUSV(gameObject);
    }

    private void Start()
    {
        ExperimentResultManager.GetOrCreate().RegisterUSV(gameObject);
    }

    public void MarkIntercepted()
    {
        if (isCounted)
            return;

        isCounted = true;
        ExperimentResultManager.GetOrCreate().ReportIntercepted(gameObject);
    }

    public void MarkFailed()
    {
        if (isCounted)
            return;

        isCounted = true;
        ExperimentResultManager.GetOrCreate().ReportFailed(gameObject);
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

        if (friendlyShipTarget != null && (other.transform == friendlyShipTarget || otherRoot == friendlyShipTarget || other.transform.IsChildOf(friendlyShipTarget)))
            return true;

        return useShipTagFallback && other.gameObject.tag == friendlyShipTag;
    }
}
