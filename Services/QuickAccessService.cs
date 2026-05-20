using System.IO;
using QuickAccessTree.Models;

namespace QuickAccessTree.Services;

public class QuickAccessService
{
    // Shell CLSID for the Quick Access virtual folder
    private const string QuickAccessShellPath = "shell:::{679f85cb-0220-4080-b29b-5540cc05aab6}";

    public List<FolderNode> GetPinnedFolders()
    {
        var result = new List<FolderNode>();
        try
        {
            Type? shellType = Type.GetTypeFromProgID("Shell.Application");
            if (shellType == null) return result;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic folder = shell.NameSpace(QuickAccessShellPath);
            if (folder == null) return result;

            dynamic items = folder.Items();
            int count = (int)items.Count;

            for (int i = 0; i < count; i++)
            {
                dynamic item = items.Item(i);
                try
                {
                    string path = (string)item.Path;
                    string name = (string)item.Name;
                    if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                    {
                        result.Add(new FolderNode
                        {
                            Name = name,
                            Path = path,
                            IsCustom = false,
                        });
                    }
                }
                catch { }
            }
        }
        catch { }
        return result;
    }
}
