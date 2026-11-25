using System;

namespace Puzzle.Core
{
    /// <summary>
    /// 게임 내에서 공유되는 결정론적 난수 생성 클래스입니다.
    /// 리플레이 기능을 위해 특정 시드(Seed)와 상태를 유지합니다.
    /// </summary>
    public class PuzzleRandom
    {
        /// <summary> 내부 난수 생성 객체 </summary>
        private Random _random;

        /// <summary> 설정된 시드값 </summary>
        private int _seed;

        /// <summary>
        /// 특정 시드값을 사용하여 난수 생성기를 초기화합니다.
        /// </summary>
        /// <param name="seed">난수 시드</param>
        public PuzzleRandom(int seed)
        {
            _seed = seed;
            _random = new Random(_seed);
        }

        /// <summary>
        /// 지정된 범위 내의 정수 난수를 반환합니다.
        /// </summary>
        /// <param name="minValue">최솟값 (포함)</param>
        /// <param name="maxValue">최댓값 (미포함)</param>
        /// <returns>랜덤 정수</returns>
        public int Next(int minValue, int maxValue) => _random.Next(minValue, maxValue);

        /// <summary>
        /// 0.0 이상 1.0 미만의 부동 소수점 난수를 반환합니다.
        /// </summary>
        /// <returns>랜덤 소수</returns>
        public double NextDouble() => _random.NextDouble();

        /// <summary>
        /// 시드를 재설정하여 난수 생성기를 초기화합니다.
        /// </summary>
        /// <param name="seed">새로운 난수 시드</param>
        public void SetSeed(int seed)
        {
            _seed = seed;
            _random = new Random(_seed);
        }
    }
}
