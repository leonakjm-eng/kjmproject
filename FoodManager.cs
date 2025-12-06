using UnityEngine;

public class FoodManager : MonoBehaviour
{
    public static FoodManager Instance;

    [Header("Settings")]
    public GameObject foodPrefab;
    public int maxFoodStorage = 15;
    public float spawnInterval = 2.0f;

    private int currentFoodStorage = 0;
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
        if (currentFoodStorage < maxFoodStorage)
        {
            currentFoodStorage++;
        }
        else
        {
            SpawnFallingFood();
        }
    }

    void SpawnFallingFood()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 spawnPos = cam.ViewportToWorldPoint(new Vector3(0.95f, 0.9f, 15f));

        GameObject food = Instantiate(foodPrefab, spawnPos, Quaternion.identity);
        food.tag = "Food";
    }

    public void OnFoodEaten(GameObject food)
    {
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
            if (viewportPos.y > 0.8f && currentFoodStorage > 0)
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
        currentFoodStorage--;
        isDragging = true;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane ground = new Plane(Vector3.up, Vector3.zero);
        if (ground.Raycast(ray, out float enter))
        {
            Vector3 point = ray.GetPoint(enter);
            point.y = 2.0f;

            draggingFood = Instantiate(foodPrefab, point, Quaternion.identity);
            draggingFood.tag = "Food";

            Rigidbody rb = draggingFood.GetComponent<Rigidbody>();
            if (rb)
            {
                rb.useGravity = false;
                rb.isKinematic = true;
            }
        }
    }

    void Drag()
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
        GUI.Label(new Rect(10, 10, 300, 50), $"Food Storage: {currentFoodStorage} / {maxFoodStorage}", style);
    }
}
