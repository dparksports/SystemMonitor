using System;
using AutoCommand.Agent.Core;
using AutoCommand.Contain.Core;
using AutoCommand.Forensics.Core;
using AutoCommand.Intercept.Core;
using AutoCommand.NetWatch.Core;
using AutoCommand.Sitedoctor.Core;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using AutoCommand.Helpers;
using AutoCommand.Sdk;

namespace AutoCommand.Services
{
    public class PluginLoaderService
    {
        private readonly List<IPlugin> _loadedPlugins = new List<IPlugin>();
        private readonly string _pluginsPath;

        public PluginLoaderService()
        {
            _pluginsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");
            if (!Directory.Exists(_pluginsPath))
            {
                Directory.CreateDirectory(_pluginsPath);
            }
        }

        public List<IPlugin> LoadedPlugins => _loadedPlugins;
        public List<string> CompileErrors { get; } = new List<string>();

        public async Task LoadPluginsAsync()
        {
            _loadedPlugins.Clear();
            CompileErrors.Clear();

            if (!Directory.Exists(_pluginsPath)) return;

            var context = new PluginLoadContext(_pluginsPath);

            // 1. Load Pre-compiled DLLs
            var pluginFiles = Directory.GetFiles(_pluginsPath, "*.dll");
            foreach (var file in pluginFiles)
            {
                try
                {
                    var assembly = context.LoadFromAssemblyPath(file);
                    await ExtractAndInitializePlugins(assembly);
                }
                catch (Exception ex)
                {
                    CompileErrors.Add($"Failed to load DLL {Path.GetFileName(file)}: {ex.Message}");
                }
            }

            // 2. Compile and Load Source Files (.cs)
            var sourceFiles = Directory.GetFiles(_pluginsPath, "*.cs");
            foreach (var file in sourceFiles)
            {
                try
                {
                    await CompileAndLoadSourceFileAsync(file, context);
                }
                catch (Exception ex)
                {
                    CompileErrors.Add($"Failed to process source file {Path.GetFileName(file)}: {ex.Message}");
                }
            }
        }

        private async Task CompileAndLoadSourceFileAsync(string filePath, PluginLoadContext context)
        {
            string sourceCode = await File.ReadAllTextAsync(filePath);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode);

            // Provide rich context by supplying all loaded assemblies to the compiler
            var references = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => !a.IsDynamic && !string.IsNullOrWhiteSpace(a.Location))
                .Select(a => MetadataReference.CreateFromFile(a.Location))
                .Cast<MetadataReference>()
                .ToList();

            string assemblyName = Path.GetFileNameWithoutExtension(filePath) + "_" + Guid.NewGuid().ToString("N");

            var compilation = CSharpCompilation.Create(
                assemblyName,
                new[] { syntaxTree },
                references,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
            );

            using var ms = new MemoryStream();
            EmitResult result = compilation.Emit(ms);

            if (!result.Success)
            {
                var failures = result.Diagnostics.Where(diagnostic => 
                    diagnostic.IsWarningAsError || 
                    diagnostic.Severity == DiagnosticSeverity.Error);

                foreach (var diagnostic in failures)
                {
                    CompileErrors.Add($"{Path.GetFileName(filePath)} ({diagnostic.Location.GetLineSpan().StartLinePosition.Line + 1}): {diagnostic.GetMessage()}");
                }
                return;
            }

            ms.Seek(0, SeekOrigin.Begin);
            var compiledAssembly = context.LoadFromStream(ms);
            await ExtractAndInitializePlugins(compiledAssembly);
        }

        private async Task ExtractAndInitializePlugins(Assembly assembly)
        {
            var pluginTypes = assembly.GetTypes()
                .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

            foreach (var type in pluginTypes)
            {
                var plugin = (IPlugin)Activator.CreateInstance(type);
                await plugin.InitializeAsync(new PluginContext(plugin.Name));
                _loadedPlugins.Add(plugin);
            }
        }

        private class PluginContext : IPluginContext
        {
            private readonly string _pluginName;

            public PluginContext(string pluginName)
            {
                _pluginName = pluginName;
            }

            public void Log(string message)
            {
                System.Diagnostics.Debug.WriteLine($"[{_pluginName}] {message}");
            }

            public void LogError(string message, Exception ex = null)
            {
                System.Diagnostics.Debug.WriteLine($"[{_pluginName}] ERROR: {message} {ex?.Message}");
            }

            public Task<string> RunCommandAsync(string fileName, string arguments)
            {
                return ProcessRunner.RunAsync(fileName, arguments);
            }
        }
    }

    public class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            string assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }
            return null;
        }

        protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
        {
            string libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
            if (libraryPath != null)
            {
                return LoadUnmanagedDllFromPath(libraryPath);
            }
            return IntPtr.Zero;
        }
    }
}
