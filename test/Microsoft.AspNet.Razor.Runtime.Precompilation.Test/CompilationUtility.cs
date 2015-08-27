// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.Dnx.Compilation;
using Microsoft.Dnx.Compilation.CSharp;
using Microsoft.Dnx.Runtime.Infrastructure;

namespace Microsoft.AspNet.Razor.Runtime.Precompilation
{
    public static class CompilationUtility
    {
        private static readonly Assembly ExecutingAssembly = typeof(CompilationUtility).GetTypeInfo().Assembly;
        public static readonly string GeneratedAssemblyName = Path.GetRandomFileName() + "." + Path.GetRandomFileName();

        public static Compilation GetCompilation(params string[] resourceFiles)
        {
            var assemblyVersion = ExecutingAssembly.GetName().Version;

            var syntaxTrees = new List<SyntaxTree>
            {
                CSharpSyntaxTree.ParseText(
                    $"[assembly: {typeof(AssemblyVersionAttribute).FullName}(\"{assemblyVersion}\")]")
            };

            foreach (var resourceFile in resourceFiles)
            {
                var resourceContent = ReadManifestResource(resourceFile);
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(resourceContent));
            }

            var libraryExporter = (ILibraryExporter)CallContextServiceLocator
                .Locator
                .ServiceProvider
                .GetService(typeof(ILibraryExporter));
            var applicationName = ExecutingAssembly.GetName().Name;
            var libraryExport = libraryExporter.GetExport(applicationName);

            var references = new List<MetadataReference>();
            var roslynReference = libraryExport.MetadataReferences[0] as IRoslynMetadataReference;
            var compilationReference = roslynReference?.MetadataReference as CompilationReference;
            if (compilationReference != null)
            {
                references.AddRange(compilationReference.Compilation.References);
                references.Add(roslynReference.MetadataReference);
            }

            var export = libraryExporter.GetAllExports(applicationName);
            foreach (var metadataReference in export.MetadataReferences)
            {
                // Taken from https://github.com/aspnet/KRuntime/blob/757ba9bfdf80bd6277e715d6375969a7f44370ee/src/...
                // Microsoft.Framework.Runtime.Roslyn/RoslynCompiler.cs#L164
                // We don't want to take a dependency on the Roslyn bit directly since it pulls in more dependencies
                // than the view engine needs (Microsoft.Framework.Runtime) for example
                references.Add(ConvertMetadataReference(metadataReference));
            }

            return CSharpCompilation.Create(
                GeneratedAssemblyName,
                syntaxTrees,
                references);
        }

        private static string ReadManifestResource(string path)
        {
            path = $"{ExecutingAssembly.GetName().Name}.{path}.cs";
            using (var contentStream = ExecutingAssembly.GetManifestResourceStream(path))
            {
                using (var reader = new StreamReader(contentStream))
                {
                    return reader.ReadToEnd();
                }
            }
        }

        private static MetadataReference ConvertMetadataReference(IMetadataReference metadataReference)
        {
            var roslynReference = metadataReference as IRoslynMetadataReference;

            if (roslynReference != null)
            {
                return roslynReference.MetadataReference;
            }

            var embeddedReference = metadataReference as IMetadataEmbeddedReference;

            if (embeddedReference != null)
            {
                return MetadataReference.CreateFromImage(embeddedReference.Contents);
            }

            var fileMetadataReference = metadataReference as IMetadataFileReference;

            if (fileMetadataReference != null)
            {
                return MetadataReference.CreateFromFile(fileMetadataReference.Path);
            }

            var projectReference = metadataReference as IMetadataProjectReference;
            if (projectReference != null)
            {
                using (var ms = new MemoryStream())
                {
                    projectReference.EmitReferenceAssembly(ms);

                    return MetadataReference.CreateFromImage(ms.ToArray());
                }
            }

            throw new NotSupportedException();
        }
    }
}
