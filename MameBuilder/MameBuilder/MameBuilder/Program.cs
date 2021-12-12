using CommandLine;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Xml;
using System.Xml.Serialization;

namespace MameBuilder
{
    class Builder
    {
        private readonly Dictionary<String, String> _biosRom = new Dictionary<string, string>();

        public enum RomModeEnum { NonMerged, Merged, Split };

        [Option("noclones", Default = false, HelpText = "Ignore clones")]
        public bool NoClones { get; set; }

        [Option("nochd", Default = false, HelpText = "Don't use CHDs")]
        public bool NoChd { get; set; }

        [Option("badrom", Default = false, HelpText = "Include bad roms")]
        public bool BadRom { get; set; }

        [Value(0, MetaName = "datfile", HelpText = "DAT/XML file")]
        public string DatFile { get; set; }

        [Option('l', "librarypath", Default = "library", HelpText = "MAME Library Path")]
        public string LibraryPath { get; set; }

        [Option('v', "verbose", Default = false, HelpText = "Verbose output")]
        public bool Verbose { get; set; }

        [Option('t', "targetpath", Default = "", HelpText = "Target Directory")]
        public string TargetPath { get; set; }

        [Option('r', "rommode", Default = RomModeEnum.NonMerged, HelpText = "ROM mode (Merged,Split,NonMerged)")]
        public RomModeEnum RomMode { get; set; }

        [Option("nosplitbios", Default = false, HelpText = "Don't Split BIOS")]
        public bool NoSplitBios { get; set; }

        [Option("nozrtwork", Default = false, HelpText = "Copy artwork")]
        public bool NoArtwork { get; set; }

        public void Run()
        {
            Mame dat;

            if (!Directory.Exists(LibraryPath))
            {
                Console.WriteLine("Library path not found.");
                return;
            }

            try
            {
                dat = DeserializeDatFile(DatFile);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to read file " + DatFile);
                Console.WriteLine(e.ToString());
                return;
            }

            if (string.IsNullOrEmpty(TargetPath))
            {
                TargetPath = dat.Build;
                if (NoChd)
                    TargetPath += " NoCHD";
                if (NoClones)
                    TargetPath += " NoClones";
                switch (RomMode)
                {
                    case RomModeEnum.NonMerged: TargetPath += " NonMerged"; break;
                    case RomModeEnum.Merged: TargetPath += " Merged"; break;
                    case RomModeEnum.Split: TargetPath += " Split"; break;
                }
            }

            try
            {
                Directory.CreateDirectory(TargetPath);
                if (Directory.Exists(TargetPath + "/roms"))
                    Directory.Delete(TargetPath + "/roms", true);
                Directory.CreateDirectory(TargetPath + "/roms");
                if (Directory.Exists(TargetPath + "/samples"))
                    Directory.Delete(TargetPath + "/samples", true);
                Directory.CreateDirectory(TargetPath + "/samples");
                if (Directory.Exists(TargetPath + "/artwork"))
                    Directory.Delete(TargetPath + "/artwork", true);
                Directory.CreateDirectory(TargetPath + "/artwork");
                File.Copy(DatFile, TargetPath + "/" + Path.GetFileName(DatFile), true);
            }
            catch (Exception e)
            {
                Console.WriteLine("Unable to create target directory " + TargetPath);
                Console.WriteLine(e.ToString());
                return;
            }

            foreach (var game in dat.Game)
            {
                if (game.Isbios == GameIsbios.Yes && game.Rom != null)
                {
                    foreach (var rom in game.Rom)
                    {
                        if (rom.Sha1 != null)
                            _biosRom[rom.Sha1] = game.Name;
                        if (rom.Crc != null)
                            _biosRom[rom.Crc] = game.Name;
                    }
                }
            }

            var missing = new Dictionary<string, int>();
            var missingSamples = new Dictionary<string, int>();
            int working = 0;
            int total = 0;

            foreach (var game in dat.Game)
            {
                if (NoClones && !string.IsNullOrEmpty(game.Cloneof))
                {
                    if (Verbose) Console.WriteLine(game.Name + " Skipped (Clone)");
                    continue;
                }

                if (NoChd && game.Disk?.Count > 0)
                {
                    if (Verbose) Console.WriteLine(game.Name + " Skipped (Uses CHD)");
                    continue;
                }

                if (IsNotWorking(game))
                {
                    if (Verbose) Console.WriteLine(game.Name + " Skipped (Not Working)");
                    continue;
                }

                if (MissingRoms(game))
                {
                    if (Verbose) Console.WriteLine(game.Name + " Skipped (Missing ROMs)");
                    continue;
                }

                Console.WriteLine(game.Name);
                total++;

                int count;
                string zipname;

                if (RomMode != RomModeEnum.Merged || string.IsNullOrEmpty(game.Romof))
                    zipname = TargetPath + "/roms/" + game.Name + ".zip";
                else
                    zipname = TargetPath + "/roms/" + game.Romof + ".zip";

                // We now do it again, having checked that this is a rom we're interested in
                using (var zip = ZipFile.Open(zipname, ZipArchiveMode.Update))
                {
                    count = zip.Entries.Count;
                    foreach (var rom in game.Rom)
                    {
                        if (rom.Status == RomStatus.Nodump)
                            continue;

                        if (RomMode == RomModeEnum.Split && !string.IsNullOrEmpty(rom.Merge))
                            continue;

                        // This is complicated by inconsistencies, such as "shootgal" having copies of the "cvs" romset
                        // but not marked as such
                        if (!NoSplitBios && game.Isbios != GameIsbios.Yes && !string.IsNullOrEmpty(game.Romof) && IsBiosRom(rom.Sha1, rom.Crc))
                        {
                            if (Verbose) Console.WriteLine(rom.Name + " is BIOS");
                            continue;
                        }

                        if (Verbose) Console.WriteLine(rom.Name);

                        if (zip.GetEntry(rom.Name) == null)
                        {
                            string romPath;

                            if (!string.IsNullOrEmpty(rom.Sha1))
                                romPath = LibraryPath + "/" + rom.Sha1.Substring(0, 2) + "/" + rom.Sha1 + ".rom";
                            else if (!string.IsNullOrEmpty(rom.Crc))
                                romPath = LibraryPath + "/crc/" + rom.Crc.Substring(0, 2) + "/" + rom.Crc + ".rom";
                            else
                                continue; // no crc/sha1
                            if (File.Exists(romPath))
                            {
                                var entry = zip.CreateEntry(rom.Name);
                                using (var stream = entry.Open())
                                using (var file = new FileStream(romPath, FileMode.Open))
                                    file.CopyTo(stream);
                                count++;
                            }
                            else
                            {
                                missing[game.Name] = 1;
                                Console.WriteLine(rom.Name + " missing ROM");
                            }

                        }
                    }
                }

                if (count == 0)
                {
                    missing[game.Name] = 1;
                    File.Delete(zipname);
                }

                if (game.Disk?.Count > 0)
                {
                    string dir;
                    if (RomMode != RomModeEnum.Merged || string.IsNullOrEmpty(game.Romof))
                        dir = TargetPath + "/roms/" + game.Name;
                    else
                        dir = TargetPath + "/roms/" + game.Romof;
                    Directory.CreateDirectory(dir);
                    foreach (var chd in game.Disk)
                    {
                        if (chd.Status == DiskStatus.Nodump)
                            continue;

                        if (Verbose) Console.WriteLine(chd.Name);

                        var chdPath = LibraryPath + "/chd/" + chd.Sha1.Substring(0, 2) + "/" + chd.Sha1 + ".dat";
                        var targetPath = dir + "/" + chd.Name + ".chd";
                        if (File.Exists(chdPath))
                        {
                            if (!File.Exists(targetPath))
                                File.Copy(chdPath, targetPath);
                        }
                        else
                            Console.WriteLine(chd.Name + " missing CHD");
                    }
                }

                if (game.Sample?.Count > 0)
                {
                    if (!string.IsNullOrEmpty(game.Sampleof))
                        zipname = TargetPath + "/samples/" + game.Sampleof + ".zip";
                    else
                        zipname = TargetPath + "/samples/" + game.Name + ".zip";
                    using (var zip = ZipFile.Open(zipname, ZipArchiveMode.Update))
                    {
                        count = zip.Entries.Count;
                        foreach (var sample in game.Sample)
                        {
                            if (zip.GetEntry(sample.Name) == null)
                            {
                                string src;

                                if (!string.IsNullOrEmpty(game.Sampleof))
                                {
                                    src = LibraryPath + "/samples/" + game.Sampleof + "/" + sample.Name;
                                    if (!File.Exists(src))
                                        src = LibraryPath + "/samples/" + game.Name + "/" + sample.Name;
                                    game.Sampleof = game.Name;
                                }
                                else
                                    src = LibraryPath + "/samples/" + game.Name + "/" + sample.Name;
                                if (!File.Exists(src) && !string.IsNullOrEmpty(game.Romof))
                                    src = LibraryPath + "/samples/" + game.Romof + "/" + sample.Name;

                                if (File.Exists(src))
                                {
                                    if (Verbose) Console.WriteLine(sample.Name);

                                    var entry = zip.CreateEntry(sample.Name);
                                    using (var stream = entry.Open())
                                    using (var file = new FileStream(src, FileMode.Open))
                                        file.CopyTo(stream);
                                    count++;
                                }
                                else
                                {
                                    missingSamples[game.Sampleof] = 1;
                                    Console.WriteLine(sample.Name + " missing sample");
                                }
                            }
                        }
                    }
                    if (count == 0)
                    {
                        missingSamples[game.Name] = 1;
                        File.Delete(zipname);
                    }

                    if(!NoArtwork)
                    {
                        string src;

                        zipname = TargetPath + "/artwork/" + game.Name + ".zip";
                        src = LibraryPath + "/artwork/" + game.Name + ".zip";

                        if(!File.Exists(zipname) && File.Exists(src))
                            File.Copy(src, zipname);
                        }
                    }

                if (!missingSamples.ContainsKey(game.Sampleof ?? "null") && !missing.ContainsKey(game.Name))
                    working++;
            }

            Console.WriteLine(total.ToString() + " games considered out of " + dat.Game.Count.ToString() + ", " + missing.Count.ToString() + " missing, " + missingSamples.Count.ToString() + " missing samples");

            Console.WriteLine("");
            Console.WriteLine("Missing games");
            foreach (var game in missing.Keys) 
                Console.WriteLine(game);

            Console.WriteLine("");
            Console.WriteLine("Missing samples");
            foreach (var game in missingSamples.Keys)
                Console.WriteLine(game);

        }

        private Mame DeserializeDatFile(string filename)
        {
            var serializer = new XmlSerializer(typeof(Mame));
            var fs = new FileStream(filename, FileMode.Open);
            var settings = new XmlReaderSettings() { DtdProcessing = DtdProcessing.Ignore };
            var reader = XmlReader.Create(fs, settings);
            var dat = (Mame)serializer.Deserialize(reader);
            fs.Close();
            return dat;
        }

        private bool IsNotWorking(Game game)
        {
            bool bad = game.Rom == null;
            if (!BadRom && !bad)
            {
                foreach (var rom in game.Rom)
                {
                    if (rom.Status == RomStatus.Baddump)
                        bad = true;
                }
                if (bad)
                    return true;
            }

            if (!BadRom && game.Disk?.Count > 0)
            {
                foreach (var disk in game.Disk)
                {
                    if (disk.Status == DiskStatus.Baddump)
                        bad = true;
                }
            }
            return bad;
        }

        private bool MissingRoms(Game game)
        {
            bool bad = false;
            foreach (var rom in game.Rom)
            {
                if (rom.Status == RomStatus.Nodump || rom.Status == RomStatus.Baddump)
                    continue;
                string romPath;

                if (!string.IsNullOrEmpty(rom.Sha1))
                    romPath = LibraryPath + "/" + rom.Sha1.Substring(0, 2) + "/" + rom.Sha1 + ".rom";
                else if (!string.IsNullOrEmpty(rom.Crc))
                    romPath = LibraryPath + "/crc/" + rom.Crc.Substring(0, 2) + "/" + rom.Crc + ".rom";
                else
                {
                    if (Verbose) Console.WriteLine(rom.Name + " ROM has no SHA1 or CRC.");
                    bad = true;
                    romPath = "";
                }

                if (!File.Exists(romPath))
                {
                    if (Verbose) Console.WriteLine(rom.Name + " ROM missing.");
                    bad = true;
                }

                if (!bad && (new FileInfo(romPath).Length != Convert.ToInt32(rom.Size)))
                {
                    if (Verbose) Console.WriteLine(rom.Size + " Bad Size");
                }
            }

            if (bad)
                return true;

            if (game.Disk?.Count > 0)
            {
                foreach (var disk in game.Disk)
                {
                    if (disk.Status == DiskStatus.Nodump)
                        continue;
                    if (disk.Sha1 == null)
                    {
                        Console.WriteLine(disk.Name + "missing SHA1 in DAT file");
                        bad = true;
                        continue;
                    }
                    var romPath = LibraryPath + "/chd/" + disk.Sha1.Substring(0, 2) + "/" + disk.Sha1 + ".dat";
                    if (!File.Exists(romPath))
                    {
                        Console.WriteLine(disk.Name + " CHD missing.");
                        bad = true;
                    }
                }
            }
            return bad;
        }

        bool IsBiosRom(string sha1, string crc)
        {
            return _biosRom.ContainsKey(string.IsNullOrEmpty(sha1) ? "null" : sha1) || _biosRom.ContainsKey(string.IsNullOrEmpty(crc) ? "null" : crc);
        }
    }

    class Program
    {
        static void Main(string[] args)
        {
            Parser.Default.ParseArguments<Builder>(args)
                .WithParsed(RunOptionsAndReturnExitCode);
        }

        private static void RunOptionsAndReturnExitCode(Builder opts)
        {
            opts.Run();
        }
    }
}
