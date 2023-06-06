using System;
using System.IO;
using System.Linq;
using Anatawa12.SimpleJson;
using UnityEditor;

namespace Anatawa12.VrcGetResolver
{
    [InitializeOnLoad]
    internal static class Resolver
    {
        static Resolver()
        {
            if (ResolveNeeded())
            {
                AskAndResolve();
            }
        }

        private static bool ResolveNeeded()
        {
            if (SessionState.GetBool("com.anatawa12.vrc-get-resolver.resolved", false))
                return false;
            SessionState.SetBool("com.anatawa12.vrc-get-resolver.resolved", true);
            string vpmManifestJson;
            try
            {
                vpmManifestJson = File.ReadAllText(VpmManifestPath);
            }
            catch (IOException e) when (e is FileNotFoundException || e is DirectoryNotFoundException)
            {
                return false;
            }

            string[] packages;
            try
            {
                var vpmManifest = new JsonParser(vpmManifestJson).Parse(JsonType.Obj);
                var locked = vpmManifest.Get("locked", JsonType.Obj, true);
                packages = locked.Keys.ToArray();
            }
            catch (SystemException e) when (e is InvalidOperationException || e is NullReferenceException)
            {
                // invalid vpm manifest
                return false;
            }

            // there are some path traversal dangerous path
            if (packages.Any(x => x.Contains('/') || x.Contains('\\') || x == ".."))
                return false;

            return packages.Any(package => !File.Exists($"Packages/{package}/package.json"));
        }

        private static void AskAndResolve()
        {
            if (!VrcGet.IsSupported)
            {
                EditorUtility.DisplayDialog("vrc-get resolver",
                    "vrc-get resolver found some missing packages need to be installed " +
                    "but vrc-get is not available for your platform.", "OK");
                return;
            }

            var message = "We found some missing vpm packages need to be installed!\n";

            if (!VrcGet.IsInstalled())
                message += "To install packages, vrc-get resolver will installs vrc-get\n";

            message += "\nDo you want to install packages?";
            
            var result = EditorUtility.DisplayDialogComplex("vrc-get resolver", message,
                "Yes", "Dismiss", "Show missing packages");
            switch (result)
            {
                case 0:
                    InstallAndResolve();
                    break;
                case 1:
                    break;
                case 2:
                    throw new NotImplementedException();
            }
        }

        private static async void InstallAndResolve()
        {
            try
            {
                EditorUtility.DisplayProgressBar("Resolve Project", "Installing vrc-get", 0.0f);
                await VrcGet.InstallIfNeeded();
                EditorUtility.DisplayProgressBar("Resolve Project", "Resolving package", 0.5f);
                await VrcGet.Resolve();
                EditorUtility.ClearProgressBar();
            }
            catch
            {
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("vrc-get resolver",
                    "Exception resolving package! See Console for more details!",
                    "OK");
            }
        }

        private const string VpmManifestPath = "Packages/vpm-manifest.json";
    }
}
