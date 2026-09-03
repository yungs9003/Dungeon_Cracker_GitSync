using System.IO;
using Dungeon.Core;
using UnityEditor;
using UnityEngine;

namespace Dungeon.EditorTools
{
    public static class StatusSaveEditorMenu
    {
        private const string ResetMenuPath = "Tools/Dungeon/Save/Reset Test Save From Default";

        [MenuItem(ResetMenuPath)]
        private static void ResetTestSaveFromDefault()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorUtility.DisplayDialog(
                    "테스트 세이브 초기화 불가",
                    "Play Mode를 완전히 종료한 뒤 다시 실행하세요.",
                    "확인");
                return;
            }

            IStatusSaveStorage storage = StatusSaveStorageFactory.Create();

            if (!File.Exists(storage.DefaultPath))
            {
                Debug.LogError($"[StatusSaveEditorMenu] 기본 세이브가 없습니다: {storage.DefaultPath}");
                return;
            }

            string defaultJson = File.ReadAllText(storage.DefaultPath);
            if (!StatusSaveDefaults.TryDeserialize(defaultJson, out StatusSaveData defaultData, out string error))
            {
                Debug.LogError($"[StatusSaveEditorMenu] 기본 세이브가 유효하지 않습니다: {error}");
                return;
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "테스트 세이브 초기화",
                "현재 savefile.test.json 진행 데이터를 기본 savefile.json 값으로 교체합니다.\n" +
                "교체 전 정상본은 Library/DungeonSaveBackups에 보관됩니다.",
                "초기화",
                "취소");

            if (!confirmed)
                return;

            string normalizedJson = StatusSaveDefaults.Serialize(defaultData);
            if (!storage.TryWrite(normalizedJson, StatusSaveDefaults.IsValidJson))
            {
                Debug.LogError($"[StatusSaveEditorMenu] 테스트 세이브 초기화 실패: {storage.LastError}");
                return;
            }

            AssetDatabase.ImportAsset(
                "Assets/StreamingAssets/savefile.test.json",
                ImportAssetOptions.ForceUpdate);

            Debug.Log($"[StatusSaveEditorMenu] 테스트 세이브 초기화 완료: {storage.ActivePath}");
        }
    }
}
