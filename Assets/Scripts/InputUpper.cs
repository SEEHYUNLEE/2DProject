using UnityEngine;
using TMPro;

public class InputUpper : MonoBehaviour
{
    public TMP_InputField myInputField;

    public void TextUpper()
    {
        string upperText = myInputField.text.ToUpper();

        if (myInputField.text != upperText)
        {
            myInputField.text = upperText;
        }
    }
}
