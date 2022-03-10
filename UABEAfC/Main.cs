﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.ObjectModel;
using System.IO;

using System.ComponentModel;
using UABEAvalonia;
using UABEAvalonia.Plugins;
using TexturePlugin;
using TextAssetPlugin;

using AssetsTools.NET;
using AssetsTools.NET.Extra;

namespace UABEC {
    internal class Main {

        public AssetWorkspace Workspace { set; get; }
        public AssetsManager am = new AssetsManager();//{  get => Workspace.am; }

        private BundleFileInstance bundleInst;
        private ObservableCollection<AssetInfoDataGridItem> dataGridItems;

        public List<Tuple<AssetsFileInstance, byte[]>> ChangedAssetsDatas { get; set; }

        private Dictionary<string, BundleReplacer> newFiles = new Dictionary<string, BundleReplacer>();

        //private PluginManager pluginManager;
        // List<UABEAPluginMenuInfo> plugInfs;


        private string selectedBundleName = "";

        private ArgCont ag;

        /*
        UABEAC [AssetFile]
	        Display item list.

        UABEAC [AssetFile] -export [ItemName]
            Export contents of asset file/bundle.

        UABEAC [AssetFile] -import [ItemName] [ImportFile]
         
         */

        public Main(ArgCont args) {
            //string filePath = @"C:\Program Files (x86)\Steam\steamapps\common\Minion Masters\MinionMasters_Data\sharedassets0.assets";
            //string filePath2 = @"C:\Program Files (x86)\Steam\steamapps\common\Minion Masters\MinionMasters_Data\StreamingAssets\AssetBundles\scenes\newbundle";


            //args[0]=filePath;
            //args[1]="-export";
            //args[2]="";
            //args[3]=filePath2;
            // UABEAfCL  
            ag = args;

            Init(); //Load classdata.tpk to AssetManager.

            ag.assetFilePath += "_temp";    // working file

            System.IO.File.Copy(ag.assetFilePathOrigin, ag.assetFilePath, true);  //copy


            try {
                Proc(ag.assetFilePath);
            } catch (Exception ex) {
                Console.WriteLine("Error");
                Console.WriteLine(ex.ToString());



            }
        }




        private void Init() {
            //am = new AssetsManager();
            string classDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "classdata.tpk");
            if (File.Exists(classDataPath)) {
                am.LoadClassPackage(classDataPath);
            } else {
                //await MessageBoxUtil.ShowDialog(this, "Error", "Missing classdata.tpk by exe.\nPlease make sure it exists.");
                Console.WriteLine("Missing classdata.tpk by exe.\nPlease make sure it exists.");
            }
        }


        public void Proc(string filePath) {





            DetectedFileType fileType = AssetBundleDetector.DetectFileType(filePath);

            am.UnloadAllAssetsFiles(true);
            am.UnloadAllBundleFiles();
            AssetsFileInstance fileInst = null;

            bool fromBundle = false;
            if (fileType == DetectedFileType.BundleFile) {
                fromBundle = true;
                bundleInst = am.LoadBundleFile(filePath, false);
                //don't pester user to decompress if it's only the header that is compressed
                if (AssetBundleUtil.IsBundleDataCompressed(bundleInst.file)) {
                    //AskLoadCompressedBundle(bundleInst);
                } else {
                    if ((bundleInst.file.bundleHeader6.flags & 0x3F) != 0) //header is compressed (most likely)
                        bundleInst.file.UnpackInfoOnly();
                    //LoadBundle(bundleInst);
                }


                int index = 0;
                if (ag.option == "show") {
                    index = SelectBundle();
                } else {
                    index = 0;
                }
                fileInst = BundleLoad(index);
                Info();// fileinst = load[selected assetfile in bundle]
                //fileInst = am.LoadAssetsFile(filePath, true);
                //InfoWindow info = new InfoWindow(am, new List<AssetsFileInstance> { fileInst }, true);
            } else {

                fileInst = am.LoadAssetsFile(filePath, true);
                //InfoWindow info = new InfoWindow(am, new List<AssetsFileInstance> { fileInst }, false);
            }


            string uVer = fileInst.file.typeTree.unityVersion;
            if (am.LoadClassDatabaseFromPackage(uVer) == null) {
                // something            
            }
            List<AssetsFileInstance> assetsFiles = new List<AssetsFileInstance> { fileInst };


            Workspace = new AssetWorkspace(am, fromBundle);
            Workspace.ItemUpdated += Workspace_ItemUpdated;

            LoadAllAssetsWithDeps(assetsFiles);
            MakeDataGridItems();

            //pluginManager = new PluginManager();
            //pluginManager.LoadPluginsInDirectory("plugins");

            ChangedAssetsDatas = new List<Tuple<AssetsFileInstance, byte[]>>();

            List<AssetInfoDataGridItem> gridItems = dataGridItems.ToList();

            if (ag.option == "show") {
                ConsoleWriteItemList(gridItems);
                return; //show list, and end.
            }



            List<AssetContainer> selection = new List<AssetContainer>();
            foreach (var item in gridItems) {
                if (ag.assetName == item.Name && ag.fileId == item.FileID.ToString() && ag.pathId == item.PathID.ToString()) {
                    selection.Add(item.assetContainer);
                }
            }
            if (selection.Count == 0) {
                //error
                Console.WriteLine("Error: Invalid ItemName");
                return;
            }



            int textureId = AssetHelper.FindAssetClassByName(am.classFile, "Texture2D").classId;
            int textId = AssetHelper.FindAssetClassByName(am.classFile, "TextAsset").classId;

            string exPath = Path.GetDirectoryName(ag.assetFilePathOrigin) + "\\" + ag.bundleName + "_" + ag.assetName + "_" + ag.fileId + "_" + ag.pathId;


            if (ag.option == "-export") {
                if (selection[0].ClassId == textureId) {
                    ExportTextureOption et = new ExportTextureOption();
                    et.ExecutePlugin(exPath, Workspace, selection);
                } else if (selection[0].ClassId == textId) {
                    ExportTextAssetOption it = new ExportTextAssetOption();
                    it.ExecutePlugin(exPath + ".txt", Workspace, selection);
                } else {      //Raw Data
                    SingleExportRaw(selection);
                }


                //menuPlugInf = plugInfs[exp];
                //UABEAPluginOption? plugOpt = menuPlugInf.pluginOpt;
                //plugOpt.ExecutePlugin("" + ag.assetName + ".png", Workspace, selection);

                Console.WriteLine("export end");
                return;
            }



            if (ag.option == "-import") {
                if (selection[0].ClassId == textureId) {
                    EditTextureOption et = new EditTextureOption();
                    et.ExecutePlugin("" + ag.importFilePath + "", Workspace, selection);
                } else if (selection[0].ClassId == textId) {
                    ImportTextAssetOption it = new ImportTextAssetOption();
                    it.ExecutePlugin("" + ag.importFilePath + "", Workspace, selection);

                } else {      //Raw Data
                    SingleImportRaw(selection);
                }
            }



            //List<AssetContainer> selection = GetSelectedAssetsReplaced();
            //SingleExportRaw(selection);


            // SingleImportRaw(selection);

            SaveFile();
            if (fileType == DetectedFileType.BundleFile) {
                BundlePreSave();
                using (FileStream fs = File.OpenWrite(ag.assetFilePathOrigin))
                using (AssetsFileWriter w = new AssetsFileWriter(fs)) {
                    bundleInst.file.Write(w, newFiles.Values.ToList());
                }
            }

            Console.WriteLine("import end");

            return;
        }



        private void ConsoleWriteItemList(List<AssetInfoDataGridItem> gridItems) {

            Console.WriteLine("      ItemName   [Bundle/Name:FileId:PathId]                             Byte            Type");
            Console.WriteLine("----------------------------------------------------------------------");
            StringBuilder sb = new StringBuilder();
            string t = "";
            foreach (var item in gridItems) {

                t = string.Format("{0,-70}", selectedBundleName + "/" + item.Name + ":" + item.FileID + ":" + item.PathID);
                sb.AppendLine(t + "   " + string.Format("{0,-9}", item.Size) + "     " + item.Type);

            }
            Console.WriteLine(sb.ToString());

        }



        private void LoadAllAssetsWithDeps(List<AssetsFileInstance> files) {
            HashSet<string> fileNames = new HashSet<string>();
            foreach (AssetsFileInstance file in files) {
                RecurseGetAllAssets(file, Workspace.LoadedAssets, Workspace.LoadedFiles, fileNames);
            }
        }

        private void RecurseGetAllAssets(AssetsFileInstance fromFile, Dictionary<AssetID, AssetContainer> conts, List<AssetsFileInstance> files, HashSet<string> fileNames) {
            fromFile.table.GenerateQuickLookupTree();

            files.Add(fromFile);
            fileNames.Add(fromFile.path.ToLower());

            foreach (AssetFileInfoEx info in fromFile.table.assetFileInfo) {
                AssetContainer cont = new AssetContainer(info, fromFile);
                conts.Add(cont.AssetId, cont);
            }

            for (int i = 0; i < fromFile.dependencies.Count; i++) {
                AssetsFileInstance dep = fromFile.GetDependency(am, i);
                if (dep == null)
                    continue;
                string depPath = dep.path.ToLower();
                if (!fileNames.Contains(depPath)) {
                    RecurseGetAllAssets(dep, conts, files, fileNames);
                } else {
                    continue;
                }
            }
        }


        private void Workspace_ItemUpdated(AssetsFileInstance file, AssetID assetId) {
            int fileId = Workspace.LoadedFiles.IndexOf(file);
            long pathId = assetId.pathID;

            var gridItem = dataGridItems.FirstOrDefault(i => i.FileID == fileId && i.PathID == pathId);

            if (Workspace.LoadedAssets.ContainsKey(assetId)) {
                //added/modified entry
                if (file != null) {
                    AssetContainer? cont = Workspace.GetAssetContainer(file, 0, assetId.pathID);
                    if (cont != null) {
                        if (gridItem != null) {
                            gridItem.assetContainer = cont;

                            //SetFieldModified(gridItem);
                            gridItem.Modified = "*";
                            gridItem.Update();
                        } else {
                            gridItem = AddDataGridItem(cont, true);
                            gridItem.Modified = "*";
                        }
                    }
                }
            } else {
                //removed entry
                if (gridItem != null) {
                    dataGridItems.Remove(gridItem);
                }
            }
        }


        private ObservableCollection<AssetInfoDataGridItem> MakeDataGridItems() {
            dataGridItems = new ObservableCollection<AssetInfoDataGridItem>();

            Workspace.GenerateAssetsFileLookup();

            foreach (AssetContainer cont in Workspace.LoadedAssets.Values) {
                AddDataGridItem(cont);
            }
            return dataGridItems;
        }

        private AssetInfoDataGridItem AddDataGridItem(AssetContainer cont, bool isNewAsset = false) {
            AssetsFileInstance thisFileInst = cont.FileInstance;
            AssetsFile thisFile = thisFileInst.file;

            string name;
            string container;
            string type;
            int fileId;
            long pathId;
            int size;
            string modified;

            container = string.Empty;
            fileId = Workspace.LoadedFiles.IndexOf(thisFileInst); //todo faster lookup THIS IS JUST FOR TESTING
            pathId = cont.PathId;
            size = (int)cont.Size;
            modified = "";

            Extensions.GetUABENameFast(thisFile, Workspace.am.classFile, cont.FileReader, cont.FilePosition, cont.ClassId, cont.MonoId, true, out name, out type);

            var item = new AssetInfoDataGridItem {
                Name = name,
                Container = container,
                Type = type,
                TypeID = cont.ClassId,
                FileID = fileId,
                PathID = pathId,
                Size = size,
                Modified = modified,
                assetContainer = cont
            };

            if (!isNewAsset)
                dataGridItems.Add(item);
            else
                dataGridItems.Insert(0, item);
            return item;
        }


        private List<AssetContainer> GetSelectedAssetsReplaced() {
            List<AssetInfoDataGridItem> gridItems = dataGridItems.ToList();
            List<AssetContainer> exts = new List<AssetContainer>();
            foreach (var gridItem in gridItems) {
                exts.Add(gridItem.assetContainer);
            }
            return exts;
        }
        private void SingleExportRaw(List<AssetContainer> selection) {
            AssetContainer selectedCont = selection[0];
            AssetsFileInstance selectedInst = selectedCont.FileInstance;

            Extensions.GetUABENameFast(selectedCont, am.classFile, false, out string assetName, out string _);
            /*
            SaveFileDialog sfd = new SaveFileDialog();
            sfd.Title = "Save As";
            sfd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "Raw Unity Asset", Extensions = new List<string>() { "dat" } }
            };
            sfd.InitialFileName = $"{assetName}-{Path.GetFileName(selectedInst.path)}-{selectedCont.PathId}.dat";

            string file = await sfd.ShowAsync(this);
            */
            string file = Path.GetDirectoryName(ag.assetFilePathOrigin) + "\\" + ag.bundleName + "_" + ag.assetName + "_" + ag.fileId + "_" + ag.pathId;
            if (file != null && file != string.Empty) {
                using (FileStream fs = File.OpenWrite(file)) {
                    AssetImportExport dumper = new AssetImportExport();
                    dumper.DumpRawAsset(fs, selectedCont.FileReader, selectedCont.FilePosition, selectedCont.Size);
                }
            }
        }
        private void SingleImportRaw(List<AssetContainer> selection) {
            AssetContainer selectedCont = selection[0];
            AssetsFileInstance selectedInst = selectedCont.FileInstance;
            /*
            OpenFileDialog ofd = new OpenFileDialog();
            ofd.Title = "Open";
            ofd.Filters = new List<FileDialogFilter>() {
                new FileDialogFilter() { Name = "Raw Unity Asset", Extensions = new List<string>() { "dat" } }
            };
            string[] fileList = await ofd.ShowAsync(this);
            if (fileList.Length == 0)
                return;
            
            string file = fileList[0];
            */
            string file = ag.importFilePath;

            if (file != null && file != string.Empty) {
                using (FileStream fs = File.OpenRead(file)) {
                    AssetImportExport importer = new AssetImportExport();
                    byte[] bytes = importer.ImportRawAsset(fs);

                    AssetsReplacer replacer = AssetImportExport.CreateAssetReplacer(selectedCont, bytes);
                    Workspace.AddReplacer(selectedInst, replacer, new MemoryStream(bytes));
                }
            }
        }




        private void SaveFile() {
            var fileToReplacer = new Dictionary<AssetsFileInstance, List<AssetsReplacer>>();

            foreach (var newAsset in Workspace.NewAssets) {
                AssetID assetId = newAsset.Key;
                AssetsReplacer replacer = newAsset.Value;
                string fileName = assetId.fileName;

                if (Workspace.LoadedFileLookup.TryGetValue(fileName.ToLower(), out AssetsFileInstance? file)) {
                    if (!fileToReplacer.ContainsKey(file))
                        fileToReplacer[file] = new List<AssetsReplacer>();

                    fileToReplacer[file].Add(replacer);
                }
            }

            if (Workspace.fromBundle) {
                ChangedAssetsDatas.Clear();
                foreach (var kvp in fileToReplacer) {
                    AssetsFileInstance file = kvp.Key;
                    List<AssetsReplacer> replacers = kvp.Value;

                    using (MemoryStream ms = new MemoryStream())
                    using (AssetsFileWriter w = new AssetsFileWriter(ms)) {
                        file.file.Write(w, 0, replacers, 0);
                        ChangedAssetsDatas.Add(new Tuple<AssetsFileInstance, byte[]>(file, ms.ToArray()));
                    }
                }
            } else {
                foreach (var kvp in fileToReplacer) {
                    AssetsFileInstance file = kvp.Key;
                    List<AssetsReplacer> replacers = kvp.Value;
                    /*
                    SaveFileDialog sfd = new SaveFileDialog();
                    sfd.Title = "Save as...";
                    sfd.InitialFileName = file.name;
                    */
                    string filePath = ag.assetFilePathOrigin;

                    while (true) {
                        // filePath = await sfd.ShowAsync(this);

                        if (filePath == "" || filePath == null)
                            return;

                        if (Path.GetFullPath(filePath) == Path.GetFullPath(file.path)) {
                            Console.WriteLine("Since this file is already open in UABEA, you must pick a new file name (sorry!)");
                            continue;
                        } else {
                            break;
                        }
                    }

                    try {
                        using (FileStream fs = File.OpenWrite(filePath))
                        using (AssetsFileWriter w = new AssetsFileWriter(fs)) {
                            file.file.Write(w, 0, replacers, 0);
                        }
                    } catch (Exception ex) {
                        Console.WriteLine("Write exception\nThere was a problem while writing the file:\n" + ex.Message);

                    }
                }
            }
        }





        private AssetsFileInstance BundleLoad(int index) {
            AssetsFileInstance fileInst = null;
            if (bundleInst != null) {
                // int index = (int)((ComboBoxItem)comboBox.SelectedItem).Tag;
                //int index = 0;
                string bunAssetName = bundleInst.file.bundleInf6.dirInf[index].name;

                //when we make a modification to an assets file in the bundle,
                //we replace the assets file in the manager. this way, all we
                //have to do is not reload from the bundle if our assets file
                //has been modified
                MemoryStream assetStream;
                if (!newFiles.ContainsKey(bunAssetName)) {
                    byte[] assetData = BundleHelper.LoadAssetDataFromBundle(bundleInst.file, index);
                    assetStream = new MemoryStream(assetData);
                } else {
                    //unused if the file already exists
                    assetStream = null;
                }

                //warning: does not update if you import an assets file onto
                //a file that wasn't originally an assets file
                var fileInf = BundleHelper.GetDirInfo(bundleInst.file, index);
                bool isAssetsFile = bundleInst.file.IsAssetsFile(bundleInst.file.reader, fileInf);

                if (isAssetsFile) {
                    string assetMemPath = Path.Combine(bundleInst.path, bunAssetName);
                    fileInst = am.LoadAssetsFile(assetStream, assetMemPath, true);

                    //if (!await LoadOrAskTypeData(fileInst))
                    //    return;

                    if (bundleInst != null && fileInst.parentBundle == null)
                        fileInst.parentBundle = bundleInst;

                    // InfoWindow info = new InfoWindow(am, new List<AssetsFileInstance> { fileInst }, true);
                    //info.Closing += InfoWindow_Closing;
                    // info.Show();
                } else {
                    // await MessageBoxUtil.ShowDialog(this,"Error", "This doesn't seem to be a valid assets file.\n" +"If you want to export a non-assets file,\n" + "use Export.");
                }
            }
            return fileInst;
        }



        private void Info() {
            if (bundleInst != null) {
                //int index = (int)((ComboBoxItem)comboBox.SelectedItem).Tag;
                int index = 0;

                string bunAssetName = bundleInst.file.bundleInf6.dirInf[index].name;

                //when we make a modification to an assets file in the bundle,
                //we replace the assets file in the manager. this way, all we
                //have to do is not reload from the bundle if our assets file
                //has been modified
                MemoryStream assetStream;
                if (!newFiles.ContainsKey(bunAssetName)) {
                    byte[] assetData = BundleHelper.LoadAssetDataFromBundle(bundleInst.file, index);
                    assetStream = new MemoryStream(assetData);
                } else {
                    //unused if the file already exists
                    assetStream = null;
                }

                //warning: does not update if you import an assets file onto
                //a file that wasn't originally an assets file
                var fileInf = BundleHelper.GetDirInfo(bundleInst.file, index);
                bool isAssetsFile = bundleInst.file.IsAssetsFile(bundleInst.file.reader, fileInf);

                if (isAssetsFile) {
                    string assetMemPath = Path.Combine(bundleInst.path, bunAssetName);
                    AssetsFileInstance fileInst = am.LoadAssetsFile(assetStream, assetMemPath, true);

                    // if (!await LoadOrAskTypeData(fileInst))
                    //     return;

                    if (bundleInst != null && fileInst.parentBundle == null)
                        fileInst.parentBundle = bundleInst;

                    //InfoWindow info = new InfoWindow(am, new List<AssetsFileInstance> { fileInst }, true);
                    //info.Closing += InfoWindow_Closing;
                    //info.Show();
                } else {
                    /*
                    await MessageBoxUtil.ShowDialog(this,
                        "Error", "This doesn't seem to be a valid assets file.\n" +
                                 "If you want to export a non-assets file,\n" +
                                 "use Export.");
                    */
                }
            }
        }

        private int SelectBundle() {
            Console.WriteLine("This is an assetbundle.");
            Console.WriteLine("");
            int max = bundleInst.file.NumFiles;
            for (int i = 0; i < max; i++) {

                Console.WriteLine("[" + i + "]   " + bundleInst.file.bundleInf6.dirInf[i].name);
            }
            Console.WriteLine("");

            Console.Write("Which file? [0-" + (max - 1) + "] >");

            string num = Console.ReadLine();

            int index = 0;
            if (int.TryParse(num, out index)) {
                selectedBundleName = bundleInst.file.bundleInf6.dirInf[index].name;
            } else {
                Console.WriteLine("invalid input. Try 0.");
                selectedBundleName = bundleInst.file.bundleInf6.dirInf[index].name;

            }
            Console.WriteLine(selectedBundleName);
            Console.WriteLine("");

            return index;
        }




        private void BundlePreSave() {
            //private void InfoWindow_Closing

            //InfoWindow window = (InfoWindow)sender;

            if (Workspace.fromBundle && ChangedAssetsDatas != null) {
                List<Tuple<AssetsFileInstance, byte[]>> assetDatas = ChangedAssetsDatas;

                //file that user initially selected
                AssetsFileInstance firstFile = Workspace.LoadedFiles[0];

                foreach (var tup in assetDatas) {
                    AssetsFileInstance fileInstance = tup.Item1;
                    byte[] assetData = tup.Item2;

                    string assetName = Path.GetFileName(fileInstance.path);

                    //only modify assets file we opened for now
                    if (fileInstance != firstFile)
                        continue;

                    BundleReplacer replacer = AssetImportExport.CreateBundleReplacer(assetName, true, assetData);
                    newFiles[assetName] = replacer;

                    //replace existing assets file in the manager
                    AssetsFileInstance? inst = am.files.FirstOrDefault(i => i.name.ToLower() == assetName.ToLower());
                    string assetsManagerName;

                    if (inst != null) {
                        assetsManagerName = inst.path;
                        am.files.Remove(inst);
                    } else //shouldn't happen
                      {
                        //we always load bundles from file, so this
                        //should always be somewhere on the disk
                        assetsManagerName = Path.Combine(bundleInst.path, assetName);
                    }

                    MemoryStream assetsStream = new MemoryStream(assetData);
                    am.LoadAssetsFile(assetsStream, assetsManagerName, true);
                }

                //changesUnsaved = true;
                //changesMade = true;
            }
        }









    }

    public class AssetInfoDataGridItem : INotifyPropertyChanged {
        public string Name { get; set; }
        public string Container { get; set; }
        public string Type { get; set; }
        public uint TypeID { get; set; }
        public int FileID { get; set; }
        public long PathID { get; set; }
        public int Size { get; set; }
        public string Modified { get; set; }

        public AssetContainer assetContainer;

        public event PropertyChangedEventHandler? PropertyChanged;

        //ultimate lazy
        public void Update(string propertyName = "") {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
