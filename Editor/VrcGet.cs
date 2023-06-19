using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using JetBrains.Annotations;
using Mono.Unix;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Anatawa12.VrcGetResolver
{
    internal static class VrcGet
    {
        private static readonly string ExecutableName = GetVrcGetExecutableName();

        private static readonly string LocalVrcGetPath =
            ExecutableName == null ? null : VrcGetInstallFolder + "/" + ExecutableName;
        private const string VrcGetInstallFolder = "Library/com.anatawa12.vrc-get-resolver";

        public static bool IsSupported => ExecutableName != null;

        private static string GetVrcGetExecutableName()
        {
            bool isArm;
            switch (RuntimeInformation.OSArchitecture)
            {
                case Architecture.Arm:
                case Architecture.X86:
                    return null; // 32bit not supported
                case Architecture.Arm64:
                    isArm = true;
                    break;
                case Architecture.X64:
                    isArm = false;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                return "x86_64-pc-windows-msvc-vrc-get.exe";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                return isArm ? "aarch64-apple-darwin-vrc-get" : "x86_64-apple-darwin-vrc-get";
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                return isArm ? "aarch64-unknown-linux-musl-vrc-get " : "x86_64-unknown-linux-musl-vrc-get ";
            return null;
        }

        public static bool IsInstalled() => 
            LocalVrcGetPath != null && File.Exists(LocalVrcGetPath) &&
            GetVersion() != null && new Version(1, 1, 0) <= new Version(GetVersion());

        private static readonly SemaphoreSlim InstallSemaphore = new SemaphoreSlim(1);

        public static async Task InstallIfNeeded()
        {
            await InstallSemaphore.WaitAsync();
            try
            {
                await InstallIfNeededImpl();
            }
            finally
            {
                InstallSemaphore.Release();
            }
        }

        private static async Task InstallIfNeededImpl()
        {
            if (!IsSupported) throw new Exception("VrcGet is not supported for this platform");
            if (IsInstalled()) return;
            Directory.CreateDirectory(VrcGetInstallFolder);
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent",
                    "vrc-get-resolver (github.com/anatawa12/vrc-get-resolver)");
                using (var name = await httpClient
                           .GetAsync($"https://github.com/anatawa12/vrc-get/releases/latest/download/{ExecutableName}"))
                using (var file = File.OpenWrite(LocalVrcGetPath))
                {
                    await name.Content.CopyToAsync(file);
                }
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // unix system. set executable
                new UnixFileInfo(LocalVrcGetPath).FileAccessPermissions |= Executable;
            }

            // now executable so reset
            _versionCache = null; // reset
        }

        private const FileAccessPermissions Executable =
            FileAccessPermissions.OtherExecute | FileAccessPermissions.GroupExecute | FileAccessPermissions.UserExecute;

        public static async Task CallCommand(string arguments)
        {
            var process = Process.Start(LocalVrcGetPath, arguments);
            if (process == null) throw new Exception("cannot start vrc-get");
            await Task.Run(() => process.WaitForExit());
        }

        private static async Task<T> CallJsonCommand<T>(string arguments)
        {
            var startInfo = new ProcessStartInfo(LocalVrcGetPath, arguments);
            startInfo.RedirectStandardOutput = true;
            startInfo.UseShellExecute = false;
            var process = Process.Start(startInfo);
            if (process == null) throw new Exception("cannot start vrc-get");
            await Task.Run(() => process.WaitForExit());
            var json = await process.StandardOutput.ReadToEndAsync();
            return JsonUtility.FromJson<T>(json);
        }

        private static volatile string _versionCache;

        [CanBeNull]
        public static string GetVersion()
        {
            if (_versionCache == "ERROR") return null;
            if (_versionCache != null) return _versionCache;
            try
            {
                var startInfo = new ProcessStartInfo(LocalVrcGetPath, "--version");
                startInfo.RedirectStandardOutput = true;
                startInfo.UseShellExecute = false;
                var process = Process.Start(startInfo);
                if (process == null) throw new Exception("cannot start vrc-get");
                process.WaitForExit();
                var result = process.StandardOutput.ReadToEnd().Trim();
                if (!result.StartsWith("vrc-get ", StringComparison.Ordinal)) return null;
                return _versionCache = result.Split(new[] { ' ' }, 3)[1];
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                _versionCache = "ERROR";
                return null;
            }
        }

        public static async Task Resolve() => await CallCommand("resolve --project .");
        public static async Task Update() => await CallCommand("update");

        public static async Task<InfoProject> GetProjectInfo() => 
            await CallJsonCommand<InfoProject>("info project --json-format 1 --project .");

        public static async Task<InfoPackage> GetPackageInfo(string package) => 
            await CallJsonCommand<InfoPackage>($"info package \"{package}\" --json-format 1 --offline");

        [Serializable]
        public sealed class InfoProject
        {
            public List<PackageInfo> packages;

            [Serializable]
            public sealed class PackageInfo
            {
                [NotNull] public string name;
                [CanBeNull] public string installed;
                [CanBeNull] public string locked;
                [CanBeNull] public string[] requested;
            }
        }
        
        [Serializable]
        public sealed class InfoPackage
        {
            public List<VersionInfo> versions;

            [Serializable]
            public sealed class VersionInfo
            {
                [NotNull] public string version;
            }
        }
    }
}
