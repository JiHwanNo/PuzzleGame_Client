using System;
using UnityEngine;

/// <summary>
/// 서버 통신 및 API 호출/응답을 관리하는 싱글톤 매니저.
/// HttpNetworkClient를 보유하며, 외부에 요청 메서드를 제공합니다.
/// </summary>
public class NetworkManager : MonoBehaviour
{
    private static NetworkManager _instance;

    /// <summary> 전역 접근을 위한 싱글톤 인스턴스 </summary>
    public static NetworkManager Instance => _instance;

    /// <summary> HTTP 클라이언트 </summary>
    private HttpNetworkClient _client;

    /// <summary> 서버 base URL </summary>
    private string _baseUrl = "http://localhost:7777";

    /// <summary> 현재 설정된 서버 base URL </summary>
    public string BaseUrl => _baseUrl;

    /// <summary> 클라이언트 초기화 완료 여부 </summary>
    public bool IsInitialized => _client != null;

    /// <summary>
    /// 싱글톤 인스턴스 등록 및 중복 방지
    /// </summary>
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
    }

    /// <summary>
    /// NetworkManager를 초기화합니다. HTTP 클라이언트를 생성합니다.
    /// </summary>
    /// <param name="baseUrl">서버 base URL. null이면 기본값(http://localhost:7777) 사용.</param>
    public void Initialize(string baseUrl = null)
    {
        if (baseUrl != null)
        {
            _baseUrl = baseUrl;
        }

        _client = new HttpNetworkClient(this, _baseUrl);
        Debug.Log($"[NetworkManager] 초기화 완료: {_baseUrl}");
    }

    /// <summary>
    /// GET 요청을 서버에 전송합니다.
    /// </summary>
    /// <typeparam name="TResponse">응답 데이터 타입</typeparam>
    /// <param name="endpoint">API 엔드포인트 경로</param>
    /// <param name="onSuccess">성공 시 호출되는 콜백</param>
    /// <param name="onFailed">실패 시 호출되는 콜백</param>
    /// <returns>코루틴 핸들</returns>
    public Coroutine Get<TResponse>(string endpoint, Action<TResponse> onSuccess, Action<string> onFailed)
    {
        if (_client == null)
        {
            string error = "[NetworkManager] 초기화되지 않았습니다. Initialize()를 먼저 호출하세요.";
            Debug.LogError(error);
            if (onFailed != null)
            {
                onFailed.Invoke(error);
            }
            return null;
        }

        return _client.Get(endpoint, onSuccess, onFailed);
    }

    /// <summary>
    /// POST 요청을 서버에 전송합니다.
    /// </summary>
    /// <typeparam name="TRequest">요청 본문 데이터 타입</typeparam>
    /// <typeparam name="TResponse">응답 데이터 타입</typeparam>
    /// <param name="endpoint">API 엔드포인트 경로</param>
    /// <param name="body">요청 본문 데이터</param>
    /// <param name="onSuccess">성공 시 호출되는 콜백</param>
    /// <param name="onFailed">실패 시 호출되는 콜백</param>
    /// <returns>코루틴 핸들</returns>
    public Coroutine Post<TRequest, TResponse>(string endpoint, TRequest body, Action<TResponse> onSuccess, Action<string> onFailed)
    {
        if (_client == null)
        {
            string error = "[NetworkManager] 초기화되지 않았습니다. Initialize()를 먼저 호출하세요.";
            Debug.LogError(error);
            if (onFailed != null)
            {
                onFailed.Invoke(error);
            }
            return null;
        }

        return _client.Post<TRequest, TResponse>(endpoint, body, onSuccess, onFailed);
    }
}
