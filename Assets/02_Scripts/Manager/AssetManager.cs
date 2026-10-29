using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary> 주소별 로딩 요청과 완료 에셋을 공유하고 핸들의 소유권을 관리합니다. </summary>
public class AssetManager
{
    private static AssetManager _instance;
    /// <summary> 전역 에셋 관리자 </summary>
    public static AssetManager Instance => _instance ?? (_instance = new AssetManager());

    /// <summary> 에셋 로드 주소와 완료 콜백입니다. </summary>
    /// <typeparam name="T">에셋 타입</typeparam>
    public struct AssetArguments<T>
    {
        public string address;
        public Action<T> successCallback;
        public Action failedCallback;
    }

    /// <summary> 하나의 주소에 대해 동기 및 비동기 호출이 공유하는 요청입니다. </summary>
    private sealed class LoadRequest
    {
        public string Address;
        public Type AssetType;
        public AsyncOperationHandle Handle;
        public bool Completed;
        public bool Released;
        public object Result;
        public readonly List<Action<bool, object>> Callbacks = new List<Action<bool, object>>();
    }

    private readonly Dictionary<string, LoadRequest> _requests = new Dictionary<string, LoadRequest>();
    private readonly HashSet<string> _persistentAddresses = new HashSet<string>();

    /// <summary> 진행 중인 로딩 또는 완료 에셋을 공유하여 비동기 요청합니다. </summary>
    /// <typeparam name="T">에셋 타입</typeparam>
    /// <param name="args">로드 주소와 콜백</param>
    internal void LoadAssetAsync<T>(AssetArguments<T> args)
    {
        LoadRequest request = GetOrStartRequest<T>(args.address);
        if (request == null || request.Released)
        {
            args.failedCallback?.Invoke();
            return;
        }
        if (request.Completed)
        {
            args.successCallback?.Invoke((T)request.Result);
            return;
        }
        request.Callbacks.Add((success, result) =>
        {
            if (success)
            {
                args.successCallback?.Invoke((T)result);
            }
            else
            {
                args.failedCallback?.Invoke();
            }
        });
        if (request.Handle.IsDone)
        {
            CompleteRequest(request);
        }
    }

    /// <summary> 기존 요청을 반환하거나 주소당 하나의 로드를 시작합니다. </summary>
    /// <typeparam name="T">에셋 타입</typeparam>
    /// <param name="address">에셋 주소</param>
    /// <returns>공유 요청 또는 유효하지 않은 요청의 경우 null</returns>
    private LoadRequest GetOrStartRequest<T>(string address)
    {
        if (string.IsNullOrEmpty(address))
        {
            Debug.LogError("[AssetManager] 에셋 주소가 비어 있습니다.");
            return null;
        }
        if (_requests.TryGetValue(address, out LoadRequest existing))
        {
            if (existing.AssetType != typeof(T))
            {
                Debug.LogError($"[AssetManager] 같은 주소에 다른 타입을 요청했습니다: {address}, {existing.AssetType} / {typeof(T)}");
                return null;
            }
            return existing;
        }

        var request = new LoadRequest { Address = address, AssetType = typeof(T) };
        try
        {
            request.Handle = Addressables.LoadAssetAsync<T>(address);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return null;
        }
        _requests.Add(address, request);
        request.Handle.Completed += operation => CompleteRequest(request);
        return request;
    }

    /// <summary> 로딩 완료를 한 번만 반영하고 대기 중인 모든 요청자에게 알립니다. </summary>
    /// <param name="request">완료할 공유 요청</param>
    private void CompleteRequest(LoadRequest request)
    {
        if (request.Completed || request.Released)
        {
            return;
        }
        request.Completed = true;
        bool success = request.Handle.Status == AsyncOperationStatus.Succeeded;
        if (success)
        {
            request.Result = request.Handle.Result;
        }
        else
        {
            Debug.LogError($"[AssetManager] 에셋 로드 실패: {request.Address}");
            _requests.Remove(request.Address);
            ReleaseHandle(request);
        }
        NotifyCallbacks(request, success);
    }

    /// <summary> 한 콜백의 예외가 다른 요청자의 완료 통지를 막지 않도록 개별 호출합니다. </summary>
    /// <param name="request">통지할 요청</param>
    /// <param name="success">로드 성공 여부</param>
    private void NotifyCallbacks(LoadRequest request, bool success)
    {
        // 콜백에서 씬 전환이나 재요청을 실행할 수 있으므로 먼저 대기 목록의 소유권을 분리합니다.
        var callbacks = request.Callbacks.ToArray();
        request.Callbacks.Clear();
        for (int i = 0; i < callbacks.Length; i++)
        {
            try
            {
                callbacks[i](success && !request.Released, request.Result);
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
            }
        }
    }

    /// <summary> 진행 중인 비동기 요청이 있으면 같은 핸들의 완료를 기다립니다. </summary>
    /// <typeparam name="T">에셋 타입</typeparam>
    /// <param name="address">에셋 주소</param>
    /// <returns>완료된 에셋 또는 실패 시 기본값</returns>
    internal T LoadAsset<T>(string address)
    {
        LoadRequest request = GetOrStartRequest<T>(address);
        if (request == null || request.Released)
        {
            return default;
        }
        if (!request.Completed)
        {
            try
            {
                request.Handle.Convert<T>().WaitForCompletion();
            }
            catch (Exception exception)
            {
                Debug.LogException(exception);
                if (!request.Released)
                {
                    _requests.Remove(address);
                    ReleaseHandle(request);
                    NotifyCallbacks(request, false);
                }
                return default;
            }
            CompleteRequest(request);
        }
        return request.Released ? default : (T)request.Result;
    }

    /// <summary> 씬 전환 후에도 유지할 주소를 등록합니다. </summary>
    /// <param name="address">유지할 에셋 주소</param>
    internal void MarkPersistent(string address)
    {
        _persistentAddresses.Add(address);
    }

    /// <summary> 완료 캐시와 진행 중인 요청을 함께 정리하고 취소된 요청자에게 실패를 알립니다. </summary>
    internal void ReleaseAll()
    {
        var releases = new List<LoadRequest>();
        foreach (LoadRequest request in _requests.Values)
        {
            if (!_persistentAddresses.Contains(request.Address))
            {
                releases.Add(request);
            }
        }
        // 실패 콜백에서 같은 주소를 다시 요청해도 새 요청까지 지우지 않도록 먼저 분리합니다.
        for (int i = 0; i < releases.Count; i++)
        {
            _requests.Remove(releases[i].Address);
            ReleaseHandle(releases[i]);
        }
        for (int i = 0; i < releases.Count; i++)
        {
            NotifyCallbacks(releases[i], false);
        }
    }

    /// <summary> 공유 핸들을 정확히 한 번 해제합니다. </summary>
    /// <param name="request">해제할 요청</param>
    private void ReleaseHandle(LoadRequest request)
    {
        if (request.Released)
        {
            return;
        }
        request.Released = true;
        if (request.Handle.IsValid())
        {
            Addressables.Release(request.Handle);
        }
    }

    /// <summary>
    /// 프리팹 에셋을 비동기적으로 로드하고 실제 게임 오브젝트로 생성(Instantiate)합니다.
    /// </summary>
    /// <param name="args">로드 설정 인자</param>
    /// <param name="parent">생성될 오브젝트의 부모 Transform</param>
    internal void LoadGameObjectAsync(AssetArguments<GameObject> args, Transform parent = null)
    {
        if (string.IsNullOrEmpty(args.address))
        {
            Debug.LogError("Addressable 주소가 비어 있습니다.");
            args.failedCallback?.Invoke();
            return;
        }

        Action<GameObject> originalSuccessCallback = args.successCallback;
        args.successCallback = (loadedPrefab) =>
        {
            if (loadedPrefab != null)
            {
                GameObject instance = UnityEngine.Object.Instantiate(loadedPrefab, parent);
                originalSuccessCallback?.Invoke(instance);
            }
            else
            {
                Debug.LogError($"프리팹 인스턴스화 실패! 주소: {args.address}");
                args.failedCallback?.Invoke();
            }
        };

        LoadAssetAsync(args);
    }

    /// <summary>
    /// 프리팹을 동기적으로 로드하고 실제 게임 오브젝트로 생성합니다.
    /// </summary>
    /// <param name="address">에셋 주소</param>
    /// <param name="parent">부모 Transform</param>
    /// <returns>생성된 게임 오브젝트</returns>
    internal GameObject LoadGameObject(string address, Transform parent = null)
    {
        GameObject prefab = LoadAsset<GameObject>(address);
        if (prefab != null)
        {
            return UnityEngine.Object.Instantiate(prefab, parent);
        }
        
        Debug.LogError($"게임 오브젝트 로드 실패! 주소: {address}");
        return null;
    }
}
