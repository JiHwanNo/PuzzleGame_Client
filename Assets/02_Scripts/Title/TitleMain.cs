using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>로비의 성 아이콘과 타이틀 로고를 표시한 뒤 로비로 이동한다.</summary>
public class TitleMain : MonoBehaviour
{
    [SerializeField] private TMP_Text text_CI;
    private RawImage _castle;

    /// <summary>젤리 성 아이콘을 요청하고 로고가 밝아지는 연출 뒤 로비로 전환한다.</summary>
    private void Start()
    {
        _castle = new GameObject("Castle", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
        _castle.rectTransform.SetParent(text_CI.rectTransform, false);
        _castle.rectTransform.anchoredPosition = new Vector2(0f, 158f);
        _castle.rectTransform.sizeDelta = new Vector2(202f, 202f);
        _castle.uvRect = new Rect(0.4f, 0f, 0.2f, 1f);
        _castle.raycastTarget = false;
        _castle.enabled = false;
        AssetManager.Instance.LoadAssetAsync(new AssetManager.AssetArguments<Texture2D>
        {
            address = "LobbyNavigation",
            successCallback = ShowCastle
        });

        text_CI.alpha = 0f;
        Sequence sequence = DOTween.Sequence().SetLink(gameObject);
        sequence.Append(text_CI.DOFade(1f, 1f));
        sequence.AppendInterval(1f);
        sequence.AppendCallback(() => Main.Instance.MoveScene(SceneEnum.TitleScene, SceneEnum.LobbyScene));
    }

    /// <summary>비동기로 받은 <paramref name="atlas"/>의 메인 탭 성을 타이틀이 열려 있는 동안 표시한다.</summary>
    private void ShowCastle(Texture2D atlas)
    {
        if (_castle != null)
        {
            _castle.texture = atlas;
            _castle.enabled = true;
        }
    }
}
