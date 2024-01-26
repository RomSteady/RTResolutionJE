using Microsoft.Win32;
using Mono.Cecil;
using Mono.Cecil.Cil;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;

namespace RTResolutionJE
{
    internal static class Program
    {
        private static string GamePath = "";
        private static readonly List<string> FindDetails = new List<string>();
        private static MemoryStream reachprofilestream;
        private static AssemblyDefinition terraria;
        private static AssemblyDefinition rtrhooks;

        private static List<Process> GetProcessesByName(
          string machine,
          string filter,
          RegexOptions options)
        {
            List<Process> processList = new List<Process>();
            Process[] processes = Process.GetProcesses(machine);
            Regex regex = new Regex(filter, options);
            foreach (Process process in processes)
            {
                if (regex.IsMatch(process.ProcessName))
                {
                    processList.Add(process);
                }
                else
                {
                    process.Dispose();
                }
            }
            return processList;
        }

        private static bool FindGame()
        {

            List<Process> processesByName = GetProcessesByName(".", "steam", RegexOptions.IgnoreCase);
            string path1 = string.Empty;
            foreach (Process process in processesByName)
            {
                try
                {
                    if (process.MainModule.ModuleName.ToLower() == "steam.exe")
                    {
                        FindDetails.Add("Steam.exe process found.");
                        FileInfo fileInfo = new FileInfo(process.MainModule.FileName);
                        path1 = fileInfo.DirectoryName;
                        DirectoryInfo[] directories1 = fileInfo.Directory.GetDirectories("steamapps");
                        if (directories1.Count() > 0)
                        {
                            FindDetails.Add("Steamapps folder found.");
                            DirectoryInfo[] directories2 = directories1[0].GetDirectories("common");
                            if (directories2.Count() > 0)
                            {
                                FindDetails.Add("Common folder found.");
                                DirectoryInfo[] directories3 = directories2[0].GetDirectories("terraria");
                                if (directories3.Count() > 0)
                                {
                                    FindDetails.Add("Terraria folder found.");
                                    GamePath = directories3[0].FullName;
                                    return true;
                                }
                            }
                        }
                    }
                }
                catch (Win32Exception)
                {
                    // Ignoring
                }
            }
            if (FindDetails.Count == 0)
            {
                FindDetails.Add("Could not find a process named Steam.exe running.");
            }
            else if (!string.IsNullOrWhiteSpace(path1))
            {
                string steamConfig = Path.Combine(path1, "config/config.vdf");
                FindDetails.Add(string.Format("Checking Steam config at {0}", steamConfig));
                GamePath = ParseConfig(steamConfig, "InstallConfigStore/Software/Valve/Steam/apps/105600/installdir");
            }
            if (string.IsNullOrWhiteSpace(GamePath))
            {
                FindDetails.Add("Steam version not found.  Checking registry for location of GOG.com version.");
                GamePath = Convert.ToString(Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\re-logic\\terraria", "install_path", ""));
            }
            if (string.IsNullOrWhiteSpace(GamePath))
            {
                FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog
                {
                    ShowNewFolderButton = false,
                    Description = "RTResolution couldn't automatically find your Terraria folder.  Please select your Terraria folder."
                };
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = folderBrowserDialog.SelectedPath;
                    if (File.Exists(Path.Combine(selectedPath, "Terraria.exe")))
                    {
                        GamePath = selectedPath;
                    }
                    else
                    {
                        FindDetails.Add(string.Format("Terraria.exe not found in " + selectedPath));
                    }
                }
            }
            return !string.IsNullOrWhiteSpace(GamePath);
        }

        private static string ParseConfig(string steamConfig, string configNode)
        {
            Stack<string> stringStack = new Stack<string>();
            string str1 = string.Empty;
            using (StreamReader streamReader = new StreamReader(steamConfig))
            {
                while (!streamReader.EndOfStream)
                {
                    string str2 = streamReader.ReadLine().Trim();
                    if (str2.StartsWith("{"))
                    {
                        stringStack.Push(str1);
                    }
                    else if (str2.StartsWith("}"))
                    {
                        _ = stringStack.Pop();
                    }
                    else if (str2.StartsWith("\""))
                    {
                        string[] array = str2.Split('\t').Where<string>(a => !string.IsNullOrWhiteSpace(a)).ToArray<string>();
                        str1 = array[0].Trim('"');
                        if (array.Length > 1 && (string.Join("/", stringStack.ToArray().Reverse<string>()) + "/" + str1).Equals(configNode, StringComparison.InvariantCultureIgnoreCase))
                        {
                            return array[1].Trim('"').Replace("\\\\", "\\");
                        }
                    }
                }
            }
            return null;
        }

        private static string SaveGameFolder()
        {
            return Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "./My Games/Terraria"));
        }

        private static void MakeLargeAddressAware(string file)
        {
            using (FileStream fileStream = File.Open(file, FileMode.Open, FileAccess.ReadWrite))
            {
                BinaryReader binaryReader = new BinaryReader(fileStream);
                BinaryWriter binaryWriter = new BinaryWriter(fileStream);
                if (binaryReader.ReadInt16() != 23117)
                {
                    return;
                }

                binaryReader.BaseStream.Position = 60L;
                int num1 = binaryReader.ReadInt32();
                binaryReader.BaseStream.Position = num1;
                if (binaryReader.ReadInt32() != 17744)
                {
                    return;
                }

                binaryReader.BaseStream.Position += 18L;
                long position = binaryReader.BaseStream.Position;
                short num2 = binaryReader.ReadInt16();
                if ((num2 & 32) == 32)
                {
                    return;
                }

                short num3 = (short)((int)num2 | 32);
                _ = binaryWriter.Seek((int)position, SeekOrigin.Begin);
                binaryWriter.Write(num3);
                binaryWriter.Flush();
            }
        }

        public static TypeDefinition FindTypeInAssembly(AssemblyDefinition assembly, string className)
        {
            foreach (ModuleDefinition module in assembly.Modules)
            {
                foreach (TypeDefinition type in module.Types)
                {
                    if (type.FullName == className)
                    {
                        return type;
                    }
                }
            }
            throw new KeyNotFoundException(string.Format("Class '{0}' not found.", className));
        }

        public static MethodDefinition FindMethodInAssembly(
          AssemblyDefinition assembly,
          string methodName)
        {
            foreach (ModuleDefinition module in assembly.Modules)
            {
                foreach (TypeDefinition type in module.Types)
                {
                    foreach (MethodDefinition method in type.Methods)
                    {
                        if (method.FullName == methodName)
                        {
                            return method;
                        }
                    }
                }
            }
            throw new KeyNotFoundException(string.Format("Method '{0}' not found.", methodName));
        }

        public static FieldDefinition FindFieldInAssembly(
            AssemblyDefinition assembly,
            string fieldName)
        {
            foreach (ModuleDefinition module in assembly.Modules)
            {
                foreach (TypeDefinition type in module.Types)
                {
                    foreach (FieldDefinition field in type.Fields)
                    {
                        if (field.FullName.EndsWith(fieldName))
                        {
                            return field;
                        }
                    }
                }
            }
            throw new KeyNotFoundException(string.Format("Field '{0}' not found.", fieldName));
        }

        public static void ChangeDefaultInt32Value(
            MethodDefinition method,
            string fieldName,
            int newValue)
        {
            foreach (Instruction instruction in method.Body.GetILProcessor().Body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Stsfld && ((MemberReference)instruction.Operand).FullName == fieldName)
                {
                    Instruction previous = instruction.Previous;
                    if (previous.OpCode == OpCodes.Ldc_I4)
                    {
                        previous.Operand = newValue;
                        return;
                    }
                }
            }
            throw new KeyNotFoundException(string.Format("Default value not found for '{0}'.", fieldName));
        }

        private static readonly string AlreadyPatchedFieldName = "___alreadyPatchedByRTR___";
        private static bool IsAlreadyPatched()
        {
            try
            {
                FieldDefinition patched = FindFieldInAssembly(terraria, AlreadyPatchedFieldName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void EnableHighResolution()
        {
            FieldDefinition minZoomX = FindFieldInAssembly(terraria, "Terraria.Main::MinimumZoomComparerX");
            FieldDefinition minZoomY = FindFieldInAssembly(terraria, "Terraria.Main::MinimumZoomComparerY");

            TypeDefinition terrariaMain = FindTypeInAssembly(terraria, "Terraria.Main");
            terrariaMain.Fields.Add(new FieldDefinition(AlreadyPatchedFieldName, FieldAttributes.Private, minZoomX.FieldType));


            foreach (Instruction instruction in
                FindMethodInAssembly(terraria, "System.Void Terraria.Main::.cctor()").Body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Stsfld)
                {
                    FieldDefinition currInst = (Mono.Cecil.FieldDefinition)instruction.Operand;
                    if (currInst.FullName.EndsWith("Terraria.Main::MinimumZoomComparerX") ||
                        currInst.FullName.EndsWith("Terraria.Main::MinimumZoomComparerY")
                    )
                    {
                        Instruction instToPatch = instruction.Previous;
                        if (instToPatch.OpCode == OpCodes.Ldc_R4)
                        {
                            instToPatch.Operand = 8192.0f;
                        }
                        else if (instToPatch.OpCode == OpCodes.Ldc_I4)
                        {
                            instToPatch.Operand = 8192;
                        }
                    }
                }
            }

            foreach (Instruction instruction in
                FindMethodInAssembly(terraria, "System.Void Terraria.Main::CacheSupportedDisplaySizes()").Body
                .Instructions)
            {
                if (instruction.OpCode == OpCodes.Ldsfld)
                {
                    FieldDefinition currInst = (Mono.Cecil.FieldDefinition)instruction.Operand;
                    if (currInst.FullName.EndsWith("Terraria.Main::maxScreenW"))
                    {
                        instruction.Operand = minZoomX;
                    }
                    if (currInst.FullName.EndsWith("Terraria.Main::maxScreenH"))
                    {
                        instruction.Operand = minZoomY;
                    }
                }
            }

            foreach (Instruction instruction in
                FindMethodInAssembly(terraria, "System.Void Terraria.Graphics.Light.LightMap::.ctor()").Body
                .Instructions)
            {
                if (instruction.OpCode == OpCodes.Ldc_I4)
                {
                    int val = (int)instruction.Operand;
                    if (val == 203) // Width, height
                    {
                        val *= 2;
                    }
                    else if (val == 41209) // Size of array
                    {
                        val *= 4;
                    }

                    instruction.Operand = val;
                }
            }

            foreach (Instruction instruction in
                FindMethodInAssembly(terraria, "System.Void Terraria.Main::SetGraphicsProfileInternal()").Body
                .Instructions)
            {
                if (instruction.OpCode == OpCodes.Stsfld)
                {
                    FieldDefinition currInst = (Mono.Cecil.FieldDefinition)instruction.Operand;
                    if (currInst.FullName.EndsWith("Terraria.Main::maxScreenW"))
                    {
                        Instruction instToPatch = instruction.Previous;
                        if (instToPatch.OpCode == OpCodes.Ldc_R4)
                        {
                            instToPatch.Operand = 8192.0f;
                        }
                        else if (instToPatch.OpCode == OpCodes.Ldc_I4)
                        {
                            instToPatch.Operand = 8192;
                        }
                    }

                    if (currInst.FullName.EndsWith("Terraria.Main::maxScreenH"))
                    {
                        Instruction instToPatch = instruction.Previous;
                        if (instToPatch.OpCode == OpCodes.Ldc_R4)
                        {
                            instToPatch.Operand = 8192.0f;
                        }
                        else if (instToPatch.OpCode == OpCodes.Ldc_I4)
                        {
                            instToPatch.Operand = 8192;
                        }
                    }

                    if (currInst.FullName.EndsWith("Terraria.Main::_renderTargetMaxSize"))
                    {
                        Instruction instToPatch = instruction.Previous;
                        if (instToPatch.OpCode == OpCodes.Ldc_R4)
                        {
                            instToPatch.Operand = 8192.0;
                        }
                        else if (instToPatch.OpCode == OpCodes.Ldc_I4)
                        {
                            instToPatch.Operand = 8192;
                        }
                    }
                }
            }

            // Biome fix for 1.4
            MethodDefinition scene = FindMethodInAssembly(terraria,
                "System.Void Terraria.SceneMetrics::ScanAndExportToMain(Terraria.SceneMetricsScanSettings)");
            if (scene != null)
            {
                MethodDefinition recthook = FindMethodInAssembly(rtrhooks,
                    "Microsoft.Xna.Framework.Rectangle RTRHooks.Xna::ShrinkRectangle(Microsoft.Xna.Framework.Rectangle)");
                List<Instruction> instructionsToPatch = new List<Instruction>();
                foreach (Instruction instruction in scene.Body.Instructions)
                {
                    if (instruction.OpCode == OpCodes.Call)
                    {
                        // If operand is blah
                        if (instruction.Operand.ToString().Contains("Microsoft.Xna.Framework.Rectangle Terraria.WorldBuilding.WorldUtils::ClampToWorld(Terraria.World,Microsoft.Xna.Framework.Rectangle)"))
                        {
                            instructionsToPatch.Add(instruction);
                        }
                    }

                }
                ILProcessor processor = scene.Body.GetILProcessor();
                foreach (Instruction instruction in instructionsToPatch)
                {
                    Instruction newInstruction = processor.Create(OpCodes.Call, scene.Module.ImportReference(recthook));
                    processor.InsertBefore(instruction, newInstruction);
                }
            }

            // Replace SpriteBatch with SpriteBatch2
            MethodDefinition loadContent = FindMethodInAssembly(terraria,
                "System.Void Terraria.Main::LoadContent()");
            if (loadContent != null)
            {
                ILProcessor processor = loadContent.Body.GetILProcessor();
                var spriteBatch2 = FindMethodInAssembly(rtrhooks, "System.Void RTRHooks.SpriteBatch2::.ctor(Microsoft.Xna.Framework.Graphics.GraphicsDevice)");
                for (var inst = 0; inst < loadContent.Body.Instructions.Count; inst++)
                {
                    var instruction = loadContent.Body.Instructions[inst];
                    if (instruction.OpCode == OpCodes.Newobj && instruction.Operand.ToString().Contains("SpriteBatch"))
                    {
                        Instruction newInstruction = processor.Create(OpCodes.Newobj,
                            loadContent.Module.ImportReference(spriteBatch2));
                        loadContent.Body.Instructions[inst] = newInstruction;
                    }
                }
            }
        }

        [STAThread]
        private static void Main()
        {
            if (!FindGame())
            {
                FindDetails.Insert(0, "Unable to find Terraria.\n\nIf the Steam version, make sure Steam is running.\n\nIf the GOG version, make sure you've run it once.\n\nDetails:");
                _ = MessageBox.Show(string.Join("\n", FindDetails.ToArray()));
            }
            else
            {
                string saveGameFolder = SaveGameFolder();
                string fileName = string.Format("{0}\\Terraria.exe", GamePath);
                string outputProgramFile = string.Format("{0}\\Terraria.exe", saveGameFolder);
                terraria = AssemblyDefinition.ReadAssembly(fileName);
                rtrhooks = AssemblyDefinition.ReadAssembly(@".\RTRHooks.dll");
                if (!IsAlreadyPatched())
                {
                    EnableHighResolution();

                    terraria.MainModule.Resources.Add(new EmbeddedResource("Terraria.Libraries.RTRHooks.dll", ManifestResourceAttributes.Public, File.OpenRead(@".\RTRHooks.dll")));

                    terraria.Write(outputProgramFile);
                    if (reachprofilestream != null)
                    {
                        reachprofilestream.Close();
                        reachprofilestream = null;
                    }

                    MakeLargeAddressAware(outputProgramFile);

                    _ = Process.Start(GamePath);
                    _ = Process.Start(saveGameFolder);
                    _ = MessageBox.Show(
                        "A patched version of Terraria.exe has been dropped in your save game folder.\n\n" + outputProgramFile +
                        "\n\nCopy the new version of Terraria.exe file into your Terraria install folder.");
                }
                else
                {
                    _ = Process.Start(GamePath);
                    _ = MessageBox.Show(
                        "The located version of Terraria.exe in your " + GamePath + " folder is already patched by a previous version of RTResolution.  Please reset your installed version of Terraria to its default version.");
                }
            }
        }
    }
}
