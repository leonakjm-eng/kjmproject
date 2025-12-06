using UnityEngine;

public class FishController : MonoBehaviour
{
    [Header("Settings")]
    public float baseSpeed = 3.0f;
    public float chaseMultiplier = 3.0f;
    public float rotationSpeed = 5.0f;

    private Rigidbody rb;
    private Vector3 targetDirection;
    private float changeDirectionTimer;
    private Transform currentTargetFood;

    // State
    private int eatCount = 0;
    private float speedBonus = 0f;

    // Bounds
    private float xBound = 8.0f;
    private float zBound = 14.0f;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.constraints = RigidbodyConstraints.FreezePositionY | RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;

        CalculateBounds();
        ChangeRandomDirection();

        if (GameManager.Instance != null)
        {
            GameManager.Instance.RegisterFish(this);
        }
    }

    void CalculateBounds()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        float dist = Mathf.Abs(cam.transform.position.y);
        float height = 2.0f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float width = height * cam.aspect;

        xBound = width * 0.5f - 1.0f;
        zBound = height * 0.5f - 1.0f;
    }

    void Update()
    {
        FindTargetFood();

        if (currentTargetFood != null)
        {
            Vector3 dir = (currentTargetFood.position - transform.position).normalized;
            dir.y = 0;
            targetDirection = dir;
        }
        else
        {
            changeDirectionTimer -= Time.deltaTime;
            if (changeDirectionTimer <= 0)
            {
                ChangeRandomDirection();
            }
        }

        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRot = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
        }

        Vector3 pos = transform.position;
        pos.x = Mathf.Clamp(pos.x, -xBound, xBound);
        pos.z = Mathf.Clamp(pos.z, -zBound, zBound);
        pos.y = 0;
        transform.position = pos;
    }

    void FixedUpdate()
    {
        float levelMultiplier = 1.0f;
        if (GameManager.Instance != null)
        {
            levelMultiplier = GameManager.Instance.GetSpeedMultiplier();
        }

        float currentSpeed = (baseSpeed + speedBonus) * levelMultiplier;

        if (currentTargetFood != null)
            currentSpeed *= chaseMultiplier;

        rb.velocity = transform.forward * currentSpeed;
    }

    void FindTargetFood()
    {
        GameObject[] foods = GameObject.FindGameObjectsWithTag("Food");
        float closestDist = float.MaxValue;
        currentTargetFood = null;

        foreach (var food in foods)
        {
            // Fix: Ignore shelf food (High Y)
            if (food.transform.position.y >= 10.0f) continue;

            float d = Vector3.Distance(transform.position, food.transform.position);
            if (d < closestDist)
            {
                closestDist = d;
                currentTargetFood = food.transform;
            }
        }
    }

    void ChangeRandomDirection()
    {
        float angle = Random.Range(0f, 360f);
        targetDirection = new Vector3(Mathf.Sin(angle * Mathf.Deg2Rad), 0, Mathf.Cos(angle * Mathf.Deg2Rad));
        changeDirectionTimer = Random.Range(1.0f, 3.0f);
    }

    void OnCollisionEnter(Collision collision)
    {
        HandleCollision(collision);
    }

    void OnCollisionStay(Collision collision)
    {
        HandleCollision(collision);
    }

    void HandleCollision(Collision collision)
    {
        if (collision.gameObject.CompareTag("Food"))
        {
            Eat(collision.gameObject);
        }
        else if (collision.gameObject.CompareTag("Fish"))
        {
            FishController other = collision.gameObject.GetComponent<FishController>();
            if (other != null)
            {
                float mySize = transform.localScale.x;
                float otherSize = other.transform.localScale.x;

                // Fix: Robust Predation Logic
                if (mySize > otherSize * 1.3f)
                {
                    if (GameManager.Instance != null)
                    {
                        GameManager.Instance.OnFishDied(collision.gameObject);
                    }
                    Destroy(collision.gameObject);
                }
            }
        }
    }

    void Eat(GameObject food)
    {
        if (food == null) return;

        // Prevent double eating logic if multiple frames trigger collision before destroy
        if (!food.activeSelf) return;
        food.SetActive(false); // Hide immediately

        Destroy(food);
        if (FoodManager.Instance != null) FoodManager.Instance.OnFoodEaten(food);

        transform.localScale *= 1.1f;
        speedBonus += 0.2f;
        eatCount++;

        if (eatCount >= 3)
        {
            Reproduce();
            eatCount = 0;
        }
    }

    void Reproduce()
    {
        // Fix: Population Cap
        if (GameManager.Instance != null && GameManager.Instance.GetCurrentFishCount() >= 18) return;

        GameObject clone = Instantiate(gameObject, transform.position, Quaternion.identity);
        FishController fc = clone.GetComponent<FishController>();
        fc.ResetState();
        // RegisterFish called in Start
    }

    public void ResetState()
    {
        eatCount = 0;
    }
}
