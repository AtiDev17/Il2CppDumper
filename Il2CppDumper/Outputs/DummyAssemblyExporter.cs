using dnlib.DotNet;
using dnlib.DotNet.Writer;
using System.IO;

namespace Il2CppDumper
{
    internal static class DummyAssemblyExporter
    {
        public static void Export(Il2CppExecutor il2CppExecutor, string outputDir, Config config)
        {
            var dummyDir = Path.Combine(outputDir, "DummyDll");
            if (Directory.Exists(dummyDir))
            {
                Directory.Delete(dummyDir, true);
            }
            _ = Directory.CreateDirectory(dummyDir);
            var dummy = new DummyAssemblyGenerator(il2CppExecutor, config);
            foreach (var assembly in dummy.Assemblies)
            {
                var module = assembly.ManifestModule;
                using var stream = new MemoryStream();
                var opts = new ModuleWriterOptions(module) { Logger = DummyLogger.NoThrowInstance };
                module.Write(stream, opts);
                File.WriteAllBytes(Path.Combine(dummyDir, module.Name), stream.ToArray());
            }
        }
    }
}
