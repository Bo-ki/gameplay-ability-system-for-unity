using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace GAS.Runtime.Tests.Event
{
    public sealed class RuntimeBoundaryContractTests
    {
        private static readonly string[] EntityHelperAllowedPaths =
        {
            "Assets/GAS/Runtime/General/Helper/EntityHelper.cs",
            "Assets/GAS/Runtime/AbilitySystem/AbilitySystemFacade.cs",
            "Assets/GAS/Runtime/AbilitySystem/AbilitySystemBinding.cs",
            "Assets/GAS/Runtime/Ability/TargetCatcher/",
            "Assets/GAS/Runtime/Cue/",
            "Assets/GAS/Runtime/System/Cue/",
            "Assets/GAS/Runtime/System/SAscDestroyFinalize.cs",
        };

        private static readonly string[] UnityEngineAllowedPaths =
        {
            "Assets/GAS/General/Editor/",
            "Assets/GAS/General/GASResourceLoader.cs",
            "Assets/GAS/General/GASTimer.cs",
            "Assets/GAS/General/Util/",
            "Assets/GAS/Runtime/General/GASManager.cs",
            "Assets/GAS/Runtime/General/Helper/EntityHelper.cs",
            "Assets/GAS/Runtime/General/Helper/CueHelper.cs",
            "Assets/GAS/Runtime/General/Helper/TagHelper.cs",
            "Assets/GAS/Runtime/General/XParam/",
            "Assets/GAS/Runtime/AbilitySystem/",
            "Assets/GAS/Runtime/Ability/TargetCatcher/",
            "Assets/GAS/Runtime/Cue/",
            "Assets/GAS/Runtime/System/Cue/",
            "Assets/GAS/Runtime/System/SAscDestroyFinalize.cs",
            "Assets/GAS/Runtime/System/SystemGroup/GASGroups.cs",
        };

        private static readonly Regex UnityEngineReferencePattern = new Regex(
            @"using\s+UnityEngine\s*;|UnityEngine\.|\bGameObject\b|\bMonoBehaviour\b|\bTransform\b|\bPhysics\.|\bObject\.Instantiate\b|\bObject\.Destroy\b|\bResources\.|\bDebug\.|\bParticleSystem\b|\bAudioSource\b|\bAnimator\b|\bTooltip\b|\bLayerMask\b|\bVector2\b|\bVector3\b",
            RegexOptions.Compiled);

        [Test]
        public void SimulationRuntimeDoesNotReadEntityHelperPresentationBinding()
        {
            var offenders = FindSourceFiles("Assets/GAS/Runtime")
                .Where(file => File.ReadAllText(file.FullPath).Contains("EntityHelper"))
                .Where(file => !IsAllowed(file.RelativePath, EntityHelperAllowedPaths))
                .Select(file => file.RelativePath)
                .ToArray();

            Assert.That(
                offenders,
                Is.Empty,
                "EntityHelper is presentation binding only. Disallowed references:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void SimulationRuntimeDoesNotIntroduceUnityEnginePresentationDependencies()
        {
            var offenders = FindSourceFiles("Assets/GAS/Runtime", "Assets/GAS/General")
                .Where(file => UnityEngineReferencePattern.IsMatch(File.ReadAllText(file.FullPath)))
                .Where(file => !IsAllowed(file.RelativePath, UnityEngineAllowedPaths))
                .Select(file => file.RelativePath)
                .ToArray();

            Assert.That(
                offenders,
                Is.Empty,
                "UnityEngine dependencies must stay in presentation, authoring, resource, debug, or bootstrap boundaries. Disallowed references:\n"
                + string.Join("\n", offenders));
        }

        private static IEnumerable<SourceFile> FindSourceFiles(params string[] relativeRoots)
        {
            var projectRoot = FindProjectRoot();
            foreach (var relativeRoot in relativeRoots)
            {
                var fullRoot = Path.Combine(projectRoot, relativeRoot.Replace('/', Path.DirectorySeparatorChar));
                if (!Directory.Exists(fullRoot))
                    continue;

                foreach (var file in Directory.GetFiles(fullRoot, "*.cs", SearchOption.AllDirectories))
                    yield return new SourceFile(file, ToRelativePath(projectRoot, file));
            }
        }

        private static string FindProjectRoot()
        {
            var current = new DirectoryInfo(Environment.CurrentDirectory);
            while (current != null)
            {
                var runtimePath = Path.Combine(current.FullName, "Assets", "GAS", "Runtime");
                if (Directory.Exists(runtimePath))
                    return current.FullName;

                current = current.Parent;
            }

            Assert.Fail("Could not locate Unity project root from " + Environment.CurrentDirectory);
            return string.Empty;
        }

        private static string ToRelativePath(string root, string fullPath)
        {
            var relative = fullPath.Substring(root.Length)
                .TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return relative.Replace(Path.DirectorySeparatorChar, '/');
        }

        private static bool IsAllowed(string relativePath, IReadOnlyList<string> allowedPaths)
        {
            for (var i = 0; i < allowedPaths.Count; i++)
            {
                var allowedPath = allowedPaths[i];
                if (allowedPath.EndsWith("/", StringComparison.Ordinal))
                {
                    if (relativePath.StartsWith(allowedPath, StringComparison.Ordinal))
                        return true;
                }
                else if (relativePath == allowedPath)
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct SourceFile
        {
            public SourceFile(string fullPath, string relativePath)
            {
                FullPath = fullPath;
                RelativePath = relativePath;
            }

            public string FullPath { get; }
            public string RelativePath { get; }
        }
    }
}
