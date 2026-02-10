#define DEV // Define this to make the code drop the patched Terraria.exe in the install folder instead of the save game folder for easier debugging.  Don't forget to undefine this before building release versions.

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
    static class Program
    {
        private static string GamePath = "";
        private static List<string> FindDetails = new List<string>();
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
                    processList.Add(process);
                else
                    process.Dispose();
            }
            return processList;
        }

        private static bool FindGame()
        {

            List<Process> processesByName = Program.GetProcessesByName(".", "steam", RegexOptions.IgnoreCase);
            string path1 = string.Empty;
            foreach (Process process in processesByName)
            {
                try
                {
                    if (process.MainModule.ModuleName.ToLower() == "steam.exe")
                    {
                        Program.FindDetails.Add("Steam.exe process found.");
                        FileInfo fileInfo = new FileInfo(process.MainModule.FileName);
                        path1 = fileInfo.DirectoryName;
                        DirectoryInfo[] directories1 = fileInfo.Directory.GetDirectories("steamapps");
                        if (directories1.Count() > 0)
                        {
                            Program.FindDetails.Add("Steamapps folder found.");
                            DirectoryInfo[] directories2 = directories1[0].GetDirectories("common");
                            if (directories2.Count() > 0)
                            {
                                Program.FindDetails.Add("Common folder found.");
                                DirectoryInfo[] directories3 = directories2[0].GetDirectories("terraria");
                                if (directories3.Count() > 0)
                                {
                                    Program.FindDetails.Add("Terraria folder found.");
                                    Program.GamePath = directories3[0].FullName;
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
            if (Program.FindDetails.Count == 0)
                Program.FindDetails.Add("Could not find a process named Steam.exe running.");
            else if (!string.IsNullOrWhiteSpace(path1))
            {
                string steamConfig = Path.Combine(path1, "config/config.vdf");
                Program.FindDetails.Add(string.Format("Checking Steam config at {0}", (object)steamConfig));
                Program.GamePath = Program.ParseConfig(steamConfig, "InstallConfigStore/Software/Valve/Steam/apps/105600/installdir");
            }
            if (string.IsNullOrWhiteSpace(Program.GamePath))
            {
                Program.FindDetails.Add("Steam version not found.  Checking registry for location of GOG.com version.");
                Program.GamePath = Convert.ToString(Registry.GetValue("HKEY_LOCAL_MACHINE\\SOFTWARE\\re-logic\\terraria", "install_path", (object)""));
            }
            if (string.IsNullOrWhiteSpace(Program.GamePath))
            {
                FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
                folderBrowserDialog.ShowNewFolderButton = false;
                folderBrowserDialog.Description = "RTResolution couldn't automatically find your Terraria folder.  Please select your Terraria folder.";
                if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
                {
                    string selectedPath = folderBrowserDialog.SelectedPath;
                    if (File.Exists(Path.Combine(selectedPath, "Terraria.exe")))
                        Program.GamePath = selectedPath;
                    else
                        Program.FindDetails.Add(string.Format("Terraria.exe not found in " + selectedPath));
                }
            }
            return !string.IsNullOrWhiteSpace(Program.GamePath);
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
                        stringStack.Push(str1);
                    else if (str2.StartsWith("}"))
                        stringStack.Pop();
                    else if (str2.StartsWith("\""))
                    {
                        string[] array = ((IEnumerable<string>)str2.Split('\t')).Where<string>((Func<string, bool>)(a => !string.IsNullOrWhiteSpace(a))).ToArray<string>();
                        str1 = array[0].Trim('"');
                        if (array.Length > 1 && (string.Join("/", ((IEnumerable<string>)stringStack.ToArray()).Reverse<string>()) + "/" + str1).Equals(configNode, StringComparison.InvariantCultureIgnoreCase))
                            return array[1].Trim('"').Replace("\\\\", "\\");
                    }
                }
            }
            return (string)null;
        }

        private static string SaveGameFolder()
        {
            return Path.GetFullPath(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "./My Games/Terraria"));
        }

        private static void MakeLargeAddressAware(string file)
        {
            using (FileStream fileStream = File.Open(file, FileMode.Open, FileAccess.ReadWrite))
            {
                BinaryReader binaryReader = new BinaryReader((Stream)fileStream);
                BinaryWriter binaryWriter = new BinaryWriter((Stream)fileStream);
                if (binaryReader.ReadInt16() != (short)23117)
                    return;
                binaryReader.BaseStream.Position = 60L;
                int num1 = binaryReader.ReadInt32();
                binaryReader.BaseStream.Position = (long)num1;
                if (binaryReader.ReadInt32() != 17744)
                    return;
                binaryReader.BaseStream.Position += 18L;
                long position = binaryReader.BaseStream.Position;
                short num2 = binaryReader.ReadInt16();
                if (((int)num2 & 32) == 32)
                    return;
                short num3 = (short)((int)num2 | 32);
                binaryWriter.Seek((int)position, SeekOrigin.Begin);
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
                        return type;
                }
            }
            throw new KeyNotFoundException(String.Format("Class '{0}' not found.", className));
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
                            return method;
                    }
                }
            }
            throw new KeyNotFoundException(String.Format("Method '{0}' not found.", methodName));
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
                            return field;
                    }
                }
            }
            throw new KeyNotFoundException(String.Format("Field '{0}' not found.", fieldName));
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
                        previous.Operand = (object)newValue;
                        return;
                    }
                }
            }
            throw new KeyNotFoundException(string.Format("Default value not found for '{0}'.", (object)fieldName));
        }

        private static string AlreadyPatchedFieldName = "___alreadyPatchedByRTR___";
        private static bool IsAlreadyPatched()
        {
            try
            {
                var patched = FindFieldInAssembly(Program.terraria, AlreadyPatchedFieldName);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void EnableHighResolution()
        {
            var minZoomX = FindFieldInAssembly(Program.terraria, "Terraria.Main::MaxWorldViewSizeWidth");
            var minZoomY = FindFieldInAssembly(Program.terraria, "Terraria.Main::MaxWorldViewSizeHeight");

            var terrariaMain = FindTypeInAssembly(Program.terraria, "Terraria.Main");
            terrariaMain.Fields.Add(new FieldDefinition(AlreadyPatchedFieldName, FieldAttributes.Private, minZoomX.FieldType));

            // First, we'll patch the instructions that cap the maximum resoution for Terraria.
            foreach (Instruction instruction in Program
                .FindMethodInAssembly(Program.terraria, "System.Void Terraria.Main::.cctor()").Body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Stsfld)
                {
                    var currInst = (Mono.Cecil.FieldDefinition)instruction.Operand;
                    if (
                        currInst.FullName.EndsWith("Terraria.Main::maxScreenW") ||
                        currInst.FullName.EndsWith("Terraria.Main::maxScreenH") ||
                        currInst.FullName.EndsWith("Terraria.Main::_renderTargetMaxSize")
                    )
                    {
                        var instToPatch = instruction.Previous;
                        if (instToPatch.OpCode == OpCodes.Ldc_R4)
                        {
                            instToPatch.Operand = (object)8192.0f;
                        }
                        else if (instToPatch.OpCode == OpCodes.Ldc_I4)
                        {
                            instToPatch.Operand = (object)8192;
                        }
                    }
                }
            }

            foreach (Instruction instruction in Program
                .FindMethodInAssembly(Program.terraria, "System.Void Terraria.Main::.cctor()").Body.Instructions)
            {
                if (instruction.OpCode == OpCodes.Stsfld)
                {
                    var currInst = (Mono.Cecil.FieldDefinition)instruction.Operand;
                    if (currInst.FullName.EndsWith("Terraria.Main::MaxWorldViewSize"))
                    {
                        // Step back to previous load instructions
                        // Stepping back from newobj -> ldc_i4 -> ldc_i4 -> base
                        var baseInstruction = instruction.Previous;
                        baseInstruction.Previous.Operand = (object)8192;
                        baseInstruction.Previous.Previous.Operand = (object)8192;   
                    }
                }
            }

            // Next, we'll patch the lightmap constructor to increase the size of the lightmap arrays.
            foreach (Instruction instruction in Program
                .FindMethodInAssembly(Program.terraria, "System.Void Terraria.Graphics.Light.LightMap::.ctor()").Body
                .Instructions)
            {
                if (instruction.OpCode == OpCodes.Ldc_I4)
                {
                    int val = (int) instruction.Operand;
                    if (val == 203) // Width, height
                    {
                        val *= 2;
                    } 
                    else if (val == 41209) // Size of array
                    {
                        val *= 4;
                    }

                    instruction.Operand = (object) val;
                }
            }


            foreach (Instruction instruction in Program
                .FindMethodInAssembly(Program.terraria, "System.Void Terraria.Main::SetGraphicsProfileInternal()").Body
                .Instructions)
            {
                if (instruction.OpCode == OpCodes.Stsfld)
                {
                    var currInst = (Mono.Cecil.FieldDefinition)instruction.Operand;
                    if (currInst.FullName.EndsWith("Terraria.Main::maxScreenW"))
                    {
                        var instToPatch = instruction.Previous;
                        if (instToPatch.OpCode == OpCodes.Ldc_R4)
                        {
                            instToPatch.Operand = (object)8192.0f;
                        }
                        else if (instToPatch.OpCode == OpCodes.Ldc_I4)
                        {
                            instToPatch.Operand = (object)8192;
                        }
                    }

                    if (currInst.FullName.EndsWith("Terraria.Main::maxScreenH"))
                    {
                        var instToPatch = instruction.Previous;
                        if (instToPatch.OpCode == OpCodes.Ldc_R4)
                        {
                            instToPatch.Operand = (object)8192.0f;
                        }
                        else if (instToPatch.OpCode == OpCodes.Ldc_I4)
                        {
                            instToPatch.Operand = (object)8192;
                        }
                    }

                    if (currInst.FullName.EndsWith("Terraria.Main::_renderTargetMaxSize"))
                    {
                        var instToPatch = instruction.Previous;
                        if (instToPatch.OpCode == OpCodes.Ldc_R4)
                        {
                            instToPatch.Operand = (object)8192.0;
                        }
                        else if (instToPatch.OpCode == OpCodes.Ldc_I4)
                        {
                            instToPatch.Operand = (object)8192;
                        }
                    }
                }
            }

/*
            // Biome fix for 1.4
            var scene = FindMethodInAssembly(Program.terraria,
                "System.Void Terraria.SceneMetrics::ScanAndExportToMain(Terraria.SceneMetricsScanSettings)");
            if (scene != null)
            {
                var recthook = FindMethodInAssembly(rtrhooks,
                    "Microsoft.Xna.Framework.Rectangle RTRHooks.Xna::ShrinkRectangle(Microsoft.Xna.Framework.Rectangle)");
                var instructionsToPatch = new List<Instruction>();
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
                var processor = scene.Body.GetILProcessor();
                foreach (var instruction in instructionsToPatch)
                {
                    var newInstruction = processor.Create(Mono.Cecil.Cil.OpCodes.Call, scene.Module.Import(recthook));
                    processor.InsertBefore(instruction, newInstruction);
                }
            }
            */
        }

        private static void EnableErrorReporting()
        {
            var constructor  = FindMethodInAssembly(Program.terraria,
                "System.Void Terraria.Program::SetupLogging()");
            if (constructor != null)
            {
                var errorHook = FindMethodInAssembly(rtrhooks,
                    "System.Void RTRHooks.ErrorHandler::Setup()");

                var processor = constructor.Body.GetILProcessor();

                var newInstruction = processor.Create(Mono.Cecil.Cil.OpCodes.Call, constructor.Module.Import(errorHook));
                processor.InsertBefore(constructor.Body.Instructions[0], newInstruction);
                processor.InsertBefore(newInstruction, processor.Create(OpCodes.Nop));
                processor.InsertAfter(newInstruction, processor.Create(OpCodes.Nop));
            }
        }


        [STAThread]
        private static void Main()
        {
            try
            {
                if (!Program.FindGame())
                {
                    Program.FindDetails.Insert(0, "Unable to find Terraria.\n\nIf the Steam version, make sure Steam is running.\n\nIf the GOG version, make sure you've run it once.\n\nDetails:");
                    MessageBox.Show(String.Join("\n", Program.FindDetails.ToArray()));
                }
                else
                {
                    string saveGameFolder = Program.SaveGameFolder();
                    #if DEV
                        string fileName = String.Format("{0}\\Terraria1.exe", Program.GamePath);
                        string outputProgramFile = String.Format("{0}\\Terraria.exe", Program.GamePath);
                    #else
                        string fileName = String.Format("{0}\\Terraria.exe", Program.GamePath);
                        string outputProgramFile = String.Format("{0}\\Terraria.exe", saveGameFolder);
                    #endif
                    Program.terraria = AssemblyDefinition.ReadAssembly(fileName);
                    Program.rtrhooks = AssemblyDefinition.ReadAssembly(@".\RTRHooks.dll");
                    if (!Program.IsAlreadyPatched())
                    {
                        Program.EnableHighResolution();
                        //Program.EnableErrorReporting();

                        terraria.MainModule.Resources.Add(new EmbeddedResource("Terraria.Libraries.RTRHooks.dll", ManifestResourceAttributes.Public, File.OpenRead(@".\RTRHooks.dll")));
                        terraria.MainModule.Resources.Remove(terraria.MainModule.Resources.FirstOrDefault(r => r.Name == "Microsoft.Xna.Framework.RuntimeProfile"));
                        terraria.MainModule.Resources.Add(new EmbeddedResource("Microsoft.Xna.Framework.RuntimeProfile", ManifestResourceAttributes.Public, File.OpenRead(@".\hidef-profile.txt")));

                        Program.terraria.Write(outputProgramFile);
                        if (Program.reachprofilestream != null)
                        {
                            Program.reachprofilestream.Close();
                            Program.reachprofilestream = (MemoryStream)null;
                        }

                        Program.MakeLargeAddressAware(outputProgramFile);

                        System.Diagnostics.Process.Start(Program.GamePath);
                        System.Diagnostics.Process.Start(saveGameFolder);
                        MessageBox.Show(
                            "A patched version of Terraria.exe has been dropped in your save game folder.\n\n" + outputProgramFile +
                            "\n\nCopy the new version of Terraria.exe file into your Terraria install folder.");
                    }
                    else
                    {
                        System.Diagnostics.Process.Start(Program.GamePath);
                        MessageBox.Show(
                            "The located version of Terraria.exe in your " + Program.GamePath + " folder is already patched by a previous version of RTResolution.  Please reset your installed version of Terraria to its default version.");
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("An error occurred:\n\n" + ex.ToString());
            }
        }
    }
}
