using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public class CameraFPS : MonoBehaviour
{
    public float rotationSpeed = 5f;
    private float totalRotation = 0f;
    private bool isRotating = false;
    private string filePath;
    public int avgFrameRate;
    private float startTime;

    private Dictionary<float, int> fpsData = new Dictionary<float, int>(); // Stores exact FPS values with time
    private Dictionary<int, int> averagedFPSData = new Dictionary<int, int>(); // Stores rounded FPS averages

    void Start()
    {
        // Set file path in persistent data path
        filePath = Path.Combine(Application.persistentDataPath, "FPS_Log.csv");

        StartRotation();
    }

    public void StartRotation()
    {
        if (!isRotating)
        {
            isRotating = true;
            totalRotation = 0f;
            startTime = Time.time;
            fpsData.Clear();
            StartCoroutine(Rotate());
        }
    }

    private IEnumerator Rotate()
    {
        while (totalRotation < 360f)
        {
            float rotationStep = rotationSpeed * Time.deltaTime;
            transform.Rotate(Vector3.up, rotationStep);
            totalRotation += rotationStep;

            // Calculate FPS and round it immediately
            int currentFPS = Mathf.RoundToInt(1f / Time.deltaTime);
            float elapsedTime = Time.time - startTime;

            // Store rounded FPS with exact time
            fpsData[elapsedTime] = currentFPS;

            Debug.Log($"{elapsedTime:F2} seconds, {currentFPS} FPS");

            yield return null;
        }

        transform.rotation = Quaternion.Euler(0, Mathf.Round(transform.eulerAngles.y), 0);
        isRotating = false;

        // Process FPS data after rotation finishes
        ComputeAveragedFPS();
        SaveAveragedFPS();

        Debug.Log("Rotation Completed! Data saved.");
    }

    private void ComputeAveragedFPS()
    {
        // Group FPS values by rounded seconds
        Dictionary<int, List<int>> groupedFPS = new Dictionary<int, List<int>>();

        foreach (var entry in fpsData)
        {
            int roundedSecond = Mathf.FloorToInt(entry.Key); // Round down to the nearest second
            if (!groupedFPS.ContainsKey(roundedSecond))
            {
                groupedFPS[roundedSecond] = new List<int>();
            }
            groupedFPS[roundedSecond].Add(entry.Value);
        }

        // Compute the average FPS per rounded second and round it
        foreach (var entry in groupedFPS)
        {
            averagedFPSData[entry.Key] = Mathf.RoundToInt((float)entry.Value.Average()); // Explicit float cast and rounding
        }
    }

    private void SaveAveragedFPS()
    {
        // Write header if file doesn't exist
        if (!File.Exists(filePath))
        {
            using (StreamWriter writer = new StreamWriter(filePath, false))
            {
                writer.WriteLine("Seconds,Average FPS");
            }
        }

        using (StreamWriter writer = new StreamWriter(filePath, true))
        {
            foreach (var entry in averagedFPSData)
            {
                writer.WriteLine($"{entry.Key},{entry.Value}");
            }
        }
    }
}
