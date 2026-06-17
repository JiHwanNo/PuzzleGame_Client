using System;
using System.Collections.Generic;

namespace Puzzle.Core
{
    /// <summary>
    /// 퍼즐 보드 내의 2차원 좌표를 나타내는 구조체입니다.
    /// </summary>
    [Serializable]
    public struct GridPos : IEquatable<GridPos>
    {
        /// <summary> 가로 좌표 (X축) </summary>
        public int X;
        /// <summary> 세로 좌표 (Y축) </summary>
        public int Y;

        public GridPos(int x, int y)
        {
            X = x;
            Y = y;
        }

        // --- 방향 상수 정의 ---
        public static readonly GridPos Up = new GridPos(0, 1);
        public static readonly GridPos Down = new GridPos(0, -1);
        public static readonly GridPos Left = new GridPos(-1, 0);
        public static readonly GridPos Right = new GridPos(1, 0);

        public static readonly GridPos UpRight = new GridPos(1, 1);
        public static readonly GridPos UpLeft = new GridPos(-1, 1);
        public static readonly GridPos DownRight = new GridPos(1, -1);
        public static readonly GridPos DownLeft = new GridPos(-1, -1);

        /// <summary>
        /// 일반 사각형 그리드에서 두 좌표가 상하좌우로 인접해 있는지 확인합니다.
        /// </summary>
        public static bool IsAdjacentSquare(GridPos a, GridPos b)
        {
            int dx = Math.Abs(a.X - b.X);
            int dy = Math.Abs(a.Y - b.Y);
            return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
        }

        /// <summary>
        /// Flat-Top(Even-Q) 육각형 그리드에서 두 좌표가 6방향으로 인접해 있는지 확인합니다.
        /// 짝수 열(Column)이 반 칸 아래로 내려가 있는 형태를 기준으로 합니다.
        /// </summary>
        public static bool IsAdjacentHexagon(GridPos a, GridPos b)
        {
            int dx = b.X - a.X;
            int dy = b.Y - a.Y;

            if (dx == 0) // 같은 열(위, 아래)
            {
                return Math.Abs(dy) == 1;
            }
            else if (Math.Abs(dx) == 1) // 인접한 열 (좌상, 좌하, 우상, 우하)
            {
                if (a.X % 2 == 0)
                {
                    // 짝수 열: 인접 열의 이웃은 Y좌표가 같거나 1 작습니다.
                    return dy == 0 || dy == -1;
                }
                else
                {
                    // 홀수 열: 인접 열의 이웃은 Y좌표가 1 크거나 같습니다.
                    return dy == 1 || dy == 0;
                }
            }
            return false;
        }

        // --- 연산자 오버로딩 ---
        public static GridPos operator +(GridPos a, GridPos b)
        {
            return new GridPos(a.X + b.X, a.Y + b.Y);
        }

        public static GridPos operator -(GridPos a, GridPos b)
        {
            return new GridPos(a.X - b.X, a.Y - b.Y);
        }

        public static bool operator ==(GridPos a, GridPos b)
        {
            return a.X == b.X && a.Y == b.Y;
        }

        public static bool operator !=(GridPos a, GridPos b)
        {
            return !(a == b);
        }

        public bool Equals(GridPos other)
        {
            return X == other.X && Y == other.Y;
        }

        public override bool Equals(object obj)
        {
            return obj is GridPos other && Equals(other);
        }

        public override int GetHashCode()
        {
            // 구버전 닷넷 호환성을 고려한 해시 생성
            unchecked
            {
                return (X * 397) ^ Y;
            }
        }

        public override string ToString()
        {
            return $"({X}, {Y})";
        }
    }

    /// <summary>
    /// 규칙 JSON 파일을 파싱하기 위한 컨테이너 클래스입니다.
    /// </summary>
    [Serializable]
    public class GameRuleContainer
    {
        /// <summary> 기본 규칙 데이터 </summary>
        public RuleData rule;
        /// <summary> 블럭 정보 리스트 </summary>
        public List<BlockData> blocks;
    }

    /// <summary>
    /// 개별 게임 규칙(매치 방식 등)을 정의하는 데이터 구조체입니다.
    /// </summary>
    [Serializable]
    public struct RuleData
    {
        /// <summary> 규칙 고유 아이디 </summary>
        public string ruleId;
        /// <summary> 퍼즐 매칭 방식 </summary>
        public PuzzleType puzzleType;
        /// <summary> 보드 타일 모양 </summary>
        public BoardShape boardShape;
        /// <summary> 스테이지 제한 시간 (초 단위, 0이면 무제한) </summary>
        public float timeLimit;
        /// <summary> 스테이지 클리어 목표 목록 </summary>
        public List<ObjectiveData> objectives;
    }

    /// <summary>
    /// 스테이지 클리어를 위해 달성해야 하는 개별 목표 데이터입니다.
    /// </summary>
    [Serializable]
    public struct ObjectiveData
    {
        /// <summary> 목표 종류 (점수, 블럭 수집 등) </summary>
        public ObjectiveType type;
        /// <summary> 목표 대상 (특정 블럭 ID 등) </summary>
        public string targetId;
        /// <summary> 달성해야 하는 목표 수치 </summary>
        public int count;
    }

    /// <summary>
    /// 게임 클리어 목표의 종류를 정의합니다.
    /// </summary>
    public enum ObjectiveType
    {
        /// <summary> 특정 점수 도달 </summary>
        Score = 0,
        /// <summary> 특정 ID의 블럭 수집(파괴) </summary>
        CollectBlock = 1,
        /// <summary> 특정 셀 모두 제거 </summary>
        ClearCell = 2
    }

    /// <summary>
    /// 블럭의 고유 속성 데이터를 담는 클래스입니다.
    /// </summary>
    [Serializable]
    public class BlockData
    {
        /// <summary> 블럭 고유 아이디 </summary>
        public string blockId;
        /// <summary> 조작 방식 (JSON에는 문자열 목록, 예: ["Swap","Touch"]. 게임 로직에서 InputType 플래그로 변환) </summary>
        public List<string> inputType;
        /// <summary> 파괴 조건 </summary>
        public DestroyType destroyType;
        /// <summary> 내구도/생명력 </summary>
        public int life;
        /// <summary> 이 블럭에 연동할 기믹 id 목록 (없거나 비어있으면 기믹 없음) </summary>
        public List<string> gimmickIds;
    }

    // ==========================================================
    // 열거형(Enum) 정의
    // ==========================================================

    /// <summary> 퍼즐의 핵심 매칭 로직 타입 </summary>
    public enum PuzzleType
    {
        None = 0,
        /// <summary> 3매치 방식 </summary>
        ThreeMatch = 1,
        /// <summary> 선 긋기 방식 </summary>
        Link = 2,
        /// <summary> 터치 매치(콜랩스) 방식 </summary>
        TapMatch = 3
    }

    /// <summary> 보드 타일의 기하학적 모양 </summary>
    public enum BoardShape
    {
        None = 0,
        /// <summary> 사각형 </summary>
        Quadrangle = 1,
        /// <summary> 육각형 </summary>
        Hexagon = 2
    }

    /// <summary> 게임 오버 조건 </summary>
    public enum GameOverCondition
    {
        None = 0,
        /// <summary> 남은 턴수 제한 </summary>
        TurnLimit = 1,
        /// <summary> 제한 시간 종료 </summary>
        TimeLimit = 2
    }

    /// <summary> 스테이지 클리어 조건 </summary>
    public enum ClearCondition
    {
        None = 0,
        /// <summary> 특정 목표 블럭 수집 </summary>
        GetTargetBlocks = 1,
        /// <summary> 목표 점수 도달 </summary>
        ScoreTarget = 2
    }

    /// <summary> 블럭의 카테고리 분류 </summary>
    public enum BlockType
    {
        None = 0,
        /// <summary> 일반 블럭 </summary>
        Normal = 100,
        /// <summary> 아이템 블럭 </summary>
        Item = 110,
        /// <summary> 목표 지점/특수 대상 </summary>
        Target = 200
    }

    /// <summary> 셀의 속성 및 상태 </summary>
    public enum CellType
    {
        /// <summary> 막힌 구역 </summary>
        Close = 0,
        /// <summary> 일반 바닥 </summary>
        Normal = 1,
        /// <summary> 로직상 잠긴 상태 </summary>
        Lock = 2,
        /// <summary> 블럭 생성기 </summary>
        Generator = 3
    }

    /// <summary> 
    /// 유저의 조작 방식 (다중 선택 가능)
    /// </summary>
    [Flags]
    public enum InputType
    {
        None = 0,
        /// <summary> 위치 바꾸기 </summary>
        Swap = 1 << 0,
        /// <summary> 연결하기 </summary>
        Link = 1 << 1,
        /// <summary> 터치(클릭)하기 </summary>
        Touch = 1 << 2
    }

    /// <summary> 블럭 파괴의 원인 및 방식 </summary>
    public enum DestroyType
    {
        None = 0,
        /// <summary> 2개 매치 파괴 </summary>
        Two_Match = 1,
        /// <summary> 3개 매치 파괴 </summary>
        Three_Match = 2,
        /// <summary> 주변 폭발 여파 </summary>
        Splash = 50,
        /// <summary> 폭탄 직접 파괴 </summary>
        Bomb = 51
    }

    /// <summary> 블럭/판넬에 부착되는 기믹의 종류 (JSON에는 문자열, 내부 처리는 이 enum) </summary>
    public enum GimmickType
    {
        /// <summary> 없음 </summary>
        None = 0,
        /// <summary> 원형 폭탄 (파괴 시 주변 반경을 함께 파괴) </summary>
        Bomb = 1
    }

    /// <summary> 방향 정의 </summary>
    public enum Direction
    {
        None = 0, Up, Down, Left, Right, UpLeft, UpRight, DownLeft, DownRight
    }

    /// <summary> 보드의 논리적 처리 상태 </summary>
    public enum BoardState
    {
        /// <summary> 입력 대기 </summary>
        Waiting = 0,
        /// <summary> 매칭 판정 및 파괴 처리 중 </summary>
        Matching = 1,
        /// <summary> 블럭 낙하 중 </summary>
        Falling = 2,
        /// <summary> 새 블럭 보충 중 </summary>
        Filling = 3,
        /// <summary> 스테이지 종료 </summary>
        Finish = 4
    }

    /// <summary> 시각적 연출의 종류 </summary>
    public enum ViewType
    {
        None = 0,
        /// <summary> 파괴 연출 </summary>
        Destroy,
        /// <summary> 생성 연출 </summary>
        Create,
        /// <summary> 이동 연출 </summary>
        Move,
        /// <summary> 착지 연출 </summary>
        Land,
        /// <summary> 낙하 연출 </summary>
        Fall,
        /// <summary> 생성 및 낙하 연출 </summary>
        CreateAndFall
    }

    /// <summary> 개별 블럭의 논리적 상태 </summary>
    public enum BlockState
    {
        /// <summary> 대기 상태 </summary>
        Idle = 0,
        /// <summary> 유저에 의해 선택됨 </summary>
        Selected = 1,
        /// <summary> 위치 이동/스왑 중 </summary>
        Moving = 2,
        /// <summary> 매칭되어 파괴 대기 중 </summary>
        Matched = 3,
        /// <summary> 낙하 중 </summary>
        Falling = 4,
        /// <summary> 비활성화/파괴됨 </summary>
        None = 5
    }

    /// <summary>
    /// 리플레이를 위해 특정 프레임에 발생한 유저의 조작 정보를 기록하는 구조체입니다.
    /// </summary>
    [Serializable]
    public struct InputRecord
    {
        /// <summary> 조작이 발생한 게임 로직 프레임 </summary>
        public ulong frame;
        /// <summary> 클릭/터치된 그리드 좌표 </summary>
        public GridPos position;

        /// <summary>
        /// 입력 기록을 생성합니다.
        /// </summary>
        /// <param name="frame">조작이 발생한 프레임</param>
        /// <param name="pos">클릭/터치된 그리드 좌표</param>
        public InputRecord(ulong frame, GridPos pos)
        {
            this.frame = frame;
            this.position = pos;
        }
    }

    /// <summary>
    /// 리플레이를 위해 유저가 입력을 종료(포인터 릴리즈)한 프레임을 기록하는 구조체입니다.
    /// </summary>
    [Serializable]
    public struct InputEndRecord
    {
        /// <summary> 입력 종료가 발생한 게임 로직 프레임 </summary>
        public ulong frame;

        /// <summary>
        /// 입력 종료 기록을 생성합니다.
        /// </summary>
        /// <param name="frame">입력 종료가 발생한 프레임</param>
        public InputEndRecord(ulong frame)
        {
            this.frame = frame;
        }
    }
}
