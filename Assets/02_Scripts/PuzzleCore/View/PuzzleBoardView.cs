using UnityEngine;
using System.Collections.Generic;

using Puzzle.Core;
using System.Collections;

/// <summary>
/// 퍼즐 보드의 데이터(Model)를 받아 화면에 시각적으로 렌더링하는 View 클래스입니다.
/// 보드의 크기에 맞춰 타일과 블럭들을 배치하고 관리합니다.
/// </summary>
public class PuzzleBoardView : MonoBehaviour
{
    /// <summary> 생성할 셀(배경 타일) 프리팹의 Resources 경로 </summary>
    private string cellAddress = "Prefab/Puzzle/CellPrefab";

    /// <summary> 생성할 블럭 프리팹의 Resources 경로 </summary>
    private string blockAddress = "Prefab/Puzzle/BlockPrefab";

    [Header("Hierarchy Roots")]
    /// <summary> 셀들이 생성될 부모 트랜스폼 </summary>
    public Transform cellRoot;

    /// <summary> 블럭들이 생성될 부모 트랜스폼 </summary>
    public Transform blockRoot;

    [Header("Settings")]
    /// <summary> 타일 한 칸의 시각적 크기 </summary>
    public float cellSize = 1.0f;

    /// <summary> 보드 외곽의 여백 크기 </summary>
    public float padding = 1.0f;

    /// <summary> true이면 DrawBoard 시 카메라 orthographicSize 조정을 건너뜁니다. (리플레이 보드용) </summary>
    public bool skipCameraAlign = false;

    /// <summary> 
    /// 보드의 수직 위치 오프셋 (0: 중앙, 0.5: 상단 정렬, -0.5: 하단 정렬) 
    /// </summary>
    [Range(-0.5f, 0.5f)]
    public float offsetY = 0f;

    /// <summary> 씬 뷰에서 그리드 좌표와 블럭 정보를 표시할지 여부 </summary>
    public bool showDebugGrid = true;

    /// <summary> 현재 연결된 보드 모델 데이터 </summary>
    private IPuzzleBoard _board;

    /// <summary> 캐싱된 보드 모양 (GetLocalPos 최적화용) </summary>
    private BoardShape _cachedBoardShape = BoardShape.None;

    /// <summary> 로드된 셀 프리팹 캐시 </summary>
    private GameObject _cellPrefabObj;

    /// <summary> 로드된 블럭 프리팹 캐시 </summary>
    private GameObject _blockPrefabObj;
    
    /// <summary> 화면에 생성된 셀 뷰들을 좌표별로 관리하는 딕셔너리 </summary>
    private Dictionary<GridPos, PuzzleCellView> _cellViews = new Dictionary<GridPos, PuzzleCellView>();

    /// <summary> 화면에 생성된 블럭 뷰들을 좌표별로 관리하는 딕셔너리 </summary>
    private Dictionary<GridPos, PuzzleBlockView> _blockViews = new Dictionary<GridPos, PuzzleBlockView>();

    /// <summary> 보드 연출 액션 그룹 대기열 (프레임 단위로 그룹화됨) </summary>
    private Queue<List<BoardViewAction>> _actionQueue = new Queue<List<BoardViewAction>>();

    /// <summary> 액션 그룹 리스트 재사용 풀 </summary>
    private readonly Stack<List<BoardViewAction>> _actionGroupPool = new Stack<List<BoardViewAction>>();

    /// <summary> 액션 그룹 사이 대기 시간 캐시 </summary>
    private static readonly WaitForSeconds _actionGroupDelay = new WaitForSeconds(0.019f);

    /// <summary> 현재 연출이 진행 중인지 여부 </summary>
    private bool _isAnimating = false;

    /// <summary> 드래그 연결 궤적을 표시하는 라인 렌더러 </summary>
    private LineRenderer _lineRenderer;

    /// <summary> LineRenderer에 사용되는 머티리얼 (해제 시 파괴 필요) </summary>
    private Material _lineMaterial;

    /// <summary> 링크 경로 위 연결 지점 표시용 포인트 오브젝트 풀 </summary>
    private List<GameObject> _linkPointMarkers = new List<GameObject>();

    /// <summary> 포인트 마커용 프로시저럴 원형 텍스처 </summary>
    private Texture2D _circleTexture;

    /// <summary> 포인트 마커용 스프라이트 </summary>
    private Sprite _circleSprite;

    /// <summary> LineRenderer 갱신 판단을 위한 이전 링크 경로 수 </summary>
    private int _prevLinkPathCount = 0;

    /// <summary> LineRenderer 갱신 판단을 위한 이전 링크 경로 마지막 좌표 </summary>
    private GridPos _prevLinkPathLast;


    /// <summary> 현재 보드가 애니메이션 연출 중인지 여부를 반환합니다. </summary>
    public bool IsAnimating => _isAnimating;

#if UNITY_EDITOR
    /// <summary> 디버그 기즈모용 캐싱된 GUIStyle (매 프레임 할당 방지) </summary>
    private static GUIStyle _debugStyle;

    /// <summary>
    /// 유니티 에디터의 씬 뷰에서 그리드 좌표와 블럭 정보를 텍스트로 표시합니다.
    /// </summary>
    private void OnDrawGizmos()
    {
        if (!showDebugGrid || _board == null || _board.Cells == null)
        {
            return;
        }

        if (_debugStyle == null)
        {
            _debugStyle = new GUIStyle();
            _debugStyle.normal.textColor = Color.yellow;
            _debugStyle.fontSize = 12;
            _debugStyle.alignment = TextAnchor.MiddleCenter;
        }
        GUIStyle style = _debugStyle;

        foreach (var kvp in _board.Cells)
        {
            GridPos pos = kvp.Key;
            PuzzleCell cell = kvp.Value;
            
            Transform root = cellRoot != null ? cellRoot : transform;
            Vector3 worldPos = root.TransformPoint(GetLocalPos(pos));
            
            string info = $"({pos.X},{pos.Y})";

            if (_blockViews.TryGetValue(pos, out PuzzleBlockView blockView))
            {
                info += $"\nID: {blockView.GetBlockData()?.GetBlockId() ?? "NullData"}";
            }
            else if (cell.Block != null)
            {
                if (_board.State == BoardState.Waiting)
                {
                    info += $"\nID: {cell.Block.GetBlockId()}\n[MISSING VIEW]";
                }
                else
                {
                    info += $"\nID: {cell.Block.GetBlockId()}\n[PROCESSING]";
                }
            }
            else if (cell.CellType == CellType.Generator)
            {
                info += "\n[GEN]";
            }
            else
            {
                info += "\n[EMPTY]";
            }

            UnityEditor.Handles.Label(worldPos, info, style);
            Gizmos.color = new Color(1, 1, 1, 0.2f);
            Gizmos.DrawWireCube(worldPos, new Vector3(cellSize * 0.9f, cellSize * 0.9f, 0.1f));
        }
    }
#endif

    private void OnDestroy()
    {
        StopAllCoroutines();
        _isAnimating = false;

        if (_lineMaterial != null)
        {
            Destroy(_lineMaterial);
            _lineMaterial = null;
        }
        if (_circleTexture != null)
        {
            Destroy(_circleTexture);
            _circleTexture = null;
        }
        if (_circleSprite != null)
        {
            Destroy(_circleSprite);
            _circleSprite = null;
        }
    }

    /// <summary>
    /// 보드 데이터를 바탕으로 셀과 블럭을 화면에 그립니다.
    /// </summary>
    /// <param name="boardData">그릴 보드 모델</param>
    /// <param name="boardShape">보드 모양 (null이면 StageInjection에서 조회)</param>
    public void DrawBoard(IPuzzleBoard boardData, BoardShape? boardShape = null)
    {
        _board = boardData;

        // 보드 모양 캐싱 (GetLocalPos에서 매번 StageInjection 조회 방지)
        if (boardShape.HasValue)
        {
            _cachedBoardShape = boardShape.Value;
        }
        else
        {
            GameSpec spec = StageInjection.Instance.GetGameSpec();
            _cachedBoardShape = spec?.rule.boardShape ?? BoardShape.None;
        }

        if (_board.Cells == null)
        {
            return;
        }

        _cellPrefabObj = AssetManager.Instance.LoadResource<GameObject>(cellAddress);
        _blockPrefabObj = AssetManager.Instance.LoadResource<GameObject>(blockAddress);

        if (_cellPrefabObj == null || _blockPrefabObj == null)
        {
            Debug.LogError($"[PuzzleBoardView] Failed to load prefabs. Cell: {cellAddress}, Block: {blockAddress}");
            return;
        }

        ClearBoard();
        SetupLineRenderer();

        if (cellRoot == null)
        {
            cellRoot = this.transform;
        }
        if (blockRoot == null)
        {
            blockRoot = this.transform;
        }

        foreach (var kvp in _board.Cells)
        {
            GridPos gridPos = kvp.Key;
            PuzzleCell cell = kvp.Value;

            CreateCellView(gridPos, cell);

            if (cell.Block != null)
            {
                CreateBlockView(gridPos, cell.Block);
            }
        }

        if (!skipCameraAlign)
        {
            AlignBoardToCenter();
        }
    }

    private void SetupLineRenderer()
    {
        if (_lineRenderer == null)
        {
            GameObject lineObj = new GameObject("LinkLine");
            lineObj.transform.SetParent(this.transform);
            _lineRenderer = lineObj.AddComponent<LineRenderer>();
            _lineRenderer.startWidth = 25f;
            _lineRenderer.endWidth = 25f;
            _lineRenderer.positionCount = 0;
            _lineRenderer.useWorldSpace = true;
            _lineRenderer.sortingLayerName = "Ingame";
            _lineRenderer.sortingOrder = 100;

            if (_lineMaterial != null)
            {
                Destroy(_lineMaterial);
            }

            Shader shader = Shader.Find("Sprites/Default");

            if (shader != null)
            {
                _lineMaterial = new Material(shader);
                _lineRenderer.material = _lineMaterial;
            }
            else
            {
                Debug.LogError("[PuzzleBoardView] LineRenderer용 셰이더를 찾을 수 없습니다.");
            }

            // 초기 색상은 흰색, 드래그 시 블럭 색상으로 동적 교체됨
            _lineRenderer.startColor = Color.white;
            _lineRenderer.endColor = Color.white;
        }
        _lineRenderer.positionCount = 0;

        // 포인트 마커용 원형 스프라이트 생성
        if (_circleSprite == null)
        {
            _circleTexture = CreateCircleTexture(64, Color.white);
            _circleSprite = Sprite.Create(
                _circleTexture,
                new Rect(0, 0, 64, 64),
                new Vector2(0.5f, 0.5f),
                1f
            );
        }
    }

    /// <summary>
    /// 프로시저럴 방식으로 원형 텍스처를 생성합니다.
    /// </summary>
    /// <param name="size">텍스처의 가로/세로 크기 (픽셀)</param>
    /// <param name="color">원의 색상</param>
    /// <returns>생성된 원형 텍스처</returns>
    private Texture2D CreateCircleTexture(int size, Color color)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        float center = size * 0.5f;
        float radius = center - 1f;

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center));
                if (dist <= radius)
                {
                    tex.SetPixel(x, y, color);
                }
                else
                {
                    tex.SetPixel(x, y, Color.clear);
                }
            }
        }

        tex.Apply();
        return tex;
    }

    public Vector3 GetLocalPos(GridPos pos)
    {
        if (_board == null)
        {
            return Vector3.zero;
        }

        float offsetX = (_board.Width - 1) * cellSize / 2f;
        float offsetY = (_board.Height - 1) * cellSize / 2f;

        float xPos = pos.X * cellSize - offsetX;
        float yPos = pos.Y * cellSize - offsetY;

        // 육각형(Hexagon) 오프셋 처리 (Even-Q Flat-Top)
        if (_cachedBoardShape == BoardShape.Hexagon)
        {
            if (pos.X % 2 == 0)
            {
                yPos -= cellSize * 0.5f; // 짝수 열은 반 칸 아래로 배치
            }
        }

        return new Vector3(xPos, yPos, 0);
    }

    private void AlignBoardToCenter()
    {
        if (_board == null)
        {
            return;
        }

        // 육각형 보드일 때 짝수 열의 반 칸 오프셋을 실제 보드 높이에 반영
        float hexOffsetHeight = 0f;
        if (_cachedBoardShape == BoardShape.Hexagon && _board.Width > 1)
        {
            hexOffsetHeight = cellSize * 0.5f;
        }

        float boardHeight = (_board.Height * cellSize) + hexOffsetHeight;
        float totalRequiredWidth = (_board.Width * cellSize) + (padding * 2f);
        float totalRequiredHeight = boardHeight + (padding * 2f);

        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            float screenAspect = (float)Screen.width / Screen.height;
            float sizeByHeight = totalRequiredHeight / 2f;
            float sizeByWidth = (totalRequiredWidth / 2f) / screenAspect;

            mainCamera.orthographicSize = Mathf.Max(sizeByHeight, sizeByWidth);
            mainCamera.transform.position = new Vector3(0, 0, -10f);
        }

        float finalY = 0f;
        if (mainCamera != null)
        {
            float camHeightHalf = mainCamera.orthographicSize;
            float boardHeightHalf = boardHeight / 2f;
            float availableSpace = camHeightHalf - boardHeightHalf - padding;
            finalY = offsetY * 2f * availableSpace;
        }

        transform.localPosition = new Vector3(0, finalY, 0);
    }

    private void ClearBoard()
    {
        StopAllCoroutines();

        foreach (var view in _cellViews.Values)
        {
            if (view != null)
            {
                PoolManager.Instance.Release(view.gameObject);
            }
        }

        foreach (var view in _blockViews.Values)
        {
            if (view != null)
            {
                PoolManager.Instance.Release(view.gameObject);
            }
        }

        _cellViews.Clear();
        _blockViews.Clear();

        while (_actionQueue.Count > 0)
        {
            ReleaseActionGroupList(_actionQueue.Dequeue());
        }

        _isAnimating = false;

        if (_lineRenderer != null)
        {
            _lineRenderer.positionCount = 0;
        }
    }

    private void CreateCellView(GridPos gridPos, PuzzleCell cellData)
    {
        if (_cellPrefabObj == null)
        {
            return;
        }

        GameObject cellObj = PoolManager.Instance.Get(_cellPrefabObj, cellRoot);
        cellObj.transform.localPosition = GetLocalPos(gridPos);
        cellObj.name = $"Cell_{gridPos.X}_{gridPos.Y}";

        PuzzleCellView cellView = cellObj.GetComponent<PuzzleCellView>();
        if (cellView == null)
        {
            cellView = cellObj.AddComponent<PuzzleCellView>();
        }

        cellView.Initialize(cellData, gridPos, this, _cachedBoardShape);
        _cellViews[gridPos] = cellView;
    }

    private void CreateBlockView(GridPos gridPos, Block blockData)
    {
        if (_blockPrefabObj == null)
        {
            return;
        }

        GameObject blockObj = PoolManager.Instance.Get(_blockPrefabObj, blockRoot);
        blockObj.transform.localPosition = GetLocalPos(gridPos);
        blockObj.name = $"Block_{gridPos.X}_{gridPos.Y}";

        PuzzleBlockView blockView = blockObj.GetComponent<PuzzleBlockView>();
        if (blockView != null)
        {
            blockView.Initialize(blockData, gridPos, this);
            _blockViews[gridPos] = blockView;
        }
    }

    private void Update()
    {
        if (_board == null)
        {
            return;
        }

        UpdateLineRenderer();

        foreach (var kvp in _blockViews)
        {
            if (kvp.Value != null)
            {
                kvp.Value.UpdateStateVisual();
            }
        }

        List<BoardViewAction> actions = _board.FetchActions();
        if (actions != null && actions.Count > 0)
        {
            // LINQ 대신 수동 그룹화로 GC 할당 방지
            GroupActionsByFrameAndOrder(actions);
        }

        if (!_isAnimating && _actionQueue.Count > 0)
        {
            StartCoroutine(ProcessActionQueue());
        }
    }

    private void UpdateLineRenderer()
    {
        if (_lineRenderer == null)
        {
            return;
        }

        if (_board is LinkPuzzleBoard linkBoard)
        {
            IReadOnlyList<GridPos> path = linkBoard.GetCurrentLinkPath();
            if (path != null && path.Count > 0)
            {
                // 경로가 변경되지 않았으면 갱신 생략
                GridPos lastPos = path[path.Count - 1];
                if (path.Count == _prevLinkPathCount && lastPos == _prevLinkPathLast)
                {
                    return;
                }
                _prevLinkPathCount = path.Count;
                _prevLinkPathLast = lastPos;

                // LineRenderer는 최소 2개 포인트가 필요하므로,
                // 포인트가 1개일 때는 같은 위치에 2개를 찍어 점으로 표시
                int pointCount = Mathf.Max(path.Count, 2);
                _lineRenderer.positionCount = pointCount;

                for (int i = 0; i < path.Count; i++)
                {
                    Vector3 localPos = GetLocalPos(path[i]);
                    localPos.z = -1f;
                    Vector3 worldPos = this.transform.TransformPoint(localPos);
                    _lineRenderer.SetPosition(i, worldPos);
                }

                // 포인트가 1개인 경우 두 번째 포인트를 동일 위치에 배치
                if (path.Count == 1)
                {
                    _lineRenderer.SetPosition(1, _lineRenderer.GetPosition(0));
                }

                // 각 연결 지점에 포인트 마커 표시
                UpdateLinkPointMarkers(path);
            }
            else
            {
                if (_prevLinkPathCount != 0)
                {
                    _prevLinkPathCount = 0;
                    _lineRenderer.positionCount = 0;
                    HideAllLinkPointMarkers();
                }
            }
        }
        else
        {
            if (_prevLinkPathCount != 0)
            {
                _prevLinkPathCount = 0;
                _lineRenderer.positionCount = 0;
                HideAllLinkPointMarkers();
            }
        }
    }

    /// <summary>
    /// 링크 경로의 첫 번째 블럭 스프라이트에서 대표 색상을 추출합니다.
    /// </summary>
    /// <param name="path">현재 링크 경로 좌표 리스트</param>
    /// <returns>블럭의 대표 색상 (추출 실패 시 반투명 흰색)</returns>

    /// <summary>
    /// 링크 경로의 각 연결 지점에 원형 포인트 마커를 표시합니다.
    /// </summary>
    /// <param name="path">현재 링크 경로 좌표 리스트</param>
    private void UpdateLinkPointMarkers(IReadOnlyList<GridPos> path)
    {
        // 필요한 만큼 마커 오브젝트 확보
        while (_linkPointMarkers.Count < path.Count)
        {
            GameObject marker = new GameObject("LinkPoint");
            marker.transform.SetParent(this.transform);

            SpriteRenderer sr = marker.AddComponent<SpriteRenderer>();
            sr.sprite = _circleSprite;
            sr.sortingLayerName = "Ingame";
            sr.sortingOrder = 101;
            sr.color = Color.white;

            marker.SetActive(false);
            _linkPointMarkers.Add(marker);
        }

        // 경로에 맞춰 마커 위치, 색상 지정 및 활성화
        for (int i = 0; i < path.Count; i++)
        {
            GameObject marker = _linkPointMarkers[i];
            Vector3 localPos = GetLocalPos(path[i]);
            localPos.z = -2f;
            marker.transform.localPosition = localPos;

            marker.SetActive(true);
        }

        // 남는 마커는 비활성화
        for (int i = path.Count; i < _linkPointMarkers.Count; i++)
        {
            _linkPointMarkers[i].SetActive(false);
        }
    }

    /// <summary>
    /// 모든 포인트 마커를 비활성화합니다.
    /// </summary>
    private void HideAllLinkPointMarkers()
    {
        for (int i = 0; i < _linkPointMarkers.Count; i++)
        {
            _linkPointMarkers[i].SetActive(false);
        }
    }

    /// <summary>
    /// 뷰 액션 리스트를 (frame, orderIndex) 기준으로 그룹화하여 큐에 넣습니다.
    /// LINQ GroupBy/OrderBy 대신 수동 정렬/그룹화로 GC 할당을 방지합니다.
    /// </summary>
    /// <param name="actions">그룹화할 뷰 액션 리스트 (정렬 완료 상태)</param>
    private void GroupActionsByFrameAndOrder(List<BoardViewAction> actions)
    {
        // FetchActions()에서 이미 frame → orderIndex 순으로 정렬되어 반환됨
        List<BoardViewAction> currentGroup = GetActionGroupList();
        uint prevFrame = actions[0].frame;
        uint prevOrder = actions[0].orderIndex;

        for (int i = 0; i < actions.Count; i++)
        {
            BoardViewAction action = actions[i];
            if (action.frame != prevFrame || action.orderIndex != prevOrder)
            {
                _actionQueue.Enqueue(currentGroup);
                currentGroup = GetActionGroupList();
                prevFrame = action.frame;
                prevOrder = action.orderIndex;
            }
            currentGroup.Add(action);
        }

        if (currentGroup.Count > 0)
        {
            _actionQueue.Enqueue(currentGroup);
        }
    }

    /// <summary>
    /// 액션 그룹 리스트를 풀에서 가져옵니다.
    /// </summary>
    private List<BoardViewAction> GetActionGroupList()
    {
        if (_actionGroupPool.Count > 0)
        {
            return _actionGroupPool.Pop();
        }

        return new List<BoardViewAction>();
    }

    /// <summary>
    /// 사용이 끝난 액션 그룹 리스트를 비우고 재사용 풀에 반환합니다.
    /// </summary>
    /// <param name="group">반환할 액션 그룹 리스트</param>
    private void ReleaseActionGroupList(List<BoardViewAction> group)
    {
        if (group == null)
        {
            return;
        }

        group.Clear();
        _actionGroupPool.Push(group);
    }

    /// <summary> ProcessActionQueue 내부에서 이동 액션을 재사용하는 임시 리스트 </summary>
    private readonly List<BoardViewAction> _tempMovementActions = new List<BoardViewAction>();

    /// <summary> ProcessActionQueue 내부에서 기타 액션을 재사용하는 임시 리스트 </summary>
    private readonly List<BoardViewAction> _tempOtherActions = new List<BoardViewAction>();

    private IEnumerator ProcessActionQueue()
    {
        _isAnimating = true;

        while (_actionQueue.Count > 0)
        {
            List<BoardViewAction> actionGroup = _actionQueue.Dequeue();

            // LINQ 대신 수동 분류로 GC 할당 방지
            _tempMovementActions.Clear();
            _tempOtherActions.Clear();
            for (int i = 0; i < actionGroup.Count; i++)
            {
                BoardViewAction action = actionGroup[i];
                if (action.type == ViewType.Move || action.type == ViewType.Fall || action.type == ViewType.CreateAndFall)
                {
                    _tempMovementActions.Add(action);
                }
                else
                {
                    _tempOtherActions.Add(action);
                }
            }

            if (_tempMovementActions.Count > 0)
            {
                yield return StartCoroutine(ExecuteBatchMovement(_tempMovementActions));
            }

            if (_tempOtherActions.Count > 0)
            {
                int completedCount = 0;
                int totalCount = _tempOtherActions.Count;

                for (int i = 0; i < _tempOtherActions.Count; i++)
                {
                    ExecuteSingleAction(_tempOtherActions[i], () => completedCount++);
                }

                while (completedCount < totalCount)
                {
                    yield return null;
                }
            }

            ReleaseActionGroupList(actionGroup);
            yield return _actionGroupDelay;
        }

        _isAnimating = false;
    }

    /// <summary> ExecuteBatchMovement 내부에서 액션-뷰 매핑을 재사용하는 임시 리스트 (Dictionary 할당 방지) </summary>
    private readonly List<BoardViewAction> _batchActions = new List<BoardViewAction>();
    private readonly List<PuzzleBlockView> _batchViews = new List<PuzzleBlockView>();

    private System.Collections.IEnumerator ExecuteBatchMovement(List<BoardViewAction> moveActions)
    {
        int completedCount = 0;
        int totalCount = moveActions.Count;

        _batchActions.Clear();
        _batchViews.Clear();

        // Move/Fall을 먼저 처리하여 기존 뷰를 _blockViews에서 제거한 뒤,
        // CreateAndFall을 처리해야 HandleImmediateDestroy가 이동 예정 블럭을 파괴하지 않음
        for (int i = 0; i < moveActions.Count; i++)
        {
            BoardViewAction action = moveActions[i];
            if (action.type == ViewType.Move || action.type == ViewType.Fall)
            {
                if (_blockViews.TryGetValue(action.position, out PuzzleBlockView view))
                {
                    _batchActions.Add(action);
                    _batchViews.Add(view);
                    _blockViews.Remove(action.position);
                }
            }
        }

        for (int i = 0; i < moveActions.Count; i++)
        {
            BoardViewAction action = moveActions[i];
            if (action.type == ViewType.CreateAndFall)
            {
                if (action.blockData != null && _blockPrefabObj != null)
                {
                    if (_blockViews.ContainsKey(action.targetPosition))
                    {
                        HandleImmediateDestroy(action.targetPosition);
                    }

                    GameObject blockObj = PoolManager.Instance.Get(_blockPrefabObj, blockRoot);
                    blockObj.transform.localPosition = GetLocalPos(action.position);
                    blockObj.name = $"Block_{action.targetPosition.X}_{action.targetPosition.Y}";

                    PuzzleBlockView bView = blockObj.GetComponent<PuzzleBlockView>();
                    if (bView != null)
                    {
                        bView.Initialize(action.blockData, action.position, this);
                        _batchActions.Add(action);
                        _batchViews.Add(bView);
                    }
                }
            }
        }

        for (int i = 0; i < _batchActions.Count; i++)
        {
            BoardViewAction action = _batchActions[i];
            PuzzleBlockView view = _batchViews[i];
            GridPos to = action.targetPosition;

            _blockViews[to] = view;

            Vector3 targetPos = GetLocalPos(to);
            System.Action onComplete = () =>
            {
                view.Initialize(view.GetBlockData(), to, this);
                completedCount++;
            };

            if (action.type == ViewType.Move)
            {
                view.PlayMoveAnimation(targetPos, onComplete);
            }
            else
            {
                view.PlayFallAnimation(targetPos, onComplete);
            }
        }

        int processedCount = _batchActions.Count;
        if (processedCount < totalCount)
        {
            completedCount += (totalCount - processedCount);
        }

        while (completedCount < totalCount)
        {
            yield return null;
        }
    }

    private void ExecuteSingleAction(BoardViewAction action, System.Action onComplete)
    {
        switch (action.type)
        {
            case ViewType.Destroy:
                if (_blockViews.TryGetValue(action.position, out PuzzleBlockView dView))
                {
                    _blockViews.Remove(action.position);
                    dView.PlayDestroyAnimation(() => 
                    {
                        PoolManager.Instance.Release(dView.gameObject);
                        onComplete?.Invoke();
                    });
                }
                else
                {
                    onComplete?.Invoke();
                }
                break;

            case ViewType.Create:
                if (action.blockData != null)
                {
                    if (_blockViews.ContainsKey(action.position))
                    {
                        HandleImmediateDestroy(action.position);
                    }

                    if (_blockPrefabObj != null)
                    {
                        GameObject blockObj = PoolManager.Instance.Get(_blockPrefabObj, blockRoot);
                        blockObj.transform.localPosition = GetLocalPos(action.position);
                        blockObj.name = $"Block_{action.position.X}_{action.position.Y}";

                        PuzzleBlockView bView = blockObj.GetComponent<PuzzleBlockView>();
                        if (bView != null)
                        {
                            bView.Initialize(action.blockData, action.position, this);
                            _blockViews[action.position] = bView;
                            bView.PlayCreateAnimation(onComplete);
                        }
                        else
                        {
                            onComplete?.Invoke();
                        }
                    }
                    else
                    {
                        onComplete?.Invoke();
                    }
                }
                else
                {
                    onComplete?.Invoke();
                }
                break;

            default:
                onComplete?.Invoke();
                break;
        }
    }

    private void HandleImmediateDestroy(GridPos pos)
    {
        if (_blockViews.TryGetValue(pos, out PuzzleBlockView view))
        {
            _blockViews.Remove(pos);
            if (view != null && view.gameObject != null)
            {
                view.transform.localScale = Vector3.one;
                PoolManager.Instance.Release(view.gameObject);
            }
        }
    }

    public void OnBlockInput(GridPos pos)
    {
        if (_board != null)
        {
            _board.Input(pos);
        }
    }

    public void RefreshBlocks()
    {
        if (_board == null)
        {
            return;
        }

        foreach (var view in _blockViews.Values)
        {
            if (view != null && view.gameObject != null)
            {
                PoolManager.Instance.Release(view.gameObject);
            }
        }
        _blockViews.Clear();

        foreach (var kvp in _board.Cells)
        {
            GridPos gridPos = kvp.Key;
            PuzzleCell cell = kvp.Value;
            if (cell.Block != null)
            {
                CreateBlockView(gridPos, cell.Block);
            }
        }
    }
}
