using DG.Tweening;
using TMPro;
using UnityEngine;

/// <summary>
/// 타이틀 씬 메인 스크립트
/// </summary>
public class TitleMain : MonoBehaviour
{
    [SerializeField] private TMP_Text text_CI;

    /// <summary>
    /// 초기화 시 텍스트 CI 색상 트윈 재생
    /// </summary>
    private void Start()
    {
        text_CI.color = Color.black;
        Sequence sequence = DOTween.Sequence();
        sequence.Append(text_CI.DOColor(new Color32(0x00, 0xFF, 0x80, 0xFF), 1f));
        sequence.AppendInterval(1f);
        sequence.AppendCallback(() => Main.Instance.MoveScene(SceneEnum.TitleScene, SceneEnum.LobbyScene));
    }
}
