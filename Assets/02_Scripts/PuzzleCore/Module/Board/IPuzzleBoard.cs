using System.Collections.Generic;

namespace Puzzle.Core
{
    /// <summary>
    /// 보드에서 발생한 상태 변화(매치, 파괴, 이동 등)를 화면(View)에 요청하기 위해 기록하는 데이터 클래스입니다.
    /// </summary>
    public class BoardViewAction
    {
        /// <summary> 해당 연출이 발생한 보드 로직 프레임 (리플레이용) </summary>
        public uint frame;
        /// <summary> 연출이 보여질 순서 번호 (뷰 연출용) </summary>
        public uint orderIndex;
        /// <summary> 연출의 종류 (파괴, 생성, 이동 등) </summary>
        public ViewType type;
        /// <summary> 주가 되는 좌표 (생성 위치, 파괴 위치 등) </summary>
        public GridPos position;
        /// <summary> 이동 시 목적지 좌표 </summary>
        public GridPos targetPosition;
        /// <summary> 연출에 관여하는 블럭 데이터 (생성 등에서 사용) </summary>
        public Block blockData;
    }

    /// <summary>
    /// 퍼즐 보드의 핵심 로직을 정의하는 인터페이스입니다.
    /// 게임 타입(매치3, 육각형, 링크 등)에 따라 다르게 구현될 수 있습니다.
    /// </summary>
    public interface IPuzzleBoard
    {
        /// <summary> 보드의 현재 논리적 상태 </summary>
        BoardState State { get; }

        /// <summary> 게임 내 공용 난수 생성기 </summary>
        PuzzleRandom Random { get; }

        /// <summary> 스테이지 목표 관리자 </summary>
        ObjectiveManager Objective { get; }

        /// <summary> 좌표별 셀 데이터를 저장하는 딕셔너리 </summary>
        Dictionary<GridPos, PuzzleCell> Cells { get; }

        /// <summary> 보드의 가로 너비 </summary>
        int Width { get; }

        /// <summary> 보드의 세로 높이 </summary>
        int Height { get; }

        /// <summary> 게임 사양서를 바탕으로 보드를 초기화합니다. </summary>
        void Initialize(GameSpec spec);

        /// <summary> 외부에서 입력 좌표를 전달받아 처리 대기열에 추가합니다. </summary>
        void Input(GridPos input);

        /// <summary> 대기열에 쌓인 입력들을 모두 처리합니다. </summary>
        bool InputEnd();

        /// <summary> 매 프레임마다 보드의 상태를 업데이트합니다. </summary>
        void Update();

        /// <summary> 고정 프레임 간격으로 논리 프레임 및 결정론적 로직을 업데이트합니다. </summary>
        void FixedUpdate();

        /// <summary> 보드의 로직 처리를 일시 정지하거나 재개합니다. </summary>
        void Pause(bool pause);

        /// <summary> 화면 연출을 위해 발생한 상태 변화를 추가합니다. </summary>
        public void AddView(BoardViewAction view, uint? customOrder = null);

        /// <summary> 발생한 View 액션 리스트를 가져오고 보드 내역을 비웁니다. (소비형) </summary>
        List<BoardViewAction> FetchActions();

        /// <summary> 지금까지 기록된 유저의 모든 조작 내역을 반환합니다. (리플레이용) </summary>
        List<InputRecord> GetRecordedInputs();

        /// <summary> 지금까지 기록된 유저의 모든 입력 종료 내역을 반환합니다. (리플레이용) </summary>
        List<InputEndRecord> GetRecordedInputEnds();

        /// <summary> 지정된 좌표에 해당하는 셀 객체를 가져옵니다. </summary>
        PuzzleCell GetCell(GridPos pos);
    }
}
