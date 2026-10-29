using UnityEngine;

/// <summary>
/// 게임 데이터 로드 및 관리를 담당하는 싱글톤 매니저
/// </summary>
public class GameDataManager : MonoBehaviour
{
    private static GameDataManager _instance;

    /// <summary> 전역 접근을 위한 싱글톤 인스턴스 </summary>
    public static GameDataManager Instance => _instance;

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
}
