using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml.Linq;
using RimWorld;
using Verse;

namespace JustTerminal;

// the files in ~ to a file outside the save and back, to take notes and programs to another game
public static class Transfer
{
    // in the game's save data, mod updates don't touch it
    public static string FolderPath => Path.Combine(GenFilePaths.SaveDataFolderPath, "JustTerminal", "Exports");

    public static List<string> Exports() =>
        Directory.Exists(FolderPath) ? Directory.GetFiles(FolderPath, "*.xml").OrderByDescending(File.GetLastWriteTime).ToList() : new List<string>();

    // a file named after the colony, the next export of the same colony replaces it
    public static string Export(FileNode root)
    {
        Directory.CreateDirectory(FolderPath);
        string colony = Faction.OfPlayerSilentFail?.Name ?? "colony";
        string name = new string(colony.Select(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-' ? c : '_').ToArray()).Trim();
        string path = Path.Combine(FolderPath, (name.Length == 0 ? "colony" : name) + ".xml");
        XElement top = new XElement("files");
        Write(root, top);
        new XDocument(top).Save(path);
        return path;
    }

    private static void Write(FileNode folder, XElement into)
    {
        foreach (FileNode c in folder.children.OrderBy(c => c.folder ? 0 : 1).ThenBy(c => c.name))
        {
            if (c.folder)
            {
                XElement f = new XElement("folder", new XAttribute("name", c.name));
                Write(c, f);
                into.Add(f);
            }
            else
            {
                into.Add(new XElement("file", new XAttribute("name", c.name), c.text ?? ""));
            }
        }
    }

    // how many files and folders an export has
    public static int Count(string path)
    {
        try
        {
            return XDocument.Load(path).Root?.Descendants().Count(e => e.Name == "file" || e.Name == "folder") ?? 0;
        }
        catch
        {
            return -1;
        }
    }

    // adds what's in the export to ~, a file with the same name is replaced, cron scripts come in off
    public static string Import(string path, FileNode root)
    {
        XElement top;
        try
        {
            top = XDocument.Load(path).Root;
        }
        catch (Exception e)
        {
            return "couldn't read " + Path.GetFileName(path) + ": " + e.Message;
        }
        if (top == null || top.Name != "files")
        {
            return Path.GetFileName(path) + " isn't a JustTerminal export";
        }
        int added = 0, replaced = 0, skipped = 0;
        Read(top, root, root, ref added, ref replaced, ref skipped);
        StringBuilder sb = new StringBuilder($"{added} added, {replaced} replaced");
        if (skipped > 0)
        {
            sb.Append($", {skipped} left out (bad names, too big, or no room)");
        }
        return sb.ToString();
    }

    private static void Read(XElement from, FileNode into, FileNode root, ref int added, ref int replaced, ref int skipped)
    {
        foreach (XElement e in from.Elements())
        {
            string name = (string)e.Attribute("name");
            bool folder = e.Name == "folder";
            if (name == null || Files.BadName(name) != null || !folder && e.Name != "file")
            {
                skipped++;
                continue;
            }
            FileNode there = into.Child(name);
            if (folder)
            {
                if (there != null && !there.folder)
                {
                    skipped += e.Descendants().Count() + 1;
                    continue;
                }
                if (there == null)
                {
                    if (root.Count >= Files.MaxNodes)
                    {
                        skipped += e.Descendants().Count() + 1;
                        continue;
                    }
                    there = new FileNode { name = name, folder = true, parent = into };
                    into.children.Add(there);
                    added++;
                }
                Read(e, there, root, ref added, ref replaced, ref skipped);
                continue;
            }
            string text = e.Value.Replace("\r", "");
            if (Encoding.UTF8.GetByteCount(text) > Files.MaxFileBytes || there != null && there.folder)
            {
                skipped++;
                continue;
            }
            if (there != null)
            {
                there.text = text;
                replaced++;
                continue;
            }
            if (root.Count >= Files.MaxNodes)
            {
                skipped++;
                continue;
            }
            into.children.Add(new FileNode { name = name, parent = into, text = text });
            added++;
        }
    }
}
