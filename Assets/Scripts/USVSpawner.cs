using System.Collections;
using UnityEngine;

public class USVSpawner : MonoBehaviour
{
    public enum SpawnMode
    {
        CircleEdge,
        CircleArea
    }

    [Header("References")]
    public GameObject usvPrefab;
    public Transform destroyerOrigin;
    public Transform shipTarget;
    public Transform spawnParent;

    [Header("Spawn Settings")]
    public float spawnRadius = 50f;
    public SpawnMode spawnMode = SpawnMode.CircleEdge;
    public int spawnCount = 3;
    public bool spawnOnStart = true;
    public bool autoSpawnRepeatedly = false;
    public float spawnInterval = 5f;
    public float spawnHeightOffset = 0f;
    public bool alignToShipTarget = true;
    public bool delayInitialSpawnUntilWavesReady = true;
    public bool registerSpawnedUSVsForExperiment = false;

    [Header("Experiment Results")]
    public ExperimentResultManager experimentResultManager;
    public bool autoFindExperimentResultManager = true;
    public bool createExperimentResultManagerIfMissing = false;

    [Header("Auto Find By Name")]
    public bool autoFindMissingReferences = true;
    public string destroyerObjectName = "Destroyer_01";
    public string shipObjectName = "Ship";

    [Header("Debug")]
    public bool drawSpawnRadius = true;
    public Color spawnRadiusColor = Color.yellow;

    private Waves waves;
    private float nextSpawnTime;

    private void Awake()
    {
        waves = FindObjectOfType<Waves>();

        if (autoFindMissingReferences)
            FindMissingReferences();

        if (autoFindExperimentResultManager)
            FindExperimentResultManager();
    }

    private IEnumerator Start()
    {
        if (delayInitialSpawnUntilWavesReady)
            yield return null;

        if (waves == null)
            waves = FindObjectOfType<Waves>();

        if (spawnOnStart)
            SpawnUSVs(spawnCount);

        nextSpawnTime = Time.time + spawnInterval;
    }

    private void Update()
    {
        if (!autoSpawnRepeatedly)
            return;

        if (Time.time < nextSpawnTime)
            return;

        SpawnUSV();
        nextSpawnTime = Time.time + spawnInterval;
    }

    public void SpawnUSVs(int count)
    {
        for (int i = 0; i < count; i++)
            SpawnUSV();
    }

    public USVController SpawnUSV()
    {
        if (usvPrefab == null)
        {
            Debug.LogWarning("USVSpawner needs a USV prefab before it can spawn.", this);
            return null;
        }

        Transform origin = destroyerOrigin != null ? destroyerOrigin : transform;
        Vector3 spawnPosition = GetRandomSpawnPosition(origin.position);
        Quaternion spawnRotation = GetSpawnRotation(spawnPosition);

        GameObject instance = Instantiate(usvPrefab, spawnPosition, spawnRotation, spawnParent);
        USVController controller = instance.GetComponent<USVController>();

        if (controller == null)
            controller = instance.AddComponent<USVController>();

        controller.SetTarget(shipTarget);

        if (registerSpawnedUSVsForExperiment)
            RegisterSpawnedUSV(instance);

        return controller;
    }

    private void RegisterSpawnedUSV(GameObject instance)
    {
        if (instance == null)
            return;

        if (experimentResultManager == null)
            FindExperimentResultManager();

        USVExperimentTracker tracker = instance.GetComponent<USVExperimentTracker>();
        if (tracker == null)
            tracker = instance.AddComponent<USVExperimentTracker>();

        tracker.Initialize(shipTarget, experimentResultManager);
    }

    private void FindExperimentResultManager()
    {
        experimentResultManager = ExperimentResultManager.FindActiveManager();

        if (experimentResultManager == null && createExperimentResultManagerIfMissing)
            experimentResultManager = ExperimentResultManager.GetOrCreate();
    }

    public Vector3 GetRandomSpawnPosition(Vector3 center)
    {
        Vector2 randomPoint = spawnMode == SpawnMode.CircleEdge
            ? Random.insideUnitCircle.normalized * spawnRadius
            : Random.insideUnitCircle * spawnRadius;

        if (randomPoint.sqrMagnitude < 0.001f)
            randomPoint = Vector2.right * spawnRadius;

        Vector3 spawnPosition = center + new Vector3(randomPoint.x, 0f, randomPoint.y);

        if (CanSampleWaves())
            spawnPosition.y = waves.GetHeight(spawnPosition) + spawnHeightOffset;
        else
            spawnPosition.y = center.y + spawnHeightOffset;

        return spawnPosition;
    }

    private bool CanSampleWaves()
    {
        if (waves == null)
            return false;

        MeshFilter meshFilter = waves.GetComponent<MeshFilter>();
        return meshFilter != null && meshFilter.sharedMesh != null;
    }

    private Quaternion GetSpawnRotation(Vector3 spawnPosition)
    {
        if (!alignToShipTarget || shipTarget == null)
            return Quaternion.identity;

        Vector3 toTarget = shipTarget.position - spawnPosition;
        toTarget.y = 0f;

        if (toTarget.sqrMagnitude < 0.001f)
            return Quaternion.identity;

        return Quaternion.LookRotation(toTarget.normalized, Vector3.up);
    }

    private void FindMissingReferences()
    {
        if (destroyerOrigin == null && !string.IsNullOrEmpty(destroyerObjectName))
        {
            GameObject destroyer = GameObject.Find(destroyerObjectName);
            if (destroyer != null)
                destroyerOrigin = destroyer.transform;
        }

        if (shipTarget == null && !string.IsNullOrEmpty(shipObjectName))
        {
            GameObject ship = GameObject.Find(shipObjectName);
            if (ship != null)
                shipTarget = ship.transform;
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (!drawSpawnRadius)
            return;

        Transform origin = destroyerOrigin != null ? destroyerOrigin : transform;
        Gizmos.color = spawnRadiusColor;
        Gizmos.DrawWireSphere(origin.position, spawnRadius);
    }
}
