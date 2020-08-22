using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Text;
using UnityEngine;
using System.IO;
using System.Reflection;
using Ionic.Zip;

namespace BadModsTxt
{
    public class BadModsTxtModule : ETGModule
    {
        public override void Init()
        {
            ETGMod.StartGlobalCoroutine(DelayedLoadMods());
        }

        private static IEnumerator DelayedLoadMods()
        {
            Debug.Log("Mods.txt is bad - starting to load mods.");
            List<string> mods = new List<string>();
            string[] array = Directory.GetFiles(ETGMod.ModsDirectory);
            for (int i = 0; i < array.Length; i++)
            {
                string fileName = Path.GetFileName(array[i]);
                if (fileName.EndsWithInvariant(".zip"))
                {
                    mods.Add(fileName);
                }
            }
            array = Directory.GetDirectories(ETGMod.ModsDirectory);
            for (int j = 0; j < array.Length; j++)
            {
                string fileName2 = Path.GetFileName(array[j]);
                if (!(fileName2 == "RelinkCache"))
                {
                    mods.Add(fileName2);
                }
            }
            if (File.Exists(ETGMod.ModsListFile))
            {
                foreach (string text in File.ReadAllLines(ETGMod.ModsListFile))
                {
                    if (!string.IsNullOrEmpty(text) && text[0] != '#')
                    {
                        if (mods.Contains(text))
                        {
                            mods.Remove(text);
                        }
                    }
                }
            }
            foreach (string text2 in mods)
            {
                if (!string.IsNullOrEmpty(text2) && text2[0] != '#')
                {
                    try
                    {
                        InitMod(text2.Trim());
                    }
                    catch (Exception e)
                    {
                        Debug.LogError("Mods.txt is bad could not load mod " + text2 + "! Check your output log / player log.");
                        ETGMod.LogDetailed(e, null);
                    }
                }
            }
            Debug.Log("Mods.txt is bad - calling Init() in loaded mods.");
            CallInNewModules("Init", null);
            Debug.Log("Mods.txt is bad - finished.");
            yield break;
        }

        public static void CallInNewModules(string methodName, object[] args = null)
        {
            Type[] array = null;
            if (args == null)
            {
                args = (object[])emptyObjectArrayInfo.GetValue(null);
                object[] emptyTypeArray = (Type[])emptyTypeArrayInfo.GetValue(null);
                args = emptyTypeArray;
            }
            for (int i = 0; i < addedModuleTypes.Count; i++)
            {
                Dictionary<string, MethodInfo> dictionary = addedModuleMethods[i];
                MethodInfo method;
                if (dictionary.TryGetValue(methodName, out method))
                {
                    if (method != null)
                    {
                        ReflectionHelper.InvokeMethod(method, loadedMods[i], args);
                    }
                }
                else
                {
                    if (array == null)
                    {
                        array = Type.GetTypeArray(args);
                    }
                    method = addedModuleTypes[i].GetMethod(methodName, array);
                    dictionary[methodName] = method;
                    if (method != null)
                    {
                        ReflectionHelper.InvokeMethod(method, loadedMods[i], args);
                    }
                }
            }
        }

        public static void InitMod(string path)
        {
            if (path.EndsWithInvariant(".zip"))
            {
                InitModZIP(path);
                return;
            }
            InitModDir(path);
        }

        public static void InitModZIP(string archive)
        {
            Debug.Log("Initializing mod ZIP " + archive + " with Mods.txt is bad.");
            if (!File.Exists(archive))
            {
                archive = Path.Combine(ETGMod.ModsDirectory, archive);
            }
            ETGModuleMetadata etgmoduleMetadata = new ETGModuleMetadata
            {
                Name = Path.GetFileNameWithoutExtension(archive),
                Version = new Version(0, 0),
                DLL = "mod.dll"
            };
            Assembly assembly = null;
            using (ZipFile zipFile = ZipFile.Read(archive))
            {
                Texture2D texture2D = null;
                foreach (ZipEntry zipEntry in zipFile.Entries)
                {
                    if (zipEntry.FileName == "metadata.txt")
                    {
                        using (MemoryStream memoryStream = new MemoryStream())
                        {
                            zipEntry.Extract(memoryStream);
                            memoryStream.Seek(0L, SeekOrigin.Begin);
                            etgmoduleMetadata = (ETGModuleMetadata)parseMethod.Invoke(null, new object[] { archive, "", memoryStream });
                            continue;
                        }
                    }
                    if (zipEntry.FileName == "icon.png")
                    {
                        texture2D = new Texture2D(2, 2);
                        texture2D.name = "icon";
                        using (MemoryStream memoryStream2 = new MemoryStream())
                        {
                            zipEntry.Extract(memoryStream2);
                            memoryStream2.Seek(0L, SeekOrigin.Begin);
                            texture2D.LoadImage(memoryStream2.GetBuffer());
                        }
                        texture2D.filterMode = FilterMode.Point;
                    }
                }
                if (texture2D != null)
                {
                    etgmoduleMetadata.Icon = texture2D;
                }
                if (!etgmoduleMetadata.Profile.RunsOn(ETGMod.BaseProfile))
                {
                    return;
                }
                foreach (ETGModuleMetadata etgmoduleMetadata2 in etgmoduleMetadata.Dependencies)
                {
                    if (!ETGMod.DependencyLoaded(etgmoduleMetadata2))
                    {
                        Debug.LogWarning(string.Concat(new object[]
                        {
                        "DEPENDENCY ",
                        etgmoduleMetadata2,
                        " OF ",
                        etgmoduleMetadata,
                        " NOT LOADED with Mods.txt is bad!"
                        }));
                        return;
                    }
                }
                AppDomain.CurrentDomain.AssemblyResolve += (ResolveEventHandler)generateModAssemblyMethod.Invoke(null, new object[] { etgmoduleMetadata });
                foreach (ZipEntry zipEntry2 in zipFile.Entries)
                {
                    string text = zipEntry2.FileName.Replace("\\", "/");
                    if (text == etgmoduleMetadata.DLL)
                    {
                        using (MemoryStream memoryStream3 = new MemoryStream())
                        {
                            zipEntry2.Extract(memoryStream3);
                            memoryStream3.Seek(0L, SeekOrigin.Begin);
                            if (etgmoduleMetadata.Prelinked)
                            {
                                assembly = Assembly.Load(memoryStream3.GetBuffer());
                                continue;
                            }
                            assembly = etgmoduleMetadata.GetRelinkedAssembly(memoryStream3);
                            continue;
                        }
                    }
                    ETGMod.Assets.AddMapping(text, new AssetMetadata(archive, text)
                    {
                        AssetType = (zipEntry2.IsDirectory ? ETGMod.Assets.t_AssetDirectory : null)
                    });
                }
            }
            if (assembly == null)
            {
                return;
            }
            assembly.MapAssets();
            foreach (Type type in assembly.GetTypes())
            {
                if (typeof(ETGModule).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    ETGModule etgmodule = (ETGModule)type.GetConstructor((Type[])emptyTypeArrayInfo.GetValue(null)).Invoke((object[])emptyObjectArrayInfo.GetValue(null));
                    if (!ETGMod.AllMods.Contains(etgmodule))
                    {
                        etgmodule.Metadata = etgmoduleMetadata;
                        ETGMod.GameMods.Add(etgmodule);
                        ETGMod.AllMods.Add(etgmodule);
                        loadedMods.Add(etgmodule);
                        addedModuleTypes.Add(type);
                        addedModuleMethods.Add(new Dictionary<string, MethodInfo>());
                    }
                }
            }
            Debug.Log("Mod " + etgmoduleMetadata.Name + " initialized with Mods.txt is bad.");
        }

        public static void InitModDir(string dir)
        {
            Debug.Log("Initializing mod directory " + dir + " with Mods.txt is bad");
            if (!Directory.Exists(dir))
            {
                dir = Path.Combine(ETGMod.ModsDirectory, dir);
            }
            ETGModuleMetadata etgmoduleMetadata = new ETGModuleMetadata
            {
                Name = Path.GetFileName(dir),
                Version = new Version(0, 0),
                DLL = "mod.dll"
            };
            Assembly assembly = null;
            string path = Path.Combine(dir, "metadata.txt");
            if (File.Exists(path))
            {
                using (FileStream fileStream = File.OpenRead(path))
                {
                    etgmoduleMetadata = (ETGModuleMetadata)parseMethod.Invoke(null, new object[] { "", dir, fileStream });
                }
            }
            foreach (ETGModuleMetadata etgmoduleMetadata2 in etgmoduleMetadata.Dependencies)
            {
                if (!ETGMod.DependencyLoaded(etgmoduleMetadata2))
                {
                    Debug.LogWarning(string.Concat(new object[]
                    {
                    "DEPENDENCY ",
                    etgmoduleMetadata2,
                    " OF ",
                    etgmoduleMetadata,
                    " NOT LOADED with Mods.txt is bad!"
                    }));
                    return;
                }
            }
            AppDomain.CurrentDomain.AssemblyResolve += (ResolveEventHandler)generateModAssemblyMethod.Invoke(null, new object[] { etgmoduleMetadata });
            if (!File.Exists(etgmoduleMetadata.DLL))
            {
                return;
            }
            if (etgmoduleMetadata.Prelinked)
            {
                assembly = Assembly.LoadFrom(etgmoduleMetadata.DLL);
            }
            else
            {
                using (FileStream fileStream2 = File.OpenRead(etgmoduleMetadata.DLL))
                {
                    assembly = etgmoduleMetadata.GetRelinkedAssembly(fileStream2);
                }
            }
            assembly.MapAssets();
            ETGMod.Assets.Crawl(dir, null);
            foreach (Type type in assembly.GetTypes())
            {
                if (typeof(ETGModule).IsAssignableFrom(type) && !type.IsAbstract)
                {
                    ETGModule etgmodule = (ETGModule)type.GetConstructor((Type[])emptyTypeArrayInfo.GetValue(null)).Invoke((object[])emptyObjectArrayInfo.GetValue(null));
                    if (!ETGMod.AllMods.Contains(etgmodule))
                    {
                        etgmodule.Metadata = etgmoduleMetadata;
                        ETGMod.GameMods.Add(etgmodule);
                        ETGMod.AllMods.Add(etgmodule);
                        loadedMods.Add(etgmodule);
                        addedModuleTypes.Add(type);
                        addedModuleMethods.Add(new Dictionary<string, MethodInfo>());
                    }
                }
            }
            Debug.Log("Mod " + etgmoduleMetadata.Name + " initialized with Mods.txt is bad.");
        }

        public override void Start()
        {
            ETGMod.StartGlobalCoroutine(DelayedStart());
        }

        private static IEnumerator DelayedStart()
        {
            Debug.Log("Mods.txt is bad - calling Start() in loaded mods.");
            CallInNewModules("Start", null);
            if (loadedMods.Count > 0)
            {
                ETGModConsole.Log("Mods.txt is bad loaded " + loadedMods.Count + " mods.");
            }
            else
            {
                ETGModConsole.Log("Mods.txt is bad didn't load any mods.");
            }
            yield return null;
            foreach(Type type in addedModuleTypes)
            {
                ((List<Type>)moduleTypesInfo.GetValue(null)).Add(type);
                ((List<Dictionary<string, MethodInfo>>)moduleMethodsInfo.GetValue(null)).Add(new Dictionary<string, MethodInfo>());
            }
            yield break;
        }

        public override void Exit()
        {
        }

        public static MethodInfo parseMethod = typeof(ETGModuleMetadata).GetMethod("Parse", BindingFlags.NonPublic | BindingFlags.Static);
        public static MethodInfo generateModAssemblyMethod = typeof(ETGMod).GetMethod("_GenerateModAssemblyResolver", BindingFlags.NonPublic | BindingFlags.Static);
        public static FieldInfo moduleTypesInfo = typeof(ETGMod).GetField("_ModuleTypes", BindingFlags.NonPublic | BindingFlags.Static);
        public static FieldInfo moduleMethodsInfo = typeof(ETGMod).GetField("_ModuleMethods", BindingFlags.NonPublic | BindingFlags.Static);
        public static FieldInfo emptyTypeArrayInfo = typeof(ETGMod).GetField("_EmptyTypeArray", BindingFlags.NonPublic | BindingFlags.Static);
        public static FieldInfo emptyObjectArrayInfo = typeof(ETGMod).GetField("_EmptyObjectArray", BindingFlags.NonPublic | BindingFlags.Static);
        private static List<Type> addedModuleTypes = new List<Type>();
        private static List<Dictionary<string, MethodInfo>> addedModuleMethods = new List<Dictionary<string, MethodInfo>>();
        private static List<ETGModule> loadedMods = new List<ETGModule>();
    }
}
