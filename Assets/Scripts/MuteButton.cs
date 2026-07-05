using UnityEngine;

public class MuteButton : MonoBehaviour
{
    private static bool isMuted = false;
    public UnityEngine.UI.Image buttonImage;

    void Start()
    {
        // 씬이 시작될 때 이전 상태를 바로 적용
        ApplyMuteState();
    }

    public void OnClickToggle()
    {
        isMuted = !isMuted;
        ApplyMuteState();
    }

    private void ApplyMuteState()
    {
        // AudioListener.pause를 true로 하면 게임 내 모든 소리가 즉시 멈춥니다.
        AudioListener.pause = isMuted;

        // 버튼 색상 변경
        if (buttonImage != null)
        {
            buttonImage.color = isMuted ? Color.gray : Color.white;
        }
    }
}
