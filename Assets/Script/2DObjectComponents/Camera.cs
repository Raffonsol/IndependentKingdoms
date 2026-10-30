using UnityEngine;

public class GameCamera : MonoBehaviour
{
    [SerializeField] private float movementSpeed = 5f;
    [SerializeField] private float movementAcceleration = 15f;
    [SerializeField] private float movementDeceleration = 12f;
    [SerializeField] private float edgeSize = 20f;
    [SerializeField] private float minZoom = 2f;
    [SerializeField] private float maxZoom = 10f;
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float zoomSmoothTime = 0.15f;

    private UnityEngine.Camera attachedCamera;
    private bool isDragging;
    private Vector3 lastMousePosition;
    private Vector2 movementVelocity;
    private float targetZoom;
    private float zoomVelocity;

    private void Awake()
    {
        attachedCamera = GetComponent<UnityEngine.Camera>();
        if (attachedCamera == null)
        {
            Debug.LogError("Camera movement requires a Camera component on " + gameObject.name + ".", this);
            return;
        }

        ClampZoom();
        targetZoom = GetZoom();
    }

    private void Update()
    {
        UpdateZoom();

        Vector2 movementDirection = GetKeyboardMovement();
        if (Screen.fullScreen && !isDragging && !Input.GetMouseButton(0))
        {
            movementDirection += GetEdgeMovement();
        }

        if (movementDirection.sqrMagnitude > 1f)
        {
            movementDirection.Normalize();
        }

        Vector2 targetVelocity = movementDirection * movementSpeed;
        float acceleration = movementDirection.sqrMagnitude > 0f
            ? movementAcceleration
            : movementDeceleration;
        movementVelocity = Vector2.MoveTowards(
            movementVelocity,
            targetVelocity,
            Mathf.Max(0f, acceleration) * Time.deltaTime);

        Vector3 movement = (transform.right * movementVelocity.x + transform.up * movementVelocity.y)
            * Time.deltaTime;

        UpdateDragging(ref movement);
        transform.position += movement;
    }

    private void UpdateZoom()
    {
        if (attachedCamera == null)
        {
            return;
        }

        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (!Mathf.Approximately(scrollInput, 0f))
        {
            targetZoom = Mathf.Clamp(targetZoom - scrollInput * zoomSpeed, minZoom, maxZoom);
        }

        float zoom = Mathf.SmoothDamp(
            GetZoom(),
            targetZoom,
            ref zoomVelocity,
            Mathf.Max(0.0001f, zoomSmoothTime));
        SetZoom(Mathf.Clamp(zoom, minZoom, maxZoom));
    }

    private float GetZoom()
    {
        return attachedCamera.orthographic
            ? attachedCamera.orthographicSize
            : attachedCamera.fieldOfView;
    }

    private void SetZoom(float zoom)
    {
        if (attachedCamera.orthographic)
        {
            attachedCamera.orthographicSize = zoom;
        }
        else
        {
            attachedCamera.fieldOfView = zoom;
        }
    }

    private void ClampZoom()
    {
        minZoom = Mathf.Max(0.01f, minZoom);
        maxZoom = Mathf.Max(minZoom, maxZoom);
        SetZoom(Mathf.Clamp(GetZoom(), minZoom, maxZoom));
    }

    private Vector2 GetKeyboardMovement()
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow))
        {
            horizontal -= 1f;
        }
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow))
        {
            horizontal += 1f;
        }
        if (Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow))
        {
            vertical -= 1f;
        }
        if (Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow))
        {
            vertical += 1f;
        }

        return new Vector2(horizontal, vertical);
    }

    private Vector2 GetEdgeMovement()
    {
        Vector3 mousePosition = Input.mousePosition;
        if (mousePosition.x < 0f || mousePosition.x > Screen.width
            || mousePosition.y < 0f || mousePosition.y > Screen.height)
        {
            return Vector2.zero;
        }

        float horizontal = 0f;
        float vertical = 0f;

        if (mousePosition.x <= edgeSize)
        {
            horizontal -= 1f;
        }
        else if (mousePosition.x >= Screen.width - edgeSize)
        {
            horizontal += 1f;
        }

        if (mousePosition.y <= edgeSize)
        {
            vertical -= 1f;
        }
        else if (mousePosition.y >= Screen.height - edgeSize)
        {
            vertical += 1f;
        }

        return new Vector2(horizontal, vertical);
    }

    private void UpdateDragging(ref Vector3 movement)
    {
        if (Input.GetMouseButtonDown(0))
        {
            isDragging = true;
            lastMousePosition = Input.mousePosition;
        }

        if (isDragging && Input.GetMouseButton(0))
        {
            Vector3 mousePosition = Input.mousePosition;
            Vector3 mouseDelta = mousePosition - lastMousePosition;
            lastMousePosition = mousePosition;

            float worldUnitsPerPixel = GetWorldUnitsPerPixel();
            movement += transform.right * (-mouseDelta.x * worldUnitsPerPixel)
                + transform.up * (-mouseDelta.y * worldUnitsPerPixel);
        }

        if (Input.GetMouseButtonUp(0))
        {
            isDragging = false;
        }
    }

    private float GetWorldUnitsPerPixel()
    {
        if (attachedCamera == null || Screen.height <= 0)
        {
            return 0f;
        }

        if (attachedCamera.orthographic)
        {
            return attachedCamera.orthographicSize * 2f / Screen.height;
        }

        float distanceToCameraPlane = Mathf.Abs(transform.position.z);
        float halfFieldOfViewRadians = attachedCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        return 2f * distanceToCameraPlane * Mathf.Tan(halfFieldOfViewRadians) / Screen.height;
    }
}
