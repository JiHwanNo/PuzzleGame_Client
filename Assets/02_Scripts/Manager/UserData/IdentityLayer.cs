using System;
using PuzleBattleShared.Models;
using UnityEngine;

/// <summary>
/// 유저 기본 정보 레이어.
/// 서버에서 유저 데이터를 요청하고 보관합니다.
/// DTO는 PuzleBattleShared.dll의 UserData를 사용합니다.
/// </summary>
public class IdentityLayer
{
    /// <summary> 서버 엔드포인트 경로 </summary>
    private const string ENDPOINT = "user/identity";
    private const string GuestProviderUserIdKey = "PuzleBattle.GuestProviderUserId";
    private int _revision;
    private Action _onLoaded;
    private Action<string> _onFailed;

    /// <summary> 유저 기본 데이터 (PuzleBattleShared.Models.UserData) </summary>
    public UserData Data { get; private set; }

    /// <summary> 데이터가 서버에서 로드되었는지 여부 </summary>
    public bool IsLoaded { get; private set; }

    /// <summary>같은 계정의 기본 정보 응답을 기다리는 중인지 표시한다.</summary>
    public bool IsLoading { get; private set; }

    /// <summary>
    /// 서버에서 유저 데이터를 요청합니다.
    /// 서버 연결 실패 시 더미 데이터로 폴백합니다.
    /// </summary>
    /// <param name="onComplete">로드 완료 시 호출되는 콜백</param>
    /// <param name="onFailed">로드 실패 시 호출되는 콜백</param>
    public void Request(Action onComplete, Action<string> onFailed)
    {
        NetworkManager network = NetworkManager.Instance;
        if (network == null)
        {
            const string error = "[IdentityLayer] 네트워크 매니저가 준비되지 않아 유저 정보를 요청할 수 없습니다.";
            Debug.LogError(error);
            if (onFailed != null)
            {
                onFailed.Invoke(error);
            }
            return;
        }
        _onLoaded += onComplete;
        _onFailed += onFailed;
        if (IsLoading)
        {
            return;
        }
        IsLoading = true;
        int revision = _revision;

        /// <summary> 서버에서 받은 유저 정보를 적용합니다. </summary>
        /// <param name="data">서버 유저 정보</param>
        void OnIdentityLoaded(UserData data)
        {
            if (revision != _revision)
            {
                return;
            }
            if (data == null)
            {
                OnIdentityLoadFailed("서버 응답에 유저 정보가 없습니다.");
                return;
            }
            Debug.Log($"[IdentityLayer] 서버 로드 완료: uid={data.uid}, nickname={data.nickname}");
            SetData(data);
        }

        /// <summary> 서버 요청 실패 시 더미 데이터를 적용합니다. </summary>
        /// <param name="error">실패 사유</param>
        void OnIdentityLoadFailed(string error)
        {
            if (revision != _revision)
            {
                return;
            }
            Debug.LogWarning($"[IdentityLayer] 서버 연결 실패, 더미 데이터로 대체: {error}");
            SetData(CreateDummyData());
        }

        if (!network.IsInitialized)
        {
            network.Initialize();
        }
        network.Get<UserData>(
            ENDPOINT,
            OnIdentityLoaded,
            OnIdentityLoadFailed
        );
    }

    /// <summary>최신 계정 <paramref name="data"/>를 반영하고 남아 있는 이전 요청의 응답을 무효화한다.</summary>
    public void SetData(UserData data)
    {
        _revision++;
        Data = data;
        IsLoaded = data != null;
        IsLoading = false;
        Action loaded = _onLoaded;
        Action<string> failed = _onFailed;
        _onLoaded = null;
        _onFailed = null;
        if (IsLoaded && loaded != null)
        {
            loaded.Invoke();
        }
        else if (!IsLoaded && failed != null)
        {
            failed.Invoke("계정이 초기화되어 기본 정보 요청을 취소했습니다.");
        }
    }

    /// <summary>
    /// 서버 연결 실패 시 사용할 더미 데이터를 생성합니다.
    /// </summary>
    /// <returns>더미 유저 데이터</returns>
    private UserData CreateDummyData()
    {
        return new UserData
        {
            uid = "dummy_user",
            nickname = "Guest",
            freeCoin = 1000,
            paidCoin = 0,
            freeDia = 50,
            paidDia = 0
        };
    }

    private string GetOrCreateGuestProviderUserId()
    {
        string providerUserId = PlayerPrefs.GetString(GuestProviderUserIdKey, "");
        if (!string.IsNullOrEmpty(providerUserId))
        {
            return providerUserId;
        }

        providerUserId = Guid.NewGuid().ToString();
        PlayerPrefs.SetString(GuestProviderUserIdKey, providerUserId);
        PlayerPrefs.Save();
        return providerUserId;
    }

    /// <summary>
    /// 데이터를 초기화합니다. 로그아웃 시 호출합니다.
    /// </summary>
    public void Clear() => SetData(null);
}
