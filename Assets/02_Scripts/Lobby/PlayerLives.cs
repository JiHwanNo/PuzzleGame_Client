using System;
using System.Globalization;
using UnityEngine;

/// <summary>라이프가 모두 충전될 UTC 시각을 기기에 저장해 오프라인 충전도 반영합니다.</summary>
internal static class PlayerLives
{
    internal const int Capacity = 5;
    private const int RechargeSeconds = 30 * 60;
    private const string FullAtKey = "StageLives.FullAtUtc";

    /// <summary><paramref name="now"/> UTC 초를 기준으로 잔량과 다음 충전까지의 시간을 계산합니다.</summary>
    /// <param name="secondsToNext">가득 찼으면 0, 아니면 다음 충전까지 남은 초입니다.</param>
    internal static int GetRemaining(long now, out int secondsToNext)
    {
        long.TryParse(PlayerPrefs.GetString(FullAtKey, "0"), NumberStyles.Integer,
            CultureInfo.InvariantCulture, out long fullAt);
        long debt = Math.Max(0L, Math.Min((long)Capacity * RechargeSeconds, fullAt - now));
        secondsToNext = 0;
        if (debt == 0)
        {
            return Capacity;
        }
        secondsToNext = (int)((debt - 1) % RechargeSeconds) + 1;
        return Capacity - (int)((debt + RechargeSeconds - 1) / RechargeSeconds);
    }

    /// <summary>한 개를 사용하면서 진행 중인 충전의 남은 시간을 보존합니다.</summary>
    /// <param name="now">사용 시점의 UTC 초입니다.</param>
    internal static bool TrySpend(long now)
    {
        int remaining = GetRemaining(now, out int secondsToNext);
        if (remaining == 0)
        {
            return false;
        }
        int missing = Capacity - remaining;
        long fullAt = now + Math.Max(1, missing) * RechargeSeconds + secondsToNext;
        PlayerPrefs.SetString(FullAtKey, fullAt.ToString(CultureInfo.InvariantCulture));
        PlayerPrefs.Save();
        return true;
    }
}
