using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// 게임의 전체적인 흐름과 씬 전환을 관리하는 메인 시스템 클래스입니다.
/// </summary>
public class Main : MonoBehaviour
{
    #region Singleton
    private static Main _instance;

    /// <summary> 전역 접근을 위한 싱글톤 인스턴스 </summary>
    public static Main Instance
    {
        get
        {
            return _instance;
        }
    }
    #endregion

    /// <summary> SharedScene 이름 상수 </summary>
    private const string SHARED_SCENE_NAME = "SharedScene";

    /// <summary> 현재 활성화된 씬 정보 </summary>
    private SceneEnum _curScene = SceneEnum.TitleScene;

    /// <summary> 씬 전환 중 여부 </summary>
    private bool _isMovingScene;

    /// <summary>
    /// 게임 시작 시 SharedScene을 자동 로드합니다. SharedScene 내 Main 컴포넌트가 함께 생성됩니다.
    /// </summary>
    /// <summary>
    /// 게임 시작 시 SharedScene을 자동 로드합니다.
    /// AfterSceneLoad 시점이므로 SharedScene에서 직접 플레이 시 Main이 이미 생성되어 중복 로드를 방지합니다.
    /// </summary>
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        // SharedScene에서 직접 플레이한 경우 Main.Awake가 이미 실행됨
        if (_instance != null)
        {
            return;
        }

        SceneManager.LoadScene(SHARED_SCENE_NAME, LoadSceneMode.Additive);
    }

    /// <summary>
    /// 싱글톤 인스턴스 등록 및 중복 방지, Active Scene 설정
    /// </summary>
    private void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Destroy(gameObject);
            return;
        }

        _instance = this;
        DontDestroyOnLoad(gameObject);
        SceneManager.sceneLoaded += OnSharedSceneLoaded;
        RemoveDuplicateEventSystems();

        // SharedScene의 Awake 시점에 이미 로드되어 있던 씬들의 중복 컴포넌트를 즉시 제거
        // (OnSharedSceneLoaded보다 먼저 실행되어 EventSystem 중복 경고를 방지)
        CleanupAllLoadedScenes();
    }


    /// <summary>
    /// SharedScene 로드 완료 시 Active Scene으로 설정합니다.
    /// </summary>
    /// <param name="scene">로드된 씬</param>
    /// <param name="mode">씬 로드 모드</param>
    private void OnSharedSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SHARED_SCENE_NAME)
        {
            SceneManager.SetActiveScene(scene);
            SceneManager.sceneLoaded -= OnSharedSceneLoaded;
        }
    }

    /// <summary>
    /// 현재 로드된 모든 씬을 순회하며 SharedScene이 아닌 씬의 중복 컴포넌트를 제거합니다.
    /// SharedScene 초기 로드 시 이미 존재하던 씬(예: 에디터에서 TitleScene 직접 플레이)을 정리하기 위해 사용됩니다.
    /// </summary>
    private void CleanupAllLoadedScenes()
    {
        for (int s = 0; s < SceneManager.sceneCount; s++)
        {
            Scene loadedScene = SceneManager.GetSceneAt(s);
            if (loadedScene.name == SHARED_SCENE_NAME || loadedScene.name == "DontDestroyOnLoad")
            {
                continue;
            }

            CleanupDuplicateComponents(loadedScene);
        }
    }

    /// <summary>
    /// 씬 로드 시 중복 EventSystem을 제거합니다.
    /// </summary>
    private void RemoveDuplicateEventSystems()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    /// <summary>
    /// Main 오브젝트 파괴 시 이벤트 구독을 해제합니다.
    /// </summary>
    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    /// <summary>
    /// 씬이 로드될 때마다 해당 씬 내 중복 컴포넌트를 제거합니다.
    /// SharedScene과 DontDestroyOnLoad에 포함된 것은 유지합니다.
    /// </summary>
    /// <param name="scene">로드된 씬</param>
    /// <param name="mode">씬 로드 모드</param>
    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, LoadSceneMode mode)
    {
        if (scene.name == SHARED_SCENE_NAME || scene.name == "DontDestroyOnLoad")
        {
            return;
        }

        CleanupDuplicateComponents(scene);
    }

    /// <summary>
    /// 대상 씬의 루트 오브젝트에서 중복 EventSystem, InputSystemUIInputModule, AudioListener를 제거합니다.
    /// SharedScene에만 이 컴포넌트들이 유지되도록 보장합니다.
    /// Destroy()는 프레임 끝에 실행되므로, 컴포넌트를 즉시 비활성화하여 중복 감지 경고를 방지합니다.
    /// </summary>
    /// <param name="scene">정리할 대상 씬</param>
    private void CleanupDuplicateComponents(Scene scene)
    {
        GameObject[] rootObjects = scene.GetRootGameObjects();
        for (int i = 0; i < rootObjects.Length; i++)
        {
            EventSystem es = rootObjects[i].GetComponentInChildren<EventSystem>(true);
            if (es != null)
            {
                es.enabled = false;
                es.gameObject.SetActive(false);
                Destroy(es.gameObject);
                continue;
            }

            InputSystemUIInputModule inputModule = rootObjects[i].GetComponentInChildren<InputSystemUIInputModule>(true);
            if (inputModule != null)
            {
                inputModule.enabled = false;
                inputModule.gameObject.SetActive(false);
                Destroy(inputModule.gameObject);
            }

            AudioListener al = rootObjects[i].GetComponentInChildren<AudioListener>(true);
            if (al != null)
            {
                al.enabled = false;
                Destroy(al.gameObject);
            }
        }
    }

    /// <summary>
    /// 지정된 씬으로 이동하며, 이전 씬은 언로드합니다.
    /// </summary>
    /// <param name="loadScene">로드할 대상 씬</param>
    internal void MoveScene(SceneEnum preScene, SceneEnum nextScene)
    {
        if (_isMovingScene)
        {
            return;
        }

        _isMovingScene = true;
        StartCoroutine(CoMoveScene(preScene, nextScene));
    }

    /// <summary>
    /// 이전 씬을 언로드 완료한 후 다음 씬을 로드하는 코루틴
    /// </summary>
    /// <param name="pre">언로드할 이전 씬</param>
    /// <param name="next">로드할 다음 씬</param>
    private IEnumerator CoMoveScene(SceneEnum pre, SceneEnum next)
    {

        // 0. 열려있는 모든 도메인(팝업, 탭) 정리
        if (DomainManager.Instance != null)
        {
            DomainManager.Instance.CloseAll();
        }

        // 1. 로딩 씬 로드
        AsyncOperation loadingOp = SceneManager.LoadSceneAsync(SceneEnum.LoadingScene.ToString(), LoadSceneMode.Additive);
        yield return loadingOp;

        // 2. 에셋 캐시 정리 후 이전 씬 언로드
        AssetManager.Instance.ReleaseAll();

        if (pre != SceneEnum.None)
        {
            AsyncOperation unloadOp = SceneManager.UnloadSceneAsync(pre.ToString());
            yield return unloadOp;
        }

        // 3. 다음 씬 비동기 로드 (활성화 대기)
        AsyncOperation nextOp = SceneManager.LoadSceneAsync(next.ToString(), LoadSceneMode.Additive);
        nextOp.allowSceneActivation = false;

        while (nextOp.progress < 0.9f)
        {
            yield return null;
        }

        // 4. 다음 씬 활성화
        nextOp.allowSceneActivation = true;
        yield return nextOp;

        // 5. 로딩 씬 언로드
        AsyncOperation unloadLoadingOp = SceneManager.UnloadSceneAsync(SceneEnum.LoadingScene.ToString());
        yield return unloadLoadingOp;

        _curScene = next;
        _isMovingScene = false;
    }

}

/// <summary>
/// 게임 내에서 사용하는 씬의 종류를 정의합니다.
/// </summary>
public enum SceneEnum
{
    None,
    TitleScene,
    LoadingScene,
    LobbyScene,
    GameScene,
    ToolScene,
}
