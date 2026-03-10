using UnityEngine;
using UnityEngine.Networking;
using System.Collections;
using TMPro;

public class GeminiBridge : MonoBehaviour
{
    public TMP_InputField userInput; 
    public TextMeshProUGUI aiResponseDisplay;

    // IMPORTANT: Put your API Key from Google between the quotes below
    private string apiKey = "AIzaSyCfUQm0lLfeYPbLoVKGji1vRsdB1DI10MA"; 
    private string url = "https://generativelanguage.googleapis.com/v1beta/models/gemini-1.5-flash:generateContent?key=";

    public void OnSendClick() {
        if (string.IsNullOrEmpty(userInput.text)) return;
        StartCoroutine(PostRequest(userInput.text));
    }

    IEnumerator PostRequest(string userText) {
        aiResponseDisplay.text = "Tutor is thinking...";
        string jsonBody = "{\"contents\": [{\"parts\":[{\"text\":\"" + userText + "\"}]}]}";
        
        using (UnityWebRequest request = new UnityWebRequest(url + apiKey, "POST")) {
            byte[] bodyRaw = System.Text.Encoding.UTF8.GetBytes(jsonBody);
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            if (request.result == UnityWebRequest.Result.Success) {
                string responseText = request.downloadHandler.text;
                int start = responseText.IndexOf("\"text\": \"") + 9;
                int end = responseText.IndexOf("\"", start);
                string finalAnswer = responseText.Substring(start, end - start);
                aiResponseDisplay.text = finalAnswer.Replace("\\n", "\n").Replace("\\\"", "\"");
            } else {
                aiResponseDisplay.text = "Error: Check Internet or API Key.";
            }
        }
    }
}