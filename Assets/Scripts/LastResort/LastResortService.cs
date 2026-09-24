using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace LastResort
{
    [Serializable]
    public sealed class StoryRequest
    {
        public string sessionId;
        public string requestId;
        public int expectedTurn;
        public string choiceId;
    }

    [Serializable]
    public sealed class LiveChoice
    {
        public string id;
        public string text;
    }

    [Serializable]
    public sealed class StoryResponse
    {
        public string sessionId;
        public string requestId;
        public int completed;
        public int day;
        public bool isEnding;
        public string title;
        public string narrative;
        public LiveChoice[] choices;
        public string epilogue;

        public bool Matches(StoryRequest request)
        {
            int expected = string.IsNullOrEmpty(request.choiceId) ? 0 : request.expectedTurn + 1;
            if (sessionId != request.sessionId || requestId != request.requestId
                || completed != expected || completed < 0 || completed > 20
                || day != Math.Min(5, completed / 4 + 1) || isEnding != (completed == 20)
                || string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(narrative)
                || choices == null) return false;
            if (isEnding) return choices.Length == 0 && !string.IsNullOrWhiteSpace(epilogue);
            if (choices.Length != 5 || !string.IsNullOrEmpty(epilogue)) return false;
            var ids = new HashSet<string>();
            foreach (LiveChoice choice in choices)
                if (choice == null || string.IsNullOrWhiteSpace(choice.id)
                    || string.IsNullOrWhiteSpace(choice.text) || !ids.Add(choice.id)) return false;
            return true;
        }
    }

    [Serializable]
    sealed class ServiceError { public string error; }

    public static class LastResortService
    {
        public static IEnumerator Send(string serverUrl, StoryRequest payload,
            Action<StoryResponse, string> finished)
        {
            Uri address;
            if (!Uri.TryCreate(serverUrl, UriKind.Absolute, out address)
                || (address.Scheme != "http" && address.Scheme != "https"))
            {
                finished(null, "The story server address is invalid. Check the LastResort object in the Inspector.");
                yield break;
            }
            using (var request = new UnityWebRequest(serverUrl.TrimEnd('/') + "/v1/story", "POST"))
            {
                request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
                request.downloadHandler = new DownloadHandlerBuffer();
                request.SetRequestHeader("Content-Type", "application/json");
                request.timeout = 110;
                UnityWebRequestAsyncOperation operation = null;
                try { operation = request.SendWebRequest(); }
                catch (Exception) { /* Handle editor HTTP restrictions without freezing the UI. */ }
                if (operation == null)
                {
                    finished(null, "Unity could not start the connection. For this local test, set Player Settings > Allow downloads over HTTP to Allowed in Development Builds, then retry.");
                    yield break;
                }
                yield return operation;
                if (request.result != UnityWebRequest.Result.Success)
                {
                    string message = null;
                    if (request.responseCode > 0)
                    {
                        try { message = JsonUtility.FromJson<ServiceError>(request.downloadHandler.text)?.error; }
                        catch (Exception) { /* Use a local, readable fallback. */ }
                    }
                    if (string.IsNullOrWhiteSpace(message))
                        message = "Could not reach the story server. Start StoryServer/Start-Server.cmd on this computer, keep its window open, then retry. Your decision has not been advanced on this screen.";
                    finished(null, message);
                    yield break;
                }
                StoryResponse result = null;
                try { result = JsonUtility.FromJson<StoryResponse>(request.downloadHandler.text); }
                catch (Exception) { /* Validation below handles unreadable responses. */ }
                if (result == null || !result.Matches(payload))
                    finished(null, "The story response was incomplete or out of sequence. Retry to recover the same request.");
                else finished(result, null);
            }
        }
    }
}
