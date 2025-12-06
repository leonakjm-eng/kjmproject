using UnityEngine;
using System.Collections.Generic;

public class FoodManager : MonoBehaviour
{
    public static FoodManager Instance;

    [Header("Settings")]
    public GameObject foodPrefab;
    public Transform spawnPoint; // New Variable
    public int maxFoodStorage = 15;
    public float spawnInterval = 2.0f;

    // Storage
    public List<GameObject> foodQueue = new List<GameObject>();

    private float timer = 0f;

    // Dragging
    private GameObject draggingFood;
    private bool isDragging = false;

    void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= spawnInterval)
        {
            timer = 0;
            AddFood();
        }

        HandleInput();
    }

    void AddFood()
    {
        if (foodQueue.Count < maxFoodStorage)
        {
            // Instantiate and store (Inactive until dragged)
            GameObject food = Instantiate(foodPrefab, Vector3.zero, Quaternion.identity);
            food.SetActive(false);
            foodQueue.Add(food);
        }
        else
        {
            SpawnFallingFood();
        }
    }

    void SpawnFallingFood()
    {
        if (spawnPoint != null)
        {
            GameObject food = Instantiate(foodPrefab, spawnPoint.position, Quaternion.identity);
            food.tag = "Food";
        }
    }

    public void OnFoodEaten(GameObject food)
    {
        // Explicit removal logic
        if (foodQueue.Contains(food))
        {
            foodQueue.Remove(food);
        }

        if (food == draggingFood)
        {
            draggingFood = null;
            isDragging = false;
        }
    }

    void HandleInput()
    {
        if (Input.GetMouseButtonDown(0))
        {
            Vector3 viewportPos = Camera.main.ScreenToViewportPoint(Input.mousePosition);
            // Click Top Area to grab from Queue
            if (viewportPos.y > 0.8f && foodQueue.Count > 0)
            {
                StartDrag();
            }
        }

        if (Input.GetMouseButton(0) && isDragging)
        {
            Drag();
        }

        if (Input.GetMouseButtonUp(0) && isDragging)
        {
            Drop();
        }
    }

    void StartDrag()
    {
        draggingFood = foodQueue[0];
        foodQueue.RemoveAt(0);

        draggingFood.SetActive(true);
        draggingFood.tag = "Food";
        isDragging = true;

        MoveToCursor();

        Rigidbody rb = draggingFood.GetComponent<Rigidbody>();
        if (rb)
        {
            rb.useGravity = false;
            rb.isKinematic = true;
        }
    }

    void Drag()
    {
        MoveToCursor();
    }

    void MoveToCursor()
    {
        if (draggingFood == null) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out float enter))
        {
            Vector3 point = ray.GetPoint(enter);
            point.y = 2.0f;
            draggingFood.transform.position = point;
        }
    }

    void Drop()
    {
        isDragging = false;
        if (draggingFood != null)
        {
            Rigidbody rb = draggingFood.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.useGravity = true;
                rb.isKinematic = false;
            }
            draggingFood = null;
        }
    }

    void OnGUI()
    {
        GUIStyle style = new GUIStyle();
        style.fontSize = 24;
        style.normal.textColor = Color.white;
        GUI.Label(new Rect(10, 10, 300, 50), $"Food Queue: {foodQueue.Count} / {maxFoodStorage}", style);
    }
}
