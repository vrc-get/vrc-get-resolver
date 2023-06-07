using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using JetBrains.Annotations;
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
        public static void Open() => GetWindow<ResolverWindow>("vrc-get resolver window");

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
        }

        private Vector2 _scroll;
        private Task<VrcGet.InfoProject> _projectTask;

        private void OnEnable()
        {
            if (_projectTask == null)
                Refresh();
        }

        private void OnGUI()
        {
            if (GUILayout.Button("Refresh"))
                Refresh();

            Debug.Assert(_projectTask != null, nameof(_projectTask) + " != null");

            if (_resolve != null)
            {
                GUILayout.Label("Resolving packages...");
            }
            else if (_projectTask.IsFaulted)
            {
                GUILayout.Label("Error getting Information", Styles.RedLabelLabel);
            }
            else if (_projectTask.IsCompleted)
            {
                const float installedWidth = 60;

                _scroll = GUILayout.BeginScrollView(_scroll);

                GUILayout.BeginHorizontal();
                var headerPackageRect = GUILayoutUtility.GetRect(GUIContent.none, Styles.WordWrapLabel);
                var headerInstalledRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label, GUILayout.Width(installedWidth));
                GUILayout.EndHorizontal();

                headerPackageRect.y += _scroll.y;
                headerInstalledRect.y += _scroll.y;

                foreach (var package in _projectTask.Result.packages)
                {
                    if (string.IsNullOrEmpty(package.locked)) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{package.name} {package.locked}", Styles.WordWrapLabel);
                    var installedRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label, GUILayout.Width(installedWidth));
                    GUILayout.EndHorizontal();

                    if (string.IsNullOrEmpty(package.installed))
                        GUI.Label(installedRect, "MISSING!", Styles.RedLabelLabel);
                    else
                        GUI.Label(installedRect, package.installed, EditorStyles.label);
                }

                void ExtendRect(ref Rect toExtend, Rect extend)
                {
                    toExtend.xMin = Mathf.Min(toExtend.xMin, extend.xMin);
                    toExtend.xMax = Mathf.Max(toExtend.xMax, extend.xMax);
                    toExtend.yMin = Mathf.Min(toExtend.yMin, extend.yMin);
                    toExtend.yMax = Mathf.Max(toExtend.yMax, extend.yMax);
                }

                var headerRect = headerPackageRect;
                ExtendRect(ref headerRect, headerPackageRect);
                ExtendRect(ref headerRect, headerInstalledRect);
                EditorGUI.DrawRect(headerRect, DefaultBackgroundColor);

                GUI.Label(headerPackageRect, "Package", Styles.WordWrapLabel);
                GUI.Label(headerInstalledRect, "Installed");

                GUILayout.EndScrollView();

                if (GUILayout.Button("Resolve ALL"))
                {
                    async Task ResolveAll()
                    {
                        await VrcGet.Resolve();
                        MethodInfo method = typeof(UnityEditor.PackageManager.Client).GetMethod("Resolve",
                            BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                        if (method != null)
                            method.Invoke(null, null);
                        _resolve = null;
                    }
                    _resolve = ResolveAll();
                }
                GUILayout.FlexibleSpace();
            }
            else
            {
                GUILayout.Label("Fetching Package information...");
            }
        }

        private static Color _defaultBackgroundColor;
        private Task _resolve;

        public static Color DefaultBackgroundColor
        {
            get
            {
                if (_defaultBackgroundColor.a == 0)
                {
                    var method = typeof(EditorGUIUtility)
                        .GetMethod("GetDefaultBackgroundColor", BindingFlags.NonPublic | BindingFlags.Static);
                    _defaultBackgroundColor = (Color)method.Invoke(null, null);
                }
                return _defaultBackgroundColor;
            }
        }

        class PackageInfo
        {
            private readonly Task<VrcGet.InfoPackage> _task;

            public PackageInfo([NotNull] Task<VrcGet.InfoPackage> task, Action repaint)
            {
                _task = task ?? throw new ArgumentNullException(nameof(task));

                task.ContinueWith(x =>
                {
                    if (x.Exception is AggregateException ex)
                    {
                        UnityEngine.Debug.LogException(ex.InnerException);
                    }
                    else
                    {
                        if (x.Result == null || x.Result.versions.Count == 0)
                        {
                        }
                        else
                        {
                            EditorApplication.delayCall += repaint.Invoke;
                        }
                    }
                });
            }
        }

        private void Refresh()
        {
            if (!VrcGet.IsSupported)
            {
                EditorUtility.DisplayDialog("vrc-get resolver", "your platform is not supported!", "OK");
                Close();
                return;
            }

            Task installIfNeeded = null;
            if (!VrcGet.IsInstalled())
            {
                if (!EditorUtility.DisplayDialog("vrc-get resolver", 
                        "vrc-get is not yet installed!", 
                        "Install", "Close the vrc-get resolver"))
                {
                    Close();
                    return;
                }

                installIfNeeded = VrcGet.InstallIfNeeded();
            }
            _projectTask = Task.Run(async () =>
            {
                if (installIfNeeded != null) await installIfNeeded;
                await VrcGet.Update();
                return await VrcGet.GetProjectInfo();
            });
            _projectTask.ContinueWith(x =>
            {
                if (x.Exception is AggregateException e)
                {
                    UnityEngine.Debug.LogException(e.InnerException);
                }
                else
                {
                    EditorApplication.delayCall += Repaint;
                }
            });
        }
    }
}
