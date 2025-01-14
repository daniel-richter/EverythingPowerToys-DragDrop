﻿namespace Community.PowerToys.Run.Plugin.Everything
{
    using System.Collections.Generic;
    using System.ComponentModel;
    using System.Diagnostics;
    using System.IO;
    using System.Text;
    using Community.PowerToys.Run.Plugin.Everything.Properties;
    using Wox.Plugin;
    using static Interop.NativeMethods;

    internal class Everything
    {
        internal Everything(Settings setting)
        {
            Everything_SetRequestFlags(Request.FULL_PATH_AND_FILE_NAME);
            Everything_SetSort((Sort)setting.Sort);
            Everything_SetMax(setting.Max);
        }

        internal IEnumerable<Result> Query(string query, Settings setting)
        {
            string orgqry = query;
            if (orgqry.Contains('\"') && !setting.MatchPath)
            {
                Everything_SetMatchPath(true);
            }

            if (orgqry.Contains(':'))
            {
                string[] nqry = query.Split(':');
                if (setting.Filters.ContainsKey(nqry[0].ToLowerInvariant()))
                {
                    Everything_SetMax(0xffffffff);
                    query = nqry[1].Trim() + " ext:" + setting.Filters[nqry[0].Trim()];
                }
            }

            _ = Everything_SetSearchW(query);
            if (!Everything_QueryW(true))
            {
                throw new Win32Exception("Unable to Query");
            }

            if (orgqry.Contains('\"') && !setting.MatchPath)
            {
                Everything_SetMatchPath(false);
            }

            uint resultCount = Everything_GetNumResults();

            for (uint i = 0; i < resultCount; i++)
            {
                StringBuilder buffer = new StringBuilder(260);
                Everything_GetResultFullPathName(i, buffer, 260);
                string fullPath = buffer.ToString();
                string name = Path.GetFileName(fullPath);
                bool isFolder = Everything_IsFolderResult(i);
                string path = isFolder ? fullPath : Path.GetDirectoryName(fullPath);
                string ext = Path.GetExtension(fullPath.Replace(".lnk", string.Empty));

                var r = new Result()
                {
                    Title = name,
                    ToolTipData = new ToolTipData(name, fullPath),
                    SubTitle = Resources.plugin_name + ": " + fullPath,

                    IcoPath = isFolder ? "Images/folder.png" : (setting.Preview ?
                        fullPath : (SearchHelper.IconLoader.Icon(ext) ?? "Images/file.png")),
                    ContextData = new SearchResult()
                    {
                        Path = fullPath,
                        Title = name,
                        File = !isFolder,
                    },
                    Action = e =>
                    {
                        using var process = new Process();
                        process.StartInfo.FileName = fullPath;
                        process.StartInfo.WorkingDirectory = path;
                        process.StartInfo.UseShellExecute = true;

                        try
                        {
                            process.Start();
                            return true;
                        }
                        catch (Win32Exception)
                        {
                            return false;
                        }
                    },

                    QueryTextDisplay = setting.QueryText ? (isFolder ? path : name) : orgqry,
                };
                yield return r;
            }

            Everything_SetMax(setting.Max);
        }
    }
}
