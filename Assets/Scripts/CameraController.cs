using System.Diagnostics;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    public float moveSpeed = 10f;
    public float edgeSize = 20f;

    public BoxCollider2D mapCollider;

    float minX, maxX;

    Camera cam;

    private Vector2 lastTouchPos;

    void Start()
    {
        cam = Camera.main;

        // 맵 경계 가져오기 (월드 좌표)
        minX = mapCollider.bounds.min.x;
        maxX = mapCollider.bounds.max.x;


        UnityEngine.Debug.Log("minX: " + minX + " maxX: " + maxX);
    }

    void Update()
    {
#if UNITY_EDITOR || UNITY_STANDALONE
        HandleMouse();
#else
        HandleTouch();
#endif
        ClampPosition();
    }

    void HandleMouse()
    {
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Vector3 pos = transform.position;

        if (mousePos.x >= Screen.width - edgeSize)
        {
            pos.x += moveSpeed * Time.deltaTime;
        }
        else if (mousePos.x <= edgeSize)
        {
            pos.x -= moveSpeed * Time.deltaTime;
        }

        transform.position = pos;
    }

    void HandleTouch()
    {
        if (Touchscreen.current.primaryTouch.press.isPressed)
        {
            Vector2 touchPos = Touchscreen.current.primaryTouch.position.ReadValue();

            if (Touchscreen.current.primaryTouch.press.wasPressedThisFrame)
            {
                lastTouchPos = touchPos;
            }
            else
            {
                Vector2 delta = touchPos - lastTouchPos;
                transform.Translate(-delta.x * 0.01f, 0, 0);
                lastTouchPos = touchPos;
            }
        }
    }

    void ClampPosition()
    {
        Vector3 pos = transform.position;

        // 카메라 반 너비 계산
        float camHalfWidth = cam.orthographicSize * cam.aspect;

        // 카메라 끝이 경계에 닿도록 제한
        float leftLimit = minX + camHalfWidth;
        float rightLimit = maxX - camHalfWidth;

        pos.x = Mathf.Clamp(pos.x, leftLimit, rightLimit);

        transform.position = pos;
    }

}
