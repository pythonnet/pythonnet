using System.ComponentModel;
using System.Reflection;
using System.Xml.Linq;
using System;
using System.Linq;
using Mono.Cecil;

namespace RemapDllTarget
{
    class Program
    {
        static void Main(string[] args)
        {
            var filename = args[0];
            Console.WriteLine($"Loading existing DLL {filename}...");
            var def = AssemblyDefinition.ReadAssembly(filename, new ReaderParameters {
                ReadingMode = ReadingMode.Immediate,
                ReadWrite = true,
                InMemory = true
            });

            var mappings = args.Skip(1)
                .Select(x => x.Split('=', 2).ToArray())
                .ToDictionary(x => x[0], x => new ModuleReference(x[1]));

            Console.WriteLine($"Remapping DllImport paths in {filename}...");
            foreach (var kvp in mappings) {
                Console.WriteLine($"{kvp.Key} => {kvp.Value}");
            }

            var module = def.MainModule;
            foreach (var mref in mappings.Values)
                module.ModuleReferences.Add(mref);

            foreach (var type in def.MainModule.Types) {
                foreach (var func in type.Methods) {
                    if (func.HasPInvokeInfo) {
                        var info = func.PInvokeInfo;
                        if (mappings.TryGetValue(info.Module.Name, out ModuleReference newRef)) {
                            Console.WriteLine($"Remapping {info.EntryPoint}: {info.Module} => {newRef}");
                            info.Module = newRef;
                        }

                        func.PInvokeInfo = info;
                    }
                }
            }

            Console.WriteLine($"Writing result");
            def.Write(filename);
        }
    }
}
