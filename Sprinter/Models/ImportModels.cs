using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Web;
using Sprinter.Extensions;
using Sprinter.Models.ViewModels;

namespace Sprinter.Models
{
    public delegate void PostProcessingDelegate(int saleID, ImportData args, DownloadInfo dl);
    public delegate void  PrepareRecordDelegate(ref ImportData import);

    public delegate List<ImportData> ParseToListDeletegate(string xlsPath, bool isSpec = false);
    public class ThreadCatalogParserInfo
    {
        public List<ImportData> DataList { get; set; }
        public string PartnerName { get; set; }
        public HttpContext Context { get; set; }
        public bool AllPartsDuringDay { get; set; }
        public PrepareRecordDelegate PrepareRecordFunc { get; set; }
        public PostProcessingDelegate PostProcessingFunc { get; set; }
        public DownloadInfo DlModel { get; set; }
        public bool SkipDelete { get; set; }

    }

    public class ThreadExportInfo
    {
        public CatalogExportFilterModel ExportFilter { get; set; }
        public HttpContext Context { get; set; }
        public string ExporterName { get; set; }
        public OnOrderFormedDelegate ProgressFunc { get; set; }
    }


    public class DescriptionParserData
    {
        public string FieldName { get; set; }
        public Regex Expr { get; set; }
        public Regex AlterExpr { get; set; }
        public int CaptureNum { get; set; }
        public int GroupNum { get; set; }
        public Regex AddExpr { get; set; }
    }


    public class DescriptionParser
    {
        public string URL { get; set; }
        public List<DescriptionParserData> FieldList { get; set; }

        public void TryLoadDescription(ref ImportData importData)
        {
            try
            {
                WebClient wc = new WebClient();
                string page = wc.DownloadString(URL).Replace("\r", "").Replace("\n", "").Replace("\t", "");

                foreach (var data in FieldList)
                {
                    var value = "";
                    var matches = data.Expr.Matches(page);
                    if (matches.Count > 0 && matches[0].Groups.Count >= data.GroupNum + 1 && matches[0].Groups[data.GroupNum].Captures.Count >= data.CaptureNum + 1)
                    {
                        value = matches[0].Groups[data.GroupNum].Captures[data.CaptureNum].Value;
                    }
                    else if (data.AlterExpr != null)
                    {
                        matches = data.AlterExpr.Matches(page);
                        if (matches.Count > 0 && matches[0].Groups.Count >= data.GroupNum + 1 && matches[0].Groups[data.GroupNum].Captures.Count >= data.CaptureNum + 1)
                        {
                            value = matches[0].Groups[data.GroupNum].Captures[data.CaptureNum].Value;
                        }
                        else if (data.AddExpr != null)
                        {
                            matches = data.AddExpr.Matches(page);
                            if (matches.Count > 0 && matches[0].Groups.Count >= data.GroupNum + 1 && matches[0].Groups[data.GroupNum].Captures.Count >= data.CaptureNum + 1)
                            {
                                value = matches[0].Groups[data.GroupNum].Captures[data.CaptureNum].Value;
                            }

                        }
                    }
                    if (value.IsFilled())
                        importData.SetPropertyValue(data.FieldName, value);
                }

            }
            catch (Exception)
            {
            }
        }
    }

    public class DownloadInfo
    {

        [DisplayName("URL каталога")]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
        public string URL { get; set; }

        [DisplayName("Путь к картинкам на сервере")]
        public string AdditionalPath { get; set; }


        public string Message { get; set; }

        public SectionListDownloadInfo SectionListDownloadInfo { get; set; }
    }

    public class SectionListProviderDetails
    {
        public string Key { get; set; }
        public string Link { get; set; }
        public ParseToListDeletegate Func { get; set; }
    }

    public class SectionListDownloadInfo
    {
        public SectionListDownloadInfo()
        {
            
        }
        public SectionListDownloadInfo(List<SectionListProviderDetails> ProviderImportData, string partnerName)
        {
            var db = new DB();
            PartnerName = partnerName;
            var partnerID = db.Partners.First(x => x.Name == partnerName).ID;

            ClearOld = false;
            if (ProviderImportData.Any(x => x.Key == partnerName))
            {
                HaveLink = ProviderImportData.First(x => x.Key == partnerName).Link.IsFilled();
                URL = ProviderImportData.First(x => x.Key == partnerName).Link;
            }
            else
            {
                HaveLink = false;
                URL = "";
            }
            ImportSettingList = new PagedData<PartnerImportSetting>(
    db.PartnerImportSettings.Where(x => x.PartnerID == partnerID).OrderBy(x => x.ImportSectionName),
    HttpContext.Current.Request.QueryString["page"].ToInt(), 30, "Master");

        }

        public bool HaveLink { get; set; }
        [DisplayName("URL каталога")]
        [Required(AllowEmptyStrings = false, ErrorMessage = "Поле '{0}' обязательно для заполнения")]
        public string URL { get; set; }

        [DisplayName("Удалить устаревшие категории из списка")]
        public bool ClearOld { get; set; }

        public string PartnerName { get; set; }
        public PagedData<PartnerImportSetting> ImportSettingList { get; set; }
        
    }

    public class CatalogStruct
    {
        public int ProviderID { get; set; }
        public int ProviderParentID { get; set; }
        public string Name { get; set; }
        public string Url { get; set; }
        public string UniqueUrl { get; set; }
        public string RedirectUrl { get; set; }
        public CatalogStruct Parent { get; set; }
        public List<CatalogStruct> Children { get; set; }

        public void SaveTree()
        {
            if (string.IsNullOrEmpty(UniqueUrl))
                UniqueUrl = Url;
            if (Parent != null)
            {
                var db = new DB();
                var exist =
                    db.CMSPages.FirstOrDefault(x => x.ImportID == ProviderID);

                if (exist == null)
                {
                    bool isRepeat = db.CMSPages.Any(x => x.URL == UniqueUrl);
                    if (isRepeat)
                    {
                        for (int i = 0; i < 50; i++)
                        {
                            UniqueUrl = string.Format("{0}_{1}", Url, i);
                            isRepeat = db.CMSPages.Any(x => x.URL == UniqueUrl);
                            if (!isRepeat) break;
                        }
                    }
                    int parentPage = Parent.Parent == null
                                         ? db.CMSPages.FirstOrDefault(
                                             x => x.URL == "catalog" && x.PageType.TypeName == "Catalog").ID
                                         : db.CMSPages.FirstOrDefault(x => x.URL == Parent.UniqueUrl).ID;
                    var newPage = new CMSPage()
                                      {
                                          Description = Name,
                                          Keywords = Name,
                                          PageName = Name,
                                          Title = Name,
                                          FullName = Name,
                                          URL = UniqueUrl,
                                          ParentID = parentPage,
                                          Visible = true,
                                          ViewMenu = true,
                                          Type = 1,
                                          ImportID = ProviderID,
                                          OriginalURL = RedirectUrl
                                      };

                    db.CMSPages.InsertOnSubmit(newPage);
                    db.SubmitChanges();


                }
            }
            foreach (var child in Children)
            {
                child.SaveTree();
            }
        }
    }

    public class ExtendedSectionInfo
    {
        public ExtendedSectionInfo Parent { get; set; }
        public string Name { get; set; }
        public string UID { get; set; }
        public override string ToString()
        {
            string retVal = "";
            if(Parent!=null)
            {
                retVal += Parent.Name.Trim();
                retVal += " -->> ";
            }
            retVal += Name;
            return retVal;
        }
    }

    public class ImportData
    {
        public string PartnerUID { get; set; }
        public bool? IsNew { get; set; }
        public bool? IsSpec { get; set; }
        public bool? IsTop { get; set; }
        public decimal PartnerPrice { get; set; }

        public string Header { get; set; }
        public List<string> Authors { get; set; }
        public string PublisherName { get; set; }
        public int? Year { get; set; }
        public int? PageCount { get; set; }
        public string Section { get; set; }
        public string ISBN { get; set; }
        public string Type { get; set; }
        public long EAN { get; set; }
        public string Description { get; set; }
        public string CoverURL { get; set; }
        public bool? OutOfPrint { get; set; }

        public ExtendedSectionInfo FullSectionInfo { get; set; }

        public static int? ParseYear(string sYear)
        {
            if (sYear.IsNullOrEmpty()) return null;

            DateTime parsedDate;
            if (DateTime.TryParseExact(sYear, "dd.MM.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out  parsedDate))
            {
                return parsedDate.Year;
            }
            else
            {
                if (DateTime.TryParseExact(sYear, "MM.dd.yyyy", CultureInfo.InvariantCulture, DateTimeStyles.None, out  parsedDate))
                {
                    return parsedDate.Year;
                }
            }

            int iYear = 0;
            int.TryParse(sYear, out iYear);
            if (iYear == 0) return null;
            if (iYear <= DateTime.Now.Year - 2000)
                return iYear + 2000;
            if (iYear < 100 && iYear > DateTime.Now.Year - 2000)
                return iYear + 1900;
            return iYear;
        }

        public static List<string> CreateAuthorsList(string authors)
        {
            string after =
                authors.Replace("и др.", "").Replace("и др", "").Replace(
                    "сост.", "").Replace("Сост.", "").Replace("Отв.", "").Replace("отв.", "").Replace(
                        "Составитель", "").Replace("составитель", "")
                    .Replace("Под ", "").Replace("под ", "").Replace("гл.", "").Replace("Гл.", "").Replace("ред.",
                                                                                                           "").Replace(
                                                                                                               "Ред.",
                                                                                                               "").
                    Replace(
                        "Иллюстрации", "").Replace(
                            "П/р", "").Replace("- от", "").Replace("Редактор", "").Replace("редактор", "").Replace(
                                "редакцией", "").Replace("пер.", "").Replace("Пер.", "").Replace("Пер.", "").Replace(
                                    "редакцией", "").Replace("с ан", "").Replace("с франц.", "").Replace("под/ред", "");

            Regex rx = new Regex("[A-Za-z]+");
            if (rx.Matches(after).Count > 0)
            {
                return
                    after.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries).Select(
                        x => x.ClearBorders())
                        .Where(x => !x.IsNullOrEmpty()).
                        ToList();

            }
            return
                after.Split(new string[] { ",", " и " }, StringSplitOptions.RemoveEmptyEntries).Select(
                    x => x.ClearBorders())
                    .Where(x => !x.IsNullOrEmpty()).
                    ToList();
        }

        public static decimal ParsePrice(string value)
        {
            decimal p = 0;
            try
            {
                var cleared =
                    value.Replace("рублей", "").Replace("руб.", "").Replace("руб", "").Replace("р.", "").Replace("р", "")
                        .Replace(".00", "").Replace(",00", "")
                        .Replace("$", "").Replace(".", ",").Trim();

                var integer = int.Parse(cleared.Contains(",") ? cleared.Substring(0, cleared.IndexOf(",")) : cleared);
                var floatable = int.Parse(cleared.Contains(",") ? cleared.Substring(cleared.IndexOf(",") + 1) : "0");
                var power = floatable == 0 ? 0 : floatable.ToString().Length;


                p = (decimal)integer + (decimal)floatable/(decimal)Math.Pow(10, power);
                //p = decimal.Parse(cleared, NumberStyles.Number, CultureInfo.CurrentCulture);
            }
            catch (Exception)
            {
            }
            return p;

        }

        public static long? ParseInt(string value)
        {
            if (value.IsNullOrEmpty()) return null;
            long lVal = 0;
            if (long.TryParse(value, out lVal))
                return lVal;
            return null;
        }
    }
}