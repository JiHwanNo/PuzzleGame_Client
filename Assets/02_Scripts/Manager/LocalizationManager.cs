using UnityEngine;

/// <summary>
/// 다국어 텍스트 로드 및 언어 전환을 관리하는 싱글톤 매니저
/// </summary>
public class LocalizationManager : MonoBehaviour
{
    private static LocalizationManager _instance;

    /// <summary> 전역 접근을 위한 싱글톤 인스턴스 </summary>
    public static LocalizationManager Instance => _instance;

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
