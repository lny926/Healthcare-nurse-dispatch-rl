using System.IO;
using UnityEngine;

public static class CSVExporter
{
    private static string fileName = "ExperimentResults.csv";

    public static void ExportExperimentResult(
        string strategy,
        int seed,
        float timeScale,
        float durationHours,
        int totalTasks,
        int completedTasks,
        float completionRate,
        float avgWait,
        float p95Wait,
        int escalations,
        float totalDistance,
        int routineCompleted,
        float averageFatigue
    )
    {
        string path = Path.Combine(UnityEngine.Application.dataPath, fileName);

        bool fileExists = File.Exists(path);

        using (StreamWriter writer = new StreamWriter(path, true))
        {
            if (!fileExists)
            {
                writer.WriteLine(
                    "Timestamp,Strategy,Seed,TimeScale,DurationHours," +
                    "TotalTasks,CompletedTasks,CompletionRate," +
                    "AverageWait,P95Wait,Escalations," +
                    "TotalDistance,RoutineCompleted,AverageFatigue"
                );
            }

            writer.WriteLine(
                System.DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + "," +
                strategy + "," +
                seed + "," +
                timeScale + "," +
                durationHours + "," +
                totalTasks + "," +
                completedTasks + "," +
                completionRate + "," +
                avgWait + "," +
                p95Wait + "," +
                escalations + "," +
                totalDistance + "," +
                routineCompleted + "," +
                averageFatigue
            );
        }

        Debug.Log("CSV Exported: " + path);
    }
}