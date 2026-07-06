using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public float dragSpeed = 0.02f;
    public BoxCollider2D mapCollider;

    private float minX, maxX;
    private Camera cam;

    private Vector2 lastInputPos;
    private bool isDragging = false;

    void Start()
    {
        cam = Camera.main;

        // 맵 경계 가져오기
        minX = mapCollider.bounds.min.x;
        maxX = mapCollider.bounds.max.x;
    }

    void Update()
    {
        HandleInput();
        ClampPosition();
    }

    void HandleInput()
    {
        // 마우스, 터치 입력을 통합적으로 가져옴
        bool isPressed = false;
        bool wasPressed = false;
        Vector2 currentPos = Vector2.zero;

        // 터치 우선 확인
        if (Touchscreen.current != null && Touchscreen.current.primaryTouch.press.isPressed)
        {
            isPressed = true;
            wasPressed = Touchscreen.current.primaryTouch.press.wasPressedThisFrame;
            currentPos = Touchscreen.current.primaryTouch.position.ReadValue();
        }
        else if (Mouse.current != null && Mouse.current.leftButton.isPressed)
        {
            isPressed = true;
            wasPressed = Mouse.current.leftButton.wasPressedThisFrame;
            currentPos = Mouse.current.position.ReadValue();
        }

        if (wasPressed)
        {
            lastInputPos = currentPos;
            isDragging = true;
        }
        else if (isPressed && isDragging)
        {
            Vector2 delta = currentPos - lastInputPos;

            // 드래그 방향과 반대로 이동
            transform.Translate(-delta.x * dragSpeed, 0, 0);

            lastInputPos = currentPos;
        }
        else
        {
            isDragging = false;
        }
    }

    void ClampPosition()
    {
        Vector3 pos = transform.position;
        float camHalfWidth = cam.orthographicSize * cam.aspect;

        float leftLimit = minX + camHalfWidth;
        float rightLimit = maxX - camHalfWidth;

        pos.x = Mathf.Clamp(pos.x, leftLimit, rightLimit);
        transform.position = pos;
    }
}