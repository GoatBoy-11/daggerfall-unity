// Offline reproduction of how Daggerfall Unity compiles and loads mod sources at runtime:
// DFU's CSharpCompiler.CodeCompiler (Mono.CSharp from mcs.dll, System.Reflection.Emit, in memory),
// referencing the game's Managed assemblies. After compiling, every type is laid out and every
// method JIT-prepared, which surfaces TypeLoadExceptions the game would throw.
// Built and run by runtime-check.sh. Usage: harness.exe <ManagedDir> <source.cs>...
using System;
using System.CodeDom.Compiler;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

public static class RuntimeCompileHarness
{
    public static int Main(string[] args)
    {
        string managed = args[0];
        CompilerParameters p = new CompilerParameters();
        foreach (string dll in Directory.GetFiles(managed, "*.dll"))
        {
            try
            {
                Assembly.LoadFrom(dll);
                p.ReferencedAssemblies.Add(dll);
            }
            catch (Exception)
            {
                // native or unloadable outside the player; DFU would not reference it either
            }
        }
        p.GenerateExecutable = false;
        p.GenerateInMemory = true;

        string[] sources = new string[args.Length - 1];
        for (int i = 1; i < args.Length; i++)
            sources[i - 1] = File.ReadAllText(args[i]);

        CompilerResults result = new CSharpCompiler.CodeCompiler().CompileAssemblyFromSourceBatch(p, sources);
        if (result.Errors.Count > 0)
        {
            foreach (CompilerError e in result.Errors)
                Console.WriteLine("COMPILE " + (e.IsWarning ? "warning" : "error") + " CS" + e.ErrorNumber + " " + args[1 + SafeIndex(e.FileName)] + "(" + e.Line + "): " + e.ErrorText);
            if (result.Errors.HasErrors)
                return 1;
        }

        int failures = 0;
        Type[] types;
        try
        {
            types = result.CompiledAssembly.GetTypes();
        }
        catch (ReflectionTypeLoadException e)
        {
            foreach (Exception le in e.LoaderExceptions)
                Console.WriteLine("LOAD " + le.Message);
            return 1;
        }

        const BindingFlags all = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;
        foreach (Type t in types)
        {
            try
            {
                t.GetFields(all);
                RuntimeHelpers.RunClassConstructor(t.TypeHandle);
                foreach (MethodInfo m in t.GetMethods(all))
                {
                    if (m.IsAbstract || m.ContainsGenericParameters)
                        continue;
                    RuntimeHelpers.PrepareMethod(m.MethodHandle);
                }
                foreach (ConstructorInfo c in t.GetConstructors(all))
                    RuntimeHelpers.PrepareMethod(c.MethodHandle);
            }
            catch (Exception e)
            {
                Exception inner = e is TypeInitializationException && e.InnerException != null ? e.InnerException : e;
                Console.WriteLine("TYPE " + t.FullName + ": " + inner.GetType().Name + ": " + inner.Message);
                failures++;
            }
        }

        Console.WriteLine(failures == 0 ? "runtime-check: OK (" + types.Length + " types)" : "runtime-check: " + failures + " type(s) failed");
        return failures == 0 ? 0 : 1;
    }

    static int SafeIndex(string fileName)
    {
        int i;
        return int.TryParse(fileName, out i) ? i : 0;
    }
}
