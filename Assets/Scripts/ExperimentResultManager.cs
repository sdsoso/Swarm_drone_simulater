using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEngine;

public class ExperimentResultManager : MonoBehaviour
{
    public static ExperimentResultManager Instance { get; private set; }

    [Header("Experiment Metadata")]
    public string algorithmName = "TTI_Alignment_Priority";
    public string csvFileName = "experiment_results.csv";
    public bool saveCsvOnExperimentEnd = true;

    [Header("Threat Weights Recorded To CSV")]
    public float ttiWeight = 0.7f;
    public float alignmentWeight = 0.3f;
    public bool acceptWeightsFromCIWS = true;

    [Header("Runtime Results")]
    public int totalUSVCount;
    public int interceptedCount;
    public int failedCount;
    public float experimentStartTime;
    public float experimentEndTime;
    public float totalEngagementTime;
    public float failureRate;

    private readonly HashSet<int> registeredUSVs = new HashSet<int>();
    private readonly HashSet<int> countedUSVs = new HashSet<int>();
    private bool experimentStarted;
    private bool experimentEnded;

    public static ExperimentResultManager GetOrCreate()
    {
        if (Instance != null)
            return Instance;

        ExperimentResultManager existingManager = FindObjectOfType<ExperimentResultManager>();
        if (existingManager != null)
            return existingManager;

        GameObject managerObject = new GameObject("ExperimentResultManager");
        return managerObject.AddComponent<ExperimentResultManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    public void SetThreatWeights(float newTtiWeight, float newAlignmentWeight)
    {
        if (!acceptWeightsFromCIWS)
            return;

        ttiWeight = newTtiWeight;
        alignmentWeight = newAlignmentWeight;
    }

    public void RegisterUSV(GameObject usv)
    {
        // Registration is based on spawned USVs, so totalUSVCount follows the scenario setup.
        if (usv == null)
            return;

        int id = usv.GetInstanceID();
        if (!registeredUSVs.Add(id))
            return;

        totalUSVCount = registeredUSVs.Count;

        if (!experimentStarted)
        {
            experimentStarted = true;
            experimentStartTime = Time.time;
        }
    }

    public void ReportIntercepted(GameObject usv)
    {
        if (!TryCountUSV(usv))
            return;

        interceptedCount++;
        CheckExperimentEnd();
    }

    public void ReportFailed(GameObject usv)
    {
        if (!TryCountUSV(usv))
            return;

        failedCount++;
        CheckExperimentEnd();
    }

    private bool TryCountUSV(GameObject usv)
    {
        // countedUSVs prevents a USV from being recorded as both intercepted and failed.
        if (usv == null || experimentEnded)
            return false;

        RegisterUSV(usv);
        return countedUSVs.Add(usv.GetInstanceID());
    }

    private void CheckExperimentEnd()
    {
        if (experimentEnded || totalUSVCount <= 0)
            return;

        if (interceptedCount + failedCount < totalUSVCount)
            return;

        EndExperiment();
    }

    private void EndExperiment()
    {
        experimentEnded = true;
        experimentEndTime = Time.time;
        totalEngagementTime = experimentEndTime - experimentStartTime;
        failureRate = totalUSVCount > 0 ? (float)failedCount / totalUSVCount : 0f;

        Debug.Log(
            "Experiment Finished\n" +
            $"Total USVs: {totalUSVCount}\n" +
            $"Intercepted USVs: {interceptedCount}\n" +
            $"Failed USVs: {failedCount}\n" +
            $"Failure Rate: {failureRate:P2}\n" +
            $"Total Engagement Time: {totalEngagementTime:F2}s");

        if (saveCsvOnExperimentEnd)
            AppendCsvResult();
    }

    private void AppendCsvResult()
    {
        // Append one row per completed run so repeated experiments can be compared in one CSV.
        string path = Path.Combine(Application.persistentDataPath, csvFileName);
        bool writeHeader = !File.Exists(path);

        using (StreamWriter writer = new StreamWriter(path, true))
        {
            if (writeHeader)
                writer.WriteLine("RunId,TotalUSVs,Intercepted,Failed,FailureRate,TotalEngagementTime,AlgorithmName,TTIWeight,AlignmentWeight");

            writer.WriteLine(string.Join(",",
                System.DateTime.UtcNow.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
                totalUSVCount.ToString(CultureInfo.InvariantCulture),
                interceptedCount.ToString(CultureInfo.InvariantCulture),
                failedCount.ToString(CultureInfo.InvariantCulture),
                failureRate.ToString(CultureInfo.InvariantCulture),
                totalEngagementTime.ToString(CultureInfo.InvariantCulture),
                EscapeCsv(algorithmName),
                ttiWeight.ToString(CultureInfo.InvariantCulture),
                alignmentWeight.ToString(CultureInfo.InvariantCulture)));
        }

        Debug.Log($"Experiment CSV saved: {path}", this);
    }

    private string EscapeCsv(string value)
    {
        if (string.IsNullOrEmpty(value))
            return string.Empty;

        if (!value.Contains(",") && !value.Contains("\"") && !value.Contains("\n"))
            return value;

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
