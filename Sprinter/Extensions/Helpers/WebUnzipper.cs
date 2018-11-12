using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using ICSharpCode.SharpZipLib.Core;
using ICSharpCode.SharpZipLib.GZip;
using ICSharpCode.SharpZipLib.Zip;
using NUnrar.Archive;
using Sprinter.Models;

namespace Sprinter.Extensions.Helpers
{
    public class WebUnzipper
    {
        public string TargetPath { get; private set; }
        public string ParseKey { get; private set; }
        public string DownloadURL { get; private set; }
        public string ResultFileName { get; private set; }
        public ParseringInfo Info
        {
            get { return ParseringInfo.Create(ParseKey.IsNullOrEmpty() ? "Undef" : ParseKey); }
        }
        public WebUnzipper(string targetPath, string parseKey, string downloadURL)
        {
            TargetPath = targetPath;
            ParseKey = parseKey;
            DownloadURL = downloadURL;
        }

        private bool IsLocalPath(string p)
        {
            return new Uri(p).IsFile;
        }


        public bool GetFile()
        {
            WebClient client = new WebClient();
            string ymlPath = "";
            
            var target = DownloadURL.IsNullOrEmpty() ? Info.Provider.DataURL : DownloadURL;
            bool isArchive = target.Contains(".zip") || target.Contains(".rar") || target.Contains(".gz");
            var zipPath = Path.Combine(TargetPath, "data" + Path.GetExtension(target));
            if (!IsLocalPath(target))
            {

                if (!target.Contains(".zip") && !target.Contains(".gz")&& !target.Contains(".rar"))
                {
                    var ext = Path.GetExtension(target);
                    var rx = new Regex(@"\.[a-zA-z]{1,3}");
                    if (!rx.IsMatch(ext ?? ""))
                        ext = ".zip";
                    zipPath = Path.Combine(TargetPath, "data" + ext);
                }
                Info.AddMessage("Запущено скачивание YML файла. Это может занять несколько минут....");
                try
                {
                    client.DownloadFile(target, zipPath);
                }
                catch (Exception e)
                {
                    Info.AddMessage(e.Message, true);
                    Info.AddMessage(e.StackTrace, true);
                    Info.EndDate = DateTime.Now;
                    return false;
                }
                Info.AddMessage("Файл скачан.");

            }
            else
            {
                zipPath = target;
            }
            //криво
            if (!IsLocalPath(target) && !target.Contains(".zip") && !target.Contains(".gz"))
            {
                target = "data.zip";
            }
            if (isArchive)
            {
                if (target.EndsWith(".gz"))
                {
                    try
                    {
                        Info.AddMessage("Распаковка архива....");
                        byte[] dataBuffer = new byte[4096];
                        using (Stream fs = new FileStream(zipPath, FileMode.Open, FileAccess.Read))
                        {
                            using (GZipInputStream gzipStream = new GZipInputStream(fs))
                            {
                                ymlPath = Path.Combine(TargetPath, "catalog.yml");
                                ResultFileName = ymlPath;
                                using (FileStream fsOut = System.IO.File.Create(ymlPath))
                                {
                                    StreamUtils.Copy(gzipStream, fsOut, dataBuffer);
                                }
                            }
                        }
                        Info.AddMessage("Архив распакован....");
                    }
                    catch (Exception e)
                    {
                        Info.AddMessage(e.Message, true);
                        Info.AddMessage(e.StackTrace, true);
                        Info.EndDate = DateTime.Now;
                        return false;
                    }
                    finally
                    {
                        File.Delete(zipPath);
                    }
                }
                else if (target.EndsWith(".zip"))
                {
                    ZipFile zf = null;
                    try
                    {
                        Info.AddMessage("Распаковка архива....");
                        FileStream fs = File.OpenRead(zipPath);
                        zf = new ZipFile(fs);
                        /*if (!String.IsNullOrEmpty(password)){ zf.Password = password;	}*/
                        foreach (ZipEntry zipEntry in zf)
                        {
                            if (!zipEntry.IsFile)
                            {
                                continue;
                            }
                            String entryFileName = zipEntry.Name;
                            byte[] buffer = new byte[4096];
                            Stream zipStream = zf.GetInputStream(zipEntry);
                            String fullZipToPath = Path.Combine(TargetPath, entryFileName);
                            if (!fullZipToPath.Contains(".doc"))
                                ResultFileName = fullZipToPath;
                            string directoryName = Path.GetDirectoryName(fullZipToPath);
                            if (directoryName.Length > 0)
                                Directory.CreateDirectory(directoryName);

                            using (FileStream streamWriter = File.Create(fullZipToPath))
                            {
                                StreamUtils.Copy(zipStream, streamWriter, buffer);
                            }
                        }
                        Info.AddMessage("Архив распакован....");
                    }
                    catch (Exception e)
                    {
                        Info.AddMessage(e.Message, true);
                        Info.AddMessage(e.StackTrace, true);
                        Info.EndDate = DateTime.Now;
                        return false;
                    }
                    finally
                    {
                        if (zf != null)
                        {
                            zf.IsStreamOwner = true;
                            zf.Close();
                        }
                        try
                        {
                            File.Delete(zipPath);
                        }
                        catch (Exception)
                        {
                        }
                    }
                }
                else if (target.EndsWith(".rar"))
                {
                    try
                    {
                        Info.AddMessage("Распаковка архива....");

                        RarArchive archive = RarArchive.Open(zipPath);
                        foreach (RarArchiveEntry entry in archive.Entries)
                        {
                            string path = Path.Combine(TargetPath, Path.GetFileName(entry.FilePath));
                            entry.WriteToFile(path);
                            ResultFileName = path;
                        }
                        Info.AddMessage("Архив распакован....");

                    }
                    catch (Exception e)
                    {
                        Info.AddMessage(e.Message, true);
                        Info.AddMessage(e.StackTrace, true);
                        Info.EndDate = DateTime.Now;
                        return false;

                    }
                    finally
                    {
                        try
                        {
                            File.Delete(zipPath);
                        }
                        catch (Exception)
                        {
                        }

                    }

                }
                else
                {
                    ResultFileName = zipPath;
                }
            }
            else
            {
                ResultFileName = zipPath;
            }
            return true;
        }

    }
}