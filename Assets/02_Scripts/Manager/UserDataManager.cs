using System;
using UnityEngine;

/// <summary>
/// 유저 데이터를 관리하는 싱글톤 매니저.
/// 서버에서 유저 데이터를 로드하고 접근을 제공합니다.
/// </summary>
public class UserDataManager : MonoBehaviour
{
    private static UserDataManager _instance;

    /// <summary> 전역 접근을 위한 싱글톤 인스턴스 </summary>
    public static UserDataManager Instance => _instance;

    /// <summary> 유저 기본 정보 레이어 </summary>
    public IdentityLayer Identity { get; private set; }

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
    /// UserDataManager를 초기화합니다. 레이어를 생성합니다.
    /// </summary>
    public void Initialize()
    {
        Identity = new IdentityLayer();
        Debug.Log("[UserDataManager] 초기화 완료");
    }

    /// <summary>
    /// Identity 레이어를 서버에서 로드합니다.
    /// </summary>
    /// <param name="onComplete">로드 완료 시 호출되는 콜백</param>
    /// <param name="onFailed">로드 실패 시 호출되는 콜백</param>
    /// <param name="forceRefresh">true면 이미 로드되었어도 서버에서 다시 받아옴</param>
    public void LoadIdentity(Action onComplete, Action<string> onFailed, bool forceRefresh = false)
    {
        if (Identity == null)
        {
            string error = "[UserDataManager] 초기화되지 않았습니다. Initialize()를 먼저 호출하세요.";
            Debug.LogError(error);
            if (onFailed != null)
            {
                onFailed.Invoke(error);
            }
            return;
        }

        if (Identity.IsLoaded && !forceRefresh)
        {
            if (onComplete != null)
            {
                onComplete.Invoke();
            }
            return;
        }

        Identity.Request(onComplete, onFailed);
    }

    /// <summary>
    /// 모든 유저 데이터를 초기화합니다. 로그아웃 시 호출합니다.
    /// </summary>
    public void ClearAll()
    {
        if (Identity != null)
        {
            Identity.Clear();
        }
        Debug.Log("[UserDataManager] 모든 데이터 초기화 완료");
    }
}
