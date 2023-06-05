using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Runtime.InteropServices;
using Mono.Unix;

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

        public static bool IsInstalled() => LocalVrcGetPath != null && File.Exists(LocalVrcGetPath);

        public static void InstallIfNeeded()
        {
            if (!IsSupported) throw new Exception("VrcGet is not supported for this platform");
            if (IsInstalled()) return;
            Directory.CreateDirectory(VrcGetInstallFolder);
            using (var httpClient = new HttpClient())
            {
                httpClient.DefaultRequestHeaders.Add("User-Agent",
                    "vrc-get-resolver (github.com/anatawa12/vrc-get-resolver)");
                using (var name = httpClient
                           .GetAsync($"https://github.com/anatawa12/vrc-get/releases/latest/download/{ExecutableName}")
                           .Result)
                using (var file = File.OpenWrite(LocalVrcGetPath))
                {
                    name.Content.CopyToAsync(file).Wait();
                }
            }

            if (!RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                // unix system. set executable
                new UnixFileInfo(LocalVrcGetPath).FileAccessPermissions |= Executable;
            }
        }

        private const FileAccessPermissions Executable =
            FileAccessPermissions.OtherExecute | FileAccessPermissions.GroupExecute | FileAccessPermissions.UserExecute;

        public static void Resolve()
        {
            var process = Process.Start($"{LocalVrcGetPath} resolve --project .");
            if (process == null) throw new Exception("cannot start vrc-get");
            process.WaitForExit();
        }
    }
}
