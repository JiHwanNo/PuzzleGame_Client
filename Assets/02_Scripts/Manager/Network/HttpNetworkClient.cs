using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// UnityWebRequest 기반 HTTP 클라이언트.
/// REST API 서버와 JSON 형식으로 통신합니다.
/// </summary>
public class HttpNetworkClient
{
    /// <summary> 코루틴 실행을 위한 MonoBehaviour 참조 </summary>
    private readonly MonoBehaviour _coroutineRunner;

    /// <summary> 서버 base URL (예: http://localhost:7777) </summary>
    private readonly string _baseUrl;

    /// <summary>
    /// HttpNetworkClient를 생성합니다.
    /// </summary>
    /// <param name="coroutineRunner">코루틴을 실행할 MonoBehaviour</param>
    /// <param name="baseUrl">서버 base URL</param>
    public HttpNetworkClient(MonoBehaviour coroutineRunner, string baseUrl)
    {
        _coroutineRunner = coroutineRunner;
        _baseUrl = baseUrl.TrimEnd('/');
    }

    /// <summary>
    /// GET 요청을 전송하고 응답을 역직렬화하여 콜백으로 반환합니다.
    /// </summary>
    /// <typeparam name="TResponse">응답 데이터 타입</typeparam>
    /// <param name="endpoint">API 엔드포인트 경로</param>
    /// <param name="onSuccess">성공 시 호출되는 콜백</param>
    /// <param name="onFailed">실패 시 호출되는 콜백</param>
    /// <returns>코루틴 핸들</returns>
    public Coroutine Get<TResponse>(string endpoint, Action<TResponse> onSuccess, Action<string> onFailed) => _coroutineRunner.StartCoroutine(CoGet(endpoint, onSuccess, onFailed));

    /// <summary>
    /// POST 요청을 전송하고 응답을 역직렬화하여 콜백으로 반환합니다.
    /// </summary>
    /// <typeparam name="TRequest">요청 본문 데이터 타입</typeparam>
    /// <typeparam name="TResponse">응답 데이터 타입</typeparam>
    /// <param name="endpoint">API 엔드포인트 경로</param>
    /// <param name="body">요청 본문 데이터</param>
    /// <param name="onSuccess">성공 시 호출되는 콜백</param>
    /// <param name="onFailed">실패 시 호출되는 콜백</param>
    /// <returns>코루틴 핸들</returns>
    public Coroutine Post<TRequest, TResponse>(string endpoint, TRequest body, Action<TResponse> onSuccess, Action<string> onFailed) => _coroutineRunner.StartCoroutine(CoPost(endpoint, body, onSuccess, onFailed));

    /// <summary>
    /// GET 요청 코루틴. UnityWebRequest로 서버에 요청을 보내고 JSON 응답을 파싱합니다.
    /// </summary>
    /// <typeparam name="TResponse">응답 데이터 타입</typeparam>
    /// <param name="endpoint">API 엔드포인트 경로</param>
    /// <param name="onSuccess">성공 콜백</param>
    /// <param name="onFailed">실패 콜백</param>
    private IEnumerator CoGet<TResponse>(string endpoint, Action<TResponse> onSuccess, Action<string> onFailed)
    {
        string url = BuildUrl(endpoint);

        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            HandleResponse(request, onSuccess, onFailed);
        }
    }

    /// <summary>
    /// POST 요청 코루틴. 요청 본문을 JSON으로 직렬화하여 서버에 전송합니다.
    /// </summary>
    /// <typeparam name="TRequest">요청 본문 데이터 타입</typeparam>
    /// <typeparam name="TResponse">응답 데이터 타입</typeparam>
    /// <param name="endpoint">API 엔드포인트 경로</param>
    /// <param name="body">요청 본문</param>
    /// <param name="onSuccess">성공 콜백</param>
    /// <param name="onFailed">실패 콜백</param>
    private IEnumerator CoPost<TRequest, TResponse>(string endpoint, TRequest body, Action<TResponse> onSuccess, Action<string> onFailed)
    {
        string url = BuildUrl(endpoint);
        string jsonBody = JsonUtility.ToJson(body);
        byte[] bodyRaw = Encoding.UTF8.GetBytes(jsonBody);

        using (UnityWebRequest request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(bodyRaw);
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");

            yield return request.SendWebRequest();

            HandleResponse(request, onSuccess, onFailed);
        }
    }

    /// <summary>
    /// 서버 응답을 처리합니다. 성공 시 JSON 역직렬화, 실패 시 에러 콜백 호출.
    /// </summary>
    /// <typeparam name="TResponse">응답 데이터 타입</typeparam>
    /// <param name="request">완료된 UnityWebRequest</param>
    /// <param name="onSuccess">성공 콜백</param>
    /// <param name="onFailed">실패 콜백</param>
    private void HandleResponse<TResponse>(UnityWebRequest request, Action<TResponse> onSuccess, Action<string> onFailed)
    {
        if (request.result != UnityWebRequest.Result.Success)
        {
            string errorMessage = $"[HttpNetworkClient] 요청 실패: {request.url} / {request.error}";
            Debug.LogError(errorMessage);
            if (onFailed != null)
            {
                onFailed.Invoke(errorMessage);
            }
            return;
        }

        string responseJson = request.downloadHandler.text;

        try
        {
            TResponse responseData = JsonUtility.FromJson<TResponse>(responseJson);
            if (onSuccess != null)
            {
                onSuccess.Invoke(responseData);
            }
        }
        catch (Exception e)
        {
            string errorMessage = $"[HttpNetworkClient] JSON 파싱 실패: {request.url} / {e.Message}";
            Debug.LogError(errorMessage);
            if (onFailed != null)
            {
                onFailed.Invoke(errorMessage);
            }
        }
    }

    /// <summary>
    /// base URL과 endpoint를 결합하여 전체 URL을 생성합니다.
    /// </summary>
    /// <param name="endpoint">API 엔드포인트 경로</param>
    /// <returns>전체 URL 문자열</returns>
    private string BuildUrl(string endpoint)
    {
        string trimmedEndpoint = endpoint.TrimStart('/');
        return $"{_baseUrl}/{trimmedEndpoint}";
    }
}
