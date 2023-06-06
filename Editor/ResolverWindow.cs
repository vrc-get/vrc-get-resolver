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
            internal static readonly GUIStyle DowngradeButton = new GUIStyle(GUI.skin.button)
            {
                normal = { textColor = Color.red },
                hover = { textColor = Color.red },
            };
        }

        private Vector2 _scroll;
        private Task<VrcGet.InfoProject> _projectTask;
        private Dictionary<string, PackageInfo> _packages;

        private void OnGUI()
        {
            if (GUILayout.Button("Refresh"))
                Refresh();

            if (_projectTask == null)
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
                const float versionWidth = 60;
                const float buttonWidth = 60;

                _scroll = GUILayout.BeginScrollView(_scroll);

                GUILayout.BeginHorizontal();
                var headerPackageRect = GUILayoutUtility.GetRect(GUIContent.none, Styles.WordWrapLabel);
                var headerInstalledRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label, GUILayout.Width(installedWidth));
                var headerButtonRect = GUILayoutUtility.GetRect(GUIContent.none, Styles.UpdateButton, GUILayout.Width(buttonWidth));
                var headerVersionPopupRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.popup, GUILayout.Width(versionWidth));
                GUILayout.EndHorizontal();

                headerPackageRect.y += _scroll.y;
                headerInstalledRect.y += _scroll.y; 
                headerVersionPopupRect.y += _scroll.y;
                headerButtonRect.y += _scroll.y;

                foreach (var package in _projectTask.Result.packages)
                {
                    if (string.IsNullOrEmpty(package.locked)) continue;
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"{package.name} {package.locked}", Styles.WordWrapLabel);
                    var installedRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.label, GUILayout.Width(installedWidth));
                    var buttonRect = GUILayoutUtility.GetRect(GUIContent.none, Styles.UpdateButton, GUILayout.Width(buttonWidth));
                    var versionPopupRect = GUILayoutUtility.GetRect(GUIContent.none, EditorStyles.popup, GUILayout.Width(versionWidth));
                    GUILayout.EndHorizontal();

                    if (string.IsNullOrEmpty(package.installed))
                        GUI.Label(installedRect, "MISSING!", Styles.RedLabelLabel);
                    else
                        GUI.Label(installedRect, package.installed, EditorStyles.label);


                    var info = _packages[package.name];
                    switch (info.Status)
                    {
                        case PackageInfo.InfoStatus.Pending:
                            GUI.Label(versionPopupRect, "Loading...");
                            break;
                        case PackageInfo.InfoStatus.Error:
                            GUI.Label(versionPopupRect, "Error", Styles.RedLabelLabel);
                            break;
                        case PackageInfo.InfoStatus.NotFound:
                            GUI.Label(versionPopupRect, "Missing", Styles.RedLabelLabel);
                            break;
                        case PackageInfo.InfoStatus.SelectingSame:
                        case PackageInfo.InfoStatus.SelectingUpgrade:
                        case PackageInfo.InfoStatus.SelectingDowngrade:
                            info.Init(false);
                            var index = info.Index == -1 ? 0 : info.Index;

                            EditorGUI.BeginChangeCheck();
                            index = EditorGUI.Popup(versionPopupRect, index, info.VersionsLabels);
                            if (EditorGUI.EndChangeCheck())
                            {
                                info.Index = index;
                            }
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }

                    EditorGUI.BeginDisabledGroup(true);
                    switch (info.Status)
                    {
                        case PackageInfo.InfoStatus.Pending:
                        case PackageInfo.InfoStatus.Error:
                        case PackageInfo.InfoStatus.NotFound:
                        case PackageInfo.InfoStatus.SelectingSame:
                            break;
                        case PackageInfo.InfoStatus.SelectingUpgrade:
                            GUI.Button(buttonRect, "Upgrade", Styles.UpdateButton);
                            break;
                        case PackageInfo.InfoStatus.SelectingDowngrade:
                            GUI.Button(buttonRect, "Downgrade", Styles.DowngradeButton);
                            break;
                        default:
                            throw new ArgumentOutOfRangeException();
                    }
                    EditorGUI.EndDisabledGroup();
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
                ExtendRect(ref headerRect, headerVersionPopupRect);
                ExtendRect(ref headerRect, headerButtonRect);
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
            private bool? _includePrereleaseOld;
            public GUIContent[] VersionsLabels;
            private int _index = -1;
            private readonly string _lockedVersion;
            public InfoStatus Status;

            public int Index
            {
                get => _index;
                set
                {
                    if (_index != value)
                    {
                        _index = value;
                        ComputeState();
                    }
                }
            }

            public PackageInfo([NotNull] Task<VrcGet.InfoPackage> task, string lockedVersion, Action repaint)
            {
                _task = task ?? throw new ArgumentNullException(nameof(task));
                _lockedVersion = lockedVersion;
                Status = InfoStatus.Pending;

                task.ContinueWith(x =>
                {
                    if (x.Exception is AggregateException ex)
                    {
                        UnityEngine.Debug.LogException(ex.InnerException);
                        Status = InfoStatus.Error;
                    }
                    else
                    {
                        if (x.Result == null || x.Result.versions.Count == 0)
                        {
                            Status = InfoStatus.NotFound;
                        }
                        else
                        {
                            Init(false);
                            EditorApplication.delayCall += repaint.Invoke;
                        }
                    }
                });
            }

            public void Init(bool includePrerelease)
            {
                if (_includePrereleaseOld == includePrerelease) return;
                _includePrereleaseOld = includePrerelease;
                var versions = _task.Result.versions.Select(x => x.version);
                if (!includePrerelease) versions = versions.Where(x => !x.Contains("-"));
                var array = versions.ToArray();
                Array.Sort(array, (a, b) => SortVersion(b, a));
                VersionsLabels = array.Select(x => new GUIContent(x)).ToArray();
                if (Index == -1)
                    Index = Array.IndexOf(array, _lockedVersion);
                ComputeState();
            }

            private void ComputeState()
            {
                if (Index == -1)
                {
                    Status = InfoStatus.SelectingSame;
                }
                else
                {
                    var version = VersionsLabels[Index].text;

                    var cmp = SortVersion(version, _lockedVersion);
                    if (cmp == 0)
                        Status = InfoStatus.SelectingSame;
                    else if (cmp < 0)
                        Status = InfoStatus.SelectingDowngrade;
                    else
                        Status = InfoStatus.SelectingUpgrade;
                }
            }

            public enum InfoStatus
            {
                Pending,
                Error,
                NotFound,
                SelectingSame,
                SelectingUpgrade,
                SelectingDowngrade,
            }

            private static int SortVersion(string a, string b)
            {
                (int maj, int min, int pat, string pre) ParseVersion(string v)
                {
                    var hyphen = v.Split(new[] {'-'}, 2);
                    var prerelease = v.Length == 1 ? null : v.Split(new[] {'+'}, 2)[0];
                    var version = hyphen[0].Split('.');
                    return (int.Parse(version[0]), int.Parse(version[1]), int.Parse(version[2]), prerelease);
                }
                int comp;
                var aParsed = ParseVersion(a);
                var bParsed = ParseVersion(b);

                comp = aParsed.maj.CompareTo(bParsed.maj);
                if (comp != 0) return comp;
                comp = aParsed.min.CompareTo(bParsed.min);
                if (comp != 0) return comp;
                comp = aParsed.pat.CompareTo(bParsed.pat);
                if (comp != 0) return comp;
                comp = String.Compare(aParsed.pre, bParsed.pre, StringComparison.Ordinal);
                if (comp != 0) return comp;
                return 0;
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
                    _packages = new Dictionary<string, PackageInfo>();
                    foreach (var package in x.Result.packages.Where(package => package.locked != null))
                    {
                        var task = Task.Run(() => VrcGet.GetPackageInfo(package.name));
                        _packages.Add(package.name, new PackageInfo(task, package.locked, Repaint));
                    }
                    EditorApplication.delayCall += Repaint;
                }
            });
        }
    }
}
