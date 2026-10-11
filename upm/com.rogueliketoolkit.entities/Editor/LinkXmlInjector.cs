using System.IO;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.UnityLinker;
using PackageInfo = UnityEditor.PackageManager.PackageInfo;

namespace RoguelikeToolkit.Entities.Editor
{
    /// <summary>
    /// UnityLinker collects link.xml files from Assets/ only, so a package's own link.xml is ignored unless a
    /// linker processor returns its path. This one returns Runtime/link.xml of this package.
    /// </summary>
    internal sealed class LinkXmlInjector : IUnityLinkerProcessor
    {
        private const string PackageName = "com.rogueliketoolkit.entities";

        public int callbackOrder => 0;

        public string GenerateAdditionalLinkXmlFile(BuildReport report, UnityLinkerBuildPipelineData data)
        {
            var package = PackageInfo.FindForAssetPath("Packages/" + PackageName);
            if (package == null)
            {
                throw new BuildFailedException(PackageName + ": package not found, cannot hand its link.xml to UnityLinker.");
            }

            return Path.Combine(package.resolvedPath, "Runtime", "link.xml");
        }
    }
}
