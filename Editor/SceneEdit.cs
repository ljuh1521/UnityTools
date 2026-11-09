using System;
using System.IO;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace UnityTools.Editor
{
    /// <summary>
    /// 다른 씬을 잠깐 열어 고치고 닫는다. 지금 열린 씬 배치는 그대로 둔다.
    ///
    /// <c>OpenScene(경로, Single)</c>로 열면 사람이 열어 둔 씬이 닫힌다(2026-11-09 DefenceR). 여기서는 곁에
    /// 열고(Additive), 원래 안 열려 있던 씬만 다시 닫는다. 이미 열려 있던 씬은 그 자리에서 고친다 — 밖에서 파일을
    /// 고치면 유니티가 다시 불러올지 묻는 창을 띄워 사람이 누를 때까지 멈춘다.
    /// </summary>
    public static class SceneEdit
    {
        /// <param name="path"><c>Assets/…/이름.unity</c></param>
        /// <param name="edit">고칠 내용. 던지면 저장하지 않는다.</param>
        /// <param name="save">끝나고 저장할지. 읽기만 할 때는 false.</param>
        public static void Run(string path, Action<Scene> edit, bool save = true)
        {
            Scene scene = SceneManager.GetSceneByPath(path);

            bool listed = scene.IsValid();
            bool loaded = listed && scene.isLoaded;

            // 사람이 고치던 것까지 같이 저장되면 그 사람은 저장한 적 없는 변경이 파일에 들어간다.
            if (save && loaded && scene.isDirty)
                throw new InvalidOperationException(
                    $"{path} 에 저장 안 한 변경이 있습니다. 사람에게 먼저 저장하거나 되돌려 달라고 하세요.");

            if (!loaded) scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Additive);

            try
            {
                edit(scene);

                if (save && !EditorSceneManager.SaveScene(scene))
                    throw new IOException(path + " 을 저장하지 못했습니다.");
            }
            finally
            {
                // 목록에만 있고 내려 둔 씬은 다시 내린다.
                if (!loaded) EditorSceneManager.CloseScene(scene, !listed);
            }
        }
    }
}
