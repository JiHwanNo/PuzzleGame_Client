namespace Puzzle.Core
{
    /// <summary>
    /// 퍼즐 셀의 바닥판(Panel) 역할을 수행하는 추상 클래스입니다.
    /// 얼음 타일, 이동 방해 타일 등 특수한 바닥 속성을 정의할 때 상속받아 사용합니다.
    /// </summary>
    public abstract class PuzzlePanel
    {
        /// <summary> 바닥판의 고유 타입 </summary>
        public string PanelType { get; protected set; }

        /// <summary>
        /// 새로운 바닥판을 특정 타입으로 초기화합니다.
        /// </summary>
        /// <param name="type">타입 이름</param>
        public PuzzlePanel(string type)
        {
            PanelType = type;
        }

        /// <summary>
        /// 매 프레임마다 바닥판의 상태를 업데이트합니다.
        /// </summary>
        internal virtual void Update()
        {
        }
    }

    /// <summary>
    /// 아무런 특수 기능이 없는 기본적인 보드 바닥판입니다.
    /// </summary>
    public class NormalPanel : PuzzlePanel
    {
        /// <summary> 특수 기능이 없는 기본 바닥판을 생성합니다. </summary>
        public NormalPanel() : base("Normal")
        {
        }
    }
}
