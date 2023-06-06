using System;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace Anatawa12.VrcGetResolver
{
    internal class ResolverWindow : EditorWindow
    {
        public ResolverWindow()
        {
            minSize = new Vector2(300, minSize.y);
        }

        [MenuItem("Tools/vrc-get resolver")]
        private static void Open() => GetWindow<ResolverWindow>("vrc-get resolver window");

        private static (string, string)[] _values = new[]
        {
            ("com.anatawa12.custom-localization-for-editor-extension", "1.0.0"),
            ("com.anatawa12.avatar-optimizer", "1.0.0"),
        };
        
        private static class Styles
        {
            internal static readonly GUIStyle WordWrapLabel = new GUIStyle(EditorStyles.label)
            {
                wordWrap = true,
            };
            internal static readonly GUIStyle RedLabelLabel = new GUIStyle(EditorStyles.label)
            {
                normal = { textColor = Color.red },
                hover = { textColor = Color.red },
            };

            internal static readonly GUIStyle UpdateButton = new GUIStyle(GUI.skin.button)
            {
                normal = { textColor = Color.green },
                hover = { textColor = Color.green },
            };
        }

        private Task<VrcGet.InfoProject> _projectTask;

        private void OnGUI()
        {
            if (GUILayout.Button("Refresh"))
                Refresh();

            if (_projectTask == null)
            {
                Refresh();
            }
            Debug.Assert(_projectTask != null, nameof(_projectTask) + " != null");

            if (_projectTask.IsFaulted)
            {
                GUILayout.Label("Error getting Information", Styles.RedLabelLabel);
            }
            else if (_projectTask.IsCompleted)
            {
                foreach (var package in _projectTask.Result.packages)
                {
                    if (package.locked == null) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{package.name} {package.locked}", Styles.WordWrapLabel);
                    if (string.IsNullOrEmpty(package.installed))
                        GUILayout.Label("MISSING!", Styles.RedLabelLabel, GUILayout.Width(50));
                    else
                        GUILayout.Label(package.installed, EditorStyles.label, GUILayout.Width(50));

                    EditorGUILayout.Popup(0, new[] { "<TODO>" }, GUILayout.Width(50));
                    GUILayout.Button("Button", Styles.UpdateButton, GUILayout.Width(50));
                    GUILayout.EndHorizontal();
                }
            }
            else
            {
                GUILayout.Label("Fetching Package information...");
            }
        }

        private void Refresh()
        {
            _projectTask = Task.Run(VrcGet.GetProjectInfo);
            _projectTask.ContinueWith(x =>
            {
                if (x.Exception is AggregateException e)
                {
                    UnityEngine.Debug.LogException(e.InnerException);
                    return;
                }
                else
                {
                    // TODO download package versions
                }
            });
        }
    }
}
