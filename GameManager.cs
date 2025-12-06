using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance;

    [Header("Settings")]
    public GameObject fishPrefab;
    public int currentLevel = 1;
    public int deathCount = 0;

    private List<GameObject> fishes = new List<GameObject>();

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        StartLevel(currentLevel);
    }

    void Update()
    {
        // Win Condition
        if (fishes.Count >= GetTargetCount())
        {
            LevelUp();
        }
    }

    public void RegisterFish(GameObject fish = null)
    {
        // Called when reproduced
        if (fish != null) fishes.Add(fish);
    }

    public void OnFishDied(GameObject fish)
    {
        if (fishes.Contains(fish))
        {
            fishes.Remove(fish);
            deathCount++;
        }
    }

    // Fixed: Added missing method
    public int GetCurrentFishCount()
    {
        return fishes.Count;
    }

    void LevelUp()
    {
        // Death Redemption
        if (deathCount > 0) deathCount--;

        currentLevel++;
        StartLevel(currentLevel);
    }

    void StartLevel(int level)
    {
        // Reset Board
        foreach(var f in fishes)
        {
            if (f != null) Destroy(f);
        }
        fishes.Clear();

        // Initial Count Formula: 3 + (Level - 1) / 2
        int initCount = 3 + (level - 1) / 2;

        for (int i = 0; i < initCount; i++)
        {
            SpawnFish();
        }
    }

    void SpawnFish()
    {
        // Random Position within bounds (approx)
        Vector3 pos = new Vector3(Random.Range(-6f, 6f), 0, Random.Range(-3f, 3f));
        GameObject fish = Instantiate(fishPrefab, pos, Quaternion.identity);
        fish.tag = "Fish";
        fishes.Add(fish);
    }

    public int GetTargetCount()
    {
        // Cap at 18
        return Mathf.Min(18, 6 + (currentLevel - 1));
    }

    public float GetSpeedMultiplier()
    {
        // Target is 18 when 6 + (L-1) = 18 => L = 13.
        // If Level >= 13, speed increases.
        if (currentLevel < 13) return 1.0f;

        float mult = 1.0f + (currentLevel - 13) * 0.2f;
        return Mathf.Min(3.0f, mult);
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.yellow;

        GUI.Label(new Rect(10, 50, 300, 50), $"Level: {currentLevel}", style);
        GUI.Label(new Rect(10, 90, 300, 50), $"Fish: {fishes.Count} / {GetTargetCount()}", style);
        GUI.Label(new Rect(10, 130, 300, 50), $"Death: {deathCount}", style);
    }
}
