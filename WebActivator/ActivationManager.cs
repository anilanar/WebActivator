using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;


namespace AppActivator
{
    public class ActivationManager
    {
        private static bool _hasInited;
        private static List<Assembly> _assemblies;

        // For unit test purpose
        public static void Reset()
        {
            _hasInited = false;
            _assemblies = null;
        }

        public static void Run()
        {
            if (!_hasInited)
            {
                // In CBM mode, pass true so that only the methods that have RunInDesigner=true get called
                RunPreStartMethods(false);
            }
        }

        private static IEnumerable<Assembly> Assemblies
        {
            get
            {
                if (_assemblies == null)
                {
                    // Cache the list of relevant assemblies, since we need it for both Pre and Post
                    _assemblies = new List<Assembly>();
                    foreach (var assemblyFile in GetAssemblyFiles())
                    {
                        try
                        {
                            // Ignore assemblies we can't load. They could be native, etc...
                            _assemblies.Add(Assembly.LoadFrom(assemblyFile));
                        }
                        catch (Win32Exception) { }
                        catch (ArgumentException) { }
                        catch (FileNotFoundException) { }
                        catch (PathTooLongException) { }
                        catch (BadImageFormatException) { }
                        catch (SecurityException) { }
                    }
                }

                return _assemblies;
            }
        }

        private static IEnumerable<string> GetAssemblyFiles()
        {
            // When running under ASP.NET, find assemblies in the bin folder.
            // Outside of ASP.NET, use whatever folder WebActivator itself is in
            string directory = AppDomain.CurrentDomain.BaseDirectory;
            return Directory.GetFiles(directory, "*.dll");
        }

        public static void RunPreStartMethods(bool designerMode = false)
        {
            RunActivationMethods<PreApplicationStartMethodAttribute>(designerMode);
        }

        public static void RunShutdownMethods()
        {
            RunActivationMethods<ApplicationShutdownMethodAttribute>();
        }

        // Call the relevant activation method from all assemblies
        private static void RunActivationMethods<T>(bool designerMode = false) where T : BaseActivationMethodAttribute
        {
            var attribs = Assemblies.SelectMany(assembly => assembly.GetActivationAttributes<T>())
                                    .OrderBy(att => att.Order);

            foreach (var activationAttrib in attribs)
            {
                // Don't run it in designer mode, unless the attribute explicitly asks for that
                if (!designerMode || activationAttrib.ShouldRunInDesignerMode())
                {
                    activationAttrib.InvokeMethod();
                }
            }
        }
    }
}
