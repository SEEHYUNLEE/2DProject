using UnityEngine;
using TMPro;

public class InputUpper : MonoBehaviour
{
    public TMP_InputField myInputField;

    public void TextUpper()
    {
        // 현재 입력된 텍스트를 대문자로 변환
        string upperText = myInputField.text.ToUpper();

        // 텍스트가 이미 대문자인지 확인하여 불필요한 루프 방지
        if (myInputField.text != upperText)
        {
            myInputField.text = upperText;
        }
    }
}
