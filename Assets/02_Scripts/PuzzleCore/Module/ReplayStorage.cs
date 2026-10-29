using System;
using System.IO;
using UnityEngine;

namespace Puzzle.Core
{
    /// <summary>
    /// 리플레이 데이터를 로컬 파일 시스템에 JSON 형식으로 저장하고 불러오는 유틸리티 클래스입니다.
    /// 저장 경로: Application.persistentDataPath/Replay/
    /// </summary>
    public static class ReplayStorage
    {
        /// <summary> 리플레이 파일이 저장되는 디렉터리 이름 </summary>
        private const string ReplayDirectory = "Replay";

        /// <summary>
        /// 리플레이 데이터를 JSON 파일로 저장합니다.
        /// </summary>
        /// <param name="replayData">저장할 리플레이 데이터</param>
        /// <returns>저장된 파일의 전체 경로. 실패 시 null</returns>
        public static string Save(ReplayData replayData)
        {
            try
            {
                string directoryPath = GetReplayDirectoryPath();
                if (!Directory.Exists(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath);
                }

                string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                string fileName = $"replay_{timestamp}.json";
                string filePath = Path.Combine(directoryPath, fileName);

                string json = JsonUtility.ToJson(replayData, true);
                File.WriteAllText(filePath, json);

                Debug.Log($"리플레이 저장 완료: {filePath}");
                return filePath;
            }
            catch (Exception e)
            {
                Debug.LogError($"리플레이 저장 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 지정된 경로에서 리플레이 데이터를 불러옵니다.
        /// </summary>
        /// <param name="filePath">리플레이 JSON 파일의 전체 경로</param>
        /// <returns>로드된 리플레이 데이터. 실패 시 null</returns>
        public static ReplayData Load(string filePath)
        {
            try
            {
                if (!File.Exists(filePath))
                {
                    Debug.LogError($"리플레이 파일을 찾을 수 없습니다: {filePath}");
                    return null;
                }

                string json = File.ReadAllText(filePath);
                ReplayData replayData = JsonUtility.FromJson<ReplayData>(json);

                if (replayData == null)
                {
                    Debug.LogError($"[ReplayStorage] 리플레이 JSON 파싱 결과가 null입니다: {filePath}");
                    return null;
                }

                if (replayData.inputs == null || replayData.inputEnds == null)
                {
                    Debug.LogError($"[ReplayStorage] 리플레이 데이터에 입력 기록이 누락되었습니다: {filePath}");
                    return null;
                }

                if (string.IsNullOrEmpty(replayData.ruleAddress) || string.IsNullOrEmpty(replayData.stageAddress))
                {
                    Debug.LogError($"[ReplayStorage] 리플레이 데이터에 규칙/스테이지 주소가 누락되었습니다: {filePath}");
                    return null;
                }

                if (replayData.formatVersion < 0 || replayData.formatVersion > ReplayData.CurrentFormatVersion)
                {
                    Debug.LogError($"[ReplayStorage] 지원하지 않는 리플레이 형식입니다: {replayData.formatVersion}");
                    return null;
                }
                if (replayData.formatVersion == ReplayData.CurrentFormatVersion &&
                    (replayData.frameCount == 0 || replayData.finalScore < 0 ||
                     float.IsNaN(replayData.timeLimit) || float.IsInfinity(replayData.timeLimit) || replayData.timeLimit < 0 ||
                     (replayData.mode != GameMode.Stage && replayData.mode != GameMode.Battle) ||
                     (replayData.mode == GameMode.Battle && !replayData.IsCompleteBattleRecord())))
                {
                    Debug.LogError($"[ReplayStorage] 종료 규칙 또는 완료 기록이 올바르지 않습니다: {filePath}");
                    return null;
                }

                Debug.Log($"리플레이 로드 완료: {filePath}");
                return replayData;
            }
            catch (Exception e)
            {
                Debug.LogError($"리플레이 로드 실패: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// 리플레이 저장 디렉터리의 전체 경로를 반환합니다.
        /// 에디터에서는 프로젝트 내부(Assets/05_Table/Replay), 빌드에서는 persistentDataPath를 사용합니다.
        /// </summary>
        /// <returns>리플레이 디렉터리 경로</returns>
        public static string GetReplayDirectoryPath()
        {
#if UNITY_EDITOR
            return Path.Combine(Application.dataPath, "05_Table", ReplayDirectory);
#else
            return Path.Combine(Application.persistentDataPath, ReplayDirectory);
#endif
        }
    }
}
