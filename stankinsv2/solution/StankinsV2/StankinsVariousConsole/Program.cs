﻿using SenderInterpretedRazor;
using Stankins.Alive;
using Stankins.Amazon;
using Stankins.AzureDevOps;
using Stankins.FileOps;
using Stankins.HTML;
using Stankins.Interfaces;
using Stankins.Office;
using Stankins.Process;
using Stankins.WLW;
using Stankins.XML;
using StankinsCommon;
using StankinsObjects;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.RepresentationModel;
using Stankins.Razor;
using Stankins.Excel;

namespace StankinsVariousConsole
{
    public class StankinsGenerator
    {
        public Backend[] backend { get; set; }
        public Frontend[] frontend { get; set; }
    }

    public class Backend
    {
        public string folder { get; set; }
        public string name { get; set; }
        public string[] worksWithFrontEnd { get; set; }
        public string[] copyTableFiles { get; set; }
    }

    public class Frontend
    {
        public string folder { get; set; }
        public string[] worksWithBackEnd { get; set; }
        public string name { get; set; }
        public string[] copyTableFiles { get; set; }
    }

    class Program
    {
        //taken from https://stackoverflow.com/questions/11981282/convert-json-to-datatable/11982180#11982180
        public static DataTable Tabulate(string json)
        {
            var jsonLinq = JObject.Parse(json);

            // Find the first array using Linq
            var srcArray = jsonLinq.Descendants().Where(d => d is JArray).First();
            var trgArray = new JArray();
            foreach (JObject row in srcArray.Children<JObject>())
            {
                var cleanRow = new JObject();
                foreach (JProperty column in row.Properties())
                {
                    // Only include JValue types
                    if (column.Value is JValue)
                    {
                        cleanRow.Add(column.Name, column.Value);
                    }
                }

                trgArray.Add(cleanRow);
            }

            return JsonConvert.DeserializeObject<DataTable>(trgArray.ToString());
        }
        static DataTable FromJSon(string json)
        {
            var token = JToken.Parse(json);
            if (!(token is JArray))
            {
                json = "[" + json + "]";
                token = JToken.Parse(json);

            }
            JArray tok = token as JArray;

            return token.ToObject<DataTable>();



        }


        static async Task<bool> GenerateApp()
        {
            string folderGenerator = @"E:\ignatandrei\stankins\stankinsv2\solution\GenerateAll\";
            string generator =folderGenerator+ "describe.txt";
            var stData = JsonConvert.DeserializeObject<StankinsGenerator>(File.ReadAllText( generator));


            var backendFolderName = @"NETCore3.1";
            var frontendFolderName = @"Angular10.0";
            var backend = stData.backend.First(it => it.folder == backendFolderName);
            var frontEnd= stData.frontend.First(it => it.folder == frontendFolderName);
            //wt  new-tab -d C:\test\backend\NETCore3.1\TestWebAPI ; split-pane -d C:\test\frontend\Angular10.0
            var outputFolder = @"C:\test";
            IDataToSent data;
            string excel = @"E:\ignatandrei\stankins\stankinsv2\solution\GenerateAll\ExcelTests\";
            //excel += "TestExportExcel.xlsx";
            excel += "ProgrammingTools.xlsx";
            var recExcel = new ReceiverExcel(excel);

            data = await recExcel.TransformData(null);

            var renameExcel = new TransformerRenameTable("it=>it.Contains(\".xls\")", "DataSource");

            data = await renameExcel.TransformData(data);

            var renameCol = new ChangeColumnName("SheetName", "TableName");
            data = await renameCol.TransformData(data);

            IDataToSent Model = data;
            var ds = Model.FindAfterName("DataSource").Value;
            var nrRowsDS = ds.Rows.Count;
            var nameTablesToRender = new string[nrRowsDS];
            for (int iRowDS = 0; iRowDS < nrRowsDS; iRowDS++)
            {
                var nameTable = ds.Rows[iRowDS]["TableName"].ToString();
                var dt = Model.FindAfterName(nameTable).Value;
                Console.WriteLine(dt.TableName);
                nameTablesToRender[iRowDS] = dt.TableName;
            }

            var g = Guid.NewGuid().ToString("N");
            var f = Path.Combine(outputFolder, g);
            Directory.CreateDirectory(f);
            File.Copy(generator, Path.Combine(outputFolder, "describe.txt"),true);
            var backendFolder = Path.Combine(f, "backend", backendFolderName);
            var frontendFolder = Path.Combine(f, "frontend", frontendFolderName);
            DirectoryCopy(Path.Combine(folderGenerator,"backend", backendFolderName), backendFolder,true);
            DirectoryCopy(Path.Combine(folderGenerator, "frontend",frontendFolderName),frontendFolder , true);
            
            foreach(var fileToCopy in backend.copyTableFiles)
            {
                var pathFile = Path.Combine(backendFolder, fileToCopy);
                string content = await File.ReadAllTextAsync(pathFile);
                foreach (DataRow item in ds.Rows)
                {
                    var nameTable = item["TableName"].ToString();
                    var data1 = Model.FindAfterName(nameTable).Value;

                    var newFileName = pathFile.Replace("@Name@", nameTable,StringComparison.InvariantCultureIgnoreCase);
                    var newContent = content.Replace("@Name@", nameTable, StringComparison.InvariantCultureIgnoreCase);
                    await File.WriteAllTextAsync(newFileName, newContent);

                }
                File.Delete(pathFile);
            }
            foreach (var fileToCopy in frontEnd.copyTableFiles)
            {
                var pathFile = Path.Combine(frontendFolder, fileToCopy);
                string content = await File.ReadAllTextAsync(pathFile);
                foreach (DataRow item in ds.Rows)
                {
                    var nameTable = item["TableName"].ToString();
                    var data1 = Model.FindAfterName(nameTable).Value;

                    var newFileName = pathFile.Replace("@Name@", nameTable, StringComparison.InvariantCultureIgnoreCase);
                    var newContent = content.Replace("@Name@", nameTable, StringComparison.InvariantCultureIgnoreCase);
                    await File.WriteAllTextAsync(newFileName, newContent);

                }
                File.Delete(pathFile);
            }
            Console.WriteLine("in excel:" + data.DataToBeSentFurther.Count);
            var nrTablesExcel = data.DataToBeSentFurther.Count;
            
            
            var tableData = data.Metadata.Tables.First();
            var rec = new ReceiverFilesInFolder(f, "*.*", SearchOption.AllDirectories);
            data = await rec.TransformData(data);
            Console.WriteLine("after files:" + data.DataToBeSentFurther.Count);
            var razorTables = nameTablesToRender.Union(new[] { "DataSource" }).ToArray();
            var t = new TransformerOneTableToMulti<SenderToRazorFromFile>("InputTemplate", "FullFileName", razorTables, new CtorDictionary());
            data = await t.TransformData(data);
            Console.WriteLine("after razor:" + data.DataToBeSentFurther.Count);

            var one = new TransformerToOneTable("it=>it.StartsWith(\"OutputString\")");
            data = await one.TransformData(data);
            //Transformer
            //var outFile = new SenderOutputToFolder(@"C:\test\", false);
            Console.WriteLine("after one table string:" + data.DataToBeSentFurther.Count);

            one = new TransformerToOneTable("it=>it.StartsWith(\"OutputByte\")");
            data = await one.TransformData(data);
            Console.WriteLine("after one table byte:" + data.DataToBeSentFurther.Count);

            var ren = new TransformerRenameTable("it=>it.StartsWith(\"OutputString\")", "OutputString");
            data = await ren.TransformData(data);
            //SenderOutputToFolder
            //TransformerUpdateColumn
            //var up = new TransformerUpdateColumn("FullFileName_origin", data.DataToBeSentFurther[2].TableName, $"'{outputFolder}' + SUBSTRING(FullFileName_origin,{lenTemplateFolder+1},100)");
            Console.WriteLine(data.DataToBeSentFurther[3].TableName);
            var lenTemplateFolder = f.Length;

            var up = new TransformerUpdateColumn("FullFileName_origin", "OutputString", $"SUBSTRING(FullFileName_origin,{lenTemplateFolder + 2},100)");
            data = await up.TransformData(data);
            var x = data.DataToBeSentFurther;
            var name = new ChangeColumnName("Name", "Key");
            data = await name.TransformData(data);
            name = new ChangeColumnName("FullFileName_origin", "Name");
            data = await name.TransformData(data);

            var remByte = new FilterRemoveTable("OutputByte");
            data = await remByte.TransformData(data);

            var save = new SenderOutputToFolder(outputFolder, false, "OutputString");
            data = await save.TransformData(data);
            return true;
        }

        //https://docs.microsoft.com/en-us/dotnet/standard/io/how-to-copy-directories
        private static void DirectoryCopy(string sourceDirName, string destDirName, bool copySubDirs)
        {
            // Get the subdirectories for the specified directory.
            DirectoryInfo dir = new DirectoryInfo(sourceDirName);

            if (!dir.Exists)
            {
                throw new DirectoryNotFoundException(
                    "Source directory does not exist or could not be found: "
                    + sourceDirName);
            }

            DirectoryInfo[] dirs = dir.GetDirectories();
            // If the destination directory doesn't exist, create it.
            if (!Directory.Exists(destDirName))
            {
                Directory.CreateDirectory(destDirName);
            }

            // Get the files in the directory and copy them to the new location.
            FileInfo[] files = dir.GetFiles();
            foreach (FileInfo file in files)
            {
                string temppath = Path.Combine(destDirName, file.Name);
                file.CopyTo(temppath, false);
            }

            // If copying subdirectories, copy them and their contents to new location.
            if (copySubDirs)
            {
                foreach (DirectoryInfo subdir in dirs)
                {
                    string temppath = Path.Combine(destDirName, subdir.Name);
                    DirectoryCopy(subdir.FullName, temppath, copySubDirs);
                }
            }
        }
        static async Task Main(string[] args)
        {
            while (true)
            {
                try
                {

                    await GenerateApp();
                    Console.WriteLine("done");
                }
                catch (Exception ex)
                {
                    Console.WriteLine("!!!! error" + ex.Message);
                    Console.WriteLine(ex.StackTrace);
                }
                Console.ReadLine();
            }
            return;
            string json = @"[{""Name"":""AAA"",""Age"":""22"",""Job"":""PPP""},"
                                + @"{""Name"":""BBB"",""Age"":""25"",""Job"":""QQQ""},"
                                + @"{""Name"":""CCC"",""Age"":""38"",""Job"":""RRR""}]";
            var dt = FromJSon(json);

            //var table = JsonConvert.DeserializeObject<DataTable>(json);
            json = @"{""Name"":""AAA"",""Age"":""22"",""Job"":""PPP""}";

            dt = FromJSon(json);

            return;

            await Yaml();
            return;
            //await OneTab();
            //return;
            //await WebSites();
            //return;
            //await PingSites();
            //return;
            //await jordanbpeterson();
            //return;
            //await Propriu();
            //return;
            //await BillGates();

            //return;
            //await Bookmarks();

            //return;
            //await ResultsDir();
            //return;
            //await BookerPrize();
            //return;
            //await Nobel();
            //return;
            //var item = new DBReceiveTableNamesSqlServer("Server =.;Database=MyTestDatabase;Trusted_Connection=True;");
            //var data = await item.TransformData(null);

            //Console.WriteLine("1");

            //IReceive receive = new ReceiverCSV("OneTab.txt", Encoding.UTF8, false, '|', '\n');
            //var dataNoble = await receive.TransformData(null);

            //ITransformer separate = new SeparateByNumber(data.Metadata.Tables[0].Name, 10);
            //data = await separate.TransformData(data);
            //Console.WriteLine("2");
        }

        private static async Task Yaml()
        {
            var data = await File.ReadAllTextAsync("stankinsYaml.txt");
            var visit = new YamlDevOpsVisitor();
            visit.LoadFromString(data);
            Console.Write(Newtonsoft.Json.JsonConvert.SerializeObject(visit.jobs, Newtonsoft.Json.Formatting.Indented));
            //Console.WriteLine(rt);
            //foreach(var item in mp.Children)
            //{
            //    var q = item;
            //    InterpretNodeRoot(q);
            //}
        }
        //static void InterpretNodeRoot(KeyValuePair<YamlNode, YamlNode> rootNode)
        //{
        //    switch (rootNode.Key.NodeType)
        //    {
        //        case YamlNodeType.Scalar:
        //            var                  
        //    }
        //}
        private static async Task OneTab()
        {
            var v = new Verifier();
            //var dr = new ReceiverLinkOneTab("https://www.one-tab.com/page/4BuJyIbyQ7akwk0DrTLwUg");
            var dr = new ReceiverLinkOneTab("https://www.one-tab.com/page/2lpYRWu3R4CRTjAFCch5aA");
            var data = await dr.TransformData(null);
            await v.TransformData(data);

            data = await new FilterRemoveColumn("href").TransformData(data);
            data = await new FilterRemoveColumn("a_text").TransformData(data);
            await v.TransformData(data);
            var firstTableName = data.Metadata.Tables[0].Name;

            data = await new TransformerOneColumnToMultiTablesByNumber(firstTableName, 15).TransformData(data);
            await v.TransformData(data);

            data = await new FilterRemoveTable(firstTableName).TransformData(data);
            await v.TransformData(data);

            string file = Path.Combine(Directory.GetCurrentDirectory(), "onetab.xlsx");

            var excel = new SenderExcel(file);
            data = await excel.TransformData(data);
            await v.TransformData(data);

            data = await new TransformerRenameTablesInOrder(304, "Friday Links ###").TransformData(data);
            await v.TransformData(data);

            data = await new SenderWindowsLiveWriter(null, "</li><li>", "", "<li>", "").TransformData(data);
            await v.TransformData(data);


        }
        private static async Task WebSites()
        {
            var v = new Verifier();
            var dt = new ReceiverWeb("http://www.yahoo.com");
            var data = await dt.TransformData(null);
            await v.TransformData(data);
            string file = Path.Combine(Directory.GetCurrentDirectory(), "ping.xlsx");
            var excel = new SenderExcel(file);
            data = await excel.TransformData(data);


        }

        private static async Task PingSites()
        {
            var v = new Verifier();
            var dt = new ReceiverPing("www.yahoo.com");
            var data = await dt.TransformData(null);
            await v.TransformData(data);
            string file = Path.Combine(Directory.GetCurrentDirectory(), "ping.xlsx");
            var excel = new SenderExcel(file);
            data = await excel.TransformData(data);


        }
        private static async Task jordanbpeterson()
        {
            var v = new Verifier();
            var dt = new ReceiverHtmlList("https://jordanbpeterson.com/great-books/");
            var data = await dt.TransformData(null);
            await v.TransformData(data);
            data = await new TransformerToOneTable().TransformData(data);
            await v.TransformData(data);

            data = await new FilterRetainColumnDataContains("li_html", "http://amzn.to").TransformData(data);
            await v.TransformData(data);



            data = await new TransformSplitColumn(data.Metadata.Tables[0].Name, "li", ':').TransformData(data);
            string file = Path.Combine(Directory.GetCurrentDirectory(), "jordanbpeterson.xlsx");
            var excel = new SenderExcel(file);
            data = await excel.TransformData(data);
            data = await v.TransformData(data);



            Process.Start(@"C:\Program Files (x86)\Microsoft Office\root\Office16\excel.exe", file);




        }
        private static async Task Propriu()
        {
            var v = new Verifier();
            var dt = new ReceiverXML(@"C:\Users\Surface1\Downloads\blogpropriu.wordpress.2018-10-06.xml", Encoding.UTF8, @"//item/category[@nicename=""carti-5-stele""]/..");
            var data = await dt.TransformData(null);
            await v.TransformData(data);
            data = await new TransformerXMLToColumn("OuterXML", "//title", "title", ",").TransformData(data);
            await v.TransformData(data);
            data = await new TransformerXMLToColumn("OuterXML", "//category", "category", ",").TransformData(data);
            await v.TransformData(data);

            data = await new TransformerXMLToColumn("OuterXML", @"//*[name()=""content:encoded""]", "content", ",").TransformData(data);
            await v.TransformData(data);

            data = await new TransformerOneTableToMulti<TransformerHtmlAHref>("Content", "content", new CtorDictionary()).TransformData(data);

            await v.TransformData(data);
            data = await new FilterTablesWithColumn("href").TransformData(data);

            await v.TransformData(data);
            data = await new TransformerToOneTable().TransformData(data);

            await v.TransformData(data);
            data = await new FilterRetainColumnDataContains("href", "amazon").TransformData(data);
            //await v.TransformData(data);
            //data = await new RetainColumnDataContains("a_text", "Lord of Light").TransformData(data);
            await v.TransformData(data);
            data = await new TransformerOneTableToMulti<AmazonMeta>("file", "href", new CtorDictionary()).TransformData(data);
            await v.TransformData(data);
            data = await new FilterTablesWithColumn("meta_content").TransformData(data);
            await v.TransformData(data);
            data = await new TransformerToOneTable().TransformData(data);
            await v.TransformData(data);
            var excel = new SenderExcel(@"andrei.xslx");
            data = await excel.TransformData(data);
            data = await v.TransformData(data);


        }

        private static async Task BillGates()
        {
            var v = new Verifier();

            //var dt = new ReceiverHtmlAHref(@"https://www.gatesnotes.com/Books#All",Encoding.UTF8);
            //var dt = new ReceiverHtmlRegex(@"C:\Users\Surface1\Documents\bg.txt", Encoding.UTF8, @".(?:href=)(?<book>.+?)(?:#disqus).*?");
            var dt = new ReceiverHtmlRegex(@"C:\Users\Surface1\Documents\bg.txt", Encoding.UTF8, @".(?:href=\\"")(?<book>.+?)(?:#disqus).*?");
            var data = await dt.TransformData(null);
            await v.TransformData(data);
            var books = new FilterRetainColumnDataContains(data.Metadata.Columns[0].Name, "ooks");
            data = await books.TransformData(data);
            await v.TransformData(data);
            var t = new TransformerOneTableToMulti<ReceiverHtmlMeta>("file", data.Metadata.Columns[0].Name, new CtorDictionary());
            data = await t.TransformData(data);
            await v.TransformData(data);
            data = await new FilterTablesWithColumn("meta_name").TransformData(data);
            await v.TransformData(data);
            data = await new FilterTablesWithColumn("meta_name").TransformData(data);
            await v.TransformData(data);
            data = await new TransformerToOneTable().TransformData(data);
            await v.TransformData(data);
            books = new FilterRetainColumnDataContains("meta_name", "keywords");
            data = await books.TransformData(data);
            await v.TransformData(data);
            var excel = new SenderExcel(@"bg.xslx");
            data = await excel.TransformData(data);
            data = await v.TransformData(data);
        }

        static async Task Bookmarks()
        {
            var v = new Verifier();

            var dt = new ReceiverHtmlSelector(@"C:\Users\Surface1\Desktop\bookmarks_11_17_17.html", Encoding.UTF8, "//dt/a");
            var data = await dt.TransformData(null);
            data = await v.TransformData(data);
            //data = await new TransformerHTMLAttribute("item_html", "href").TransformData(data);
            //data = await v.TransformData(data);
            data = await new TransformerAddColumnExpressionByColumn("item_html", "'<li>' + item_html +'</li>'", "li").TransformData(data);
            data = await v.TransformData(data);
            data = await new FilterRemoveColumn("item_html").TransformData(data);
            data = await v.TransformData(data);
            data = await new FilterRemoveColumn("item").TransformData(data);
            data = await v.TransformData(data);
            data = await new TransformerOneColumnToMultiTablesByNumber(data.Metadata.Tables.First().Name, 20).TransformData(data);
            data = await v.TransformData(data);

            var excel = new SenderExcel(@"text.xslx");
            data = await excel.TransformData(data);
            data = await v.TransformData(data);

        }

        static async Task ResultsDir()
        {
            var v = new Verifier();

            var r = new ReceiverProcess("print.exe", null);
            var data = await r.TransformData(null);
            await v.TransformData(data);
            return;
        }
        static async Task Nobel()
        {
            //var writeData = new SenderToConsole();
            var v = new Verifier();
            var nobelLiterature = new ReceiverHtmlTables("https://en.wikipedia.org/wiki/List_of_Nobel_laureates_in_Literature", Encoding.UTF8);
            var data = await nobelLiterature.TransformData(null);
            data = await v.TransformData(data);


            var f = new FilterTablesWithColumn("Laureate");
            data = await f.TransformData(data);
            data = await v.TransformData(data);


            var justSome = new FilterColumnData("Laureate", "Laureate not like '*ohn*'");
            data = await justSome.TransformData(data);
            data = await v.TransformData(data);

            var transform = new TransformerHTMLAttribute("Laureate_html", "href", "LaureateWiki");
            data = await transform.TransformData(data);
            data = await v.TransformData(data);

            var transformPicture = new TransformerHTMLAttribute("Picture_html", "src", "PictureUrl");
            data = await transformPicture.TransformData(data);
            data = await v.TransformData(data);

            var addSite = new TransformerAddColumnExpressionByTable(data.Metadata.Tables.First().Name, "'https://en.wikipedia.org'+ LaureateWiki ", "LaureateFullWiki");
            data = await addSite.TransformData(data);
            data = await v.TransformData(data);

            var gatherLaureatesWiki = new TransformerOneTableToMulti<BaseObjectInSerial<ReceiverHtmlList, TransformerToOneTable>>(
                "file", "LaureateFullWiki", new StankinsCommon.CtorDictionary()
                {
                    {nameof(Encoding),Encoding.UTF8 }
                }
                );
            data = await gatherLaureatesWiki.TransformData(data);
            data = await v.TransformData(data);
            //var h = new ReceiverHtmlList("https://en.wikipedia.org/wiki/Sully_Prudhomme",Encoding.UTF8);
            //var data2 = await h.TransformData(null);
            var yearFilter = new FilterColumnDataWithRegex("li_html", @"(\([0-9]{4})|(, [0-9]{4})");
            data = await yearFilter.TransformData(data);
            data = await v.TransformData(data);
            var italicFilter = new FilterColumnDataWithRegex("li_html", @"[<]i[>]");
            data = await italicFilter.TransformData(data);
            data = await v.TransformData(data);
            data = await (new TransformTrim()).TransformData(data);
            data = await v.TransformData(data);
            data = await (new FilterRemoveColumnDataGreaterThanLength("li_html", 400)).TransformData(data);
            //data = await (new TransformerAddColumnExpressionByColumn("li_html", "Len(li_html)", "liLen")).TransformData(data);
            //var csv = new SenderFileCSV(@"D:\test");
            //data = await csv.TransformData(data);
            data = await new FilterRemoveColumn("li_html").TransformData(data);
            data = await new FilterRemoveColumn("Picture").TransformData(data);
            data = await new FilterRemoveColumn("Year_html").TransformData(data);
            data = await new FilterRemoveColumn("Genre(s)_html").TransformData(data);
            data = await new FilterRemoveColumn("LaureateWiki").TransformData(data);
            data = await new FilterRemoveColumn("Country").TransformData(data);
            data = await new FilterRemoveColumn("Picture_html").TransformData(data);
            data = await new FilterRemoveColumn("Laureate_html").TransformData(data);
            data = await new FilterRemoveColumn("Country_html").TransformData(data);
            data = await new FilterRemoveColumn("Language(s)_html").TransformData(data);
            data = await new FilterRemoveColumn("Citation_html").TransformData(data);

            data = await v.TransformData(data);
            //var regexLast = @"(?:.+\/)((?<name>.+))";
            data = await new AddColumnRegex("LaureateFullWiki_origin", @"(?:.+\/)((?<nameAuthor>.+))").TransformData(data);
            data = await v.TransformData(data);

            data = await new AddColumnRegex("LaureateFullWiki", @"(?:.+\/)((?<name>.+))").TransformData(data);

            data = await new FilterRemoveColumn("LaureateFullWiki_origin").TransformData(data);

            data = await new ChangeTableNamesRegex(@"(?:.+\/)((?<name>.+))").TransformData(data);
            data = await v.TransformData(data);

            data = await new ChangeColumnName("li", "bookName").TransformData(data);
            data = await v.TransformData(data);

            data = await new SenderExcel(@"D:\test\nobel.xlsx").TransformData(data);


            data = await new FilterTablesWithColumn("bookName").TransformData(data);
            data = await v.TransformData(data);

            data = await new TransformerToOneTable().TransformData(data);
            data = await v.TransformData(data);

            var content = System.IO.File.ReadAllText("sqliteCreation.txt");
            data = await new SenderRazorTableOneByOne(content, @"D:\test\").TransformData(data);
            data = await v.TransformData(data);
            //data = await writeData.TransformData(data);




        }
        static async Task BookerPrize()
        {
            //var writeData = new SenderToConsole();
            var v = new Verifier();
            var booker = new ReceiverHtmlTables("https://en.wikipedia.org/wiki/Booker_Prize", Encoding.UTF8);
            var data = await booker.TransformData(null);

            data = await v.TransformData(data);

            data = await new FilterTablesWithColumn("Author").TransformData(data);
            data = await v.TransformData(data);

            data = await new FilterColumnData("Author", "Author not like '*ohn*'").TransformData(data);
            data = await v.TransformData(data);
            data = await new TransformerHTMLAttribute("Author_html", "href", "AuthorWiki").TransformData(data);
            data = await v.TransformData(data);
            data = await new TransformerAddColumnExpressionByTable(data.Metadata.Tables.First().Name, "'https://en.wikipedia.org'+ AuthorWiki ", "AuthorFullWiki").TransformData(data);
            data = await v.TransformData(data);
            var gatherLaureatesWiki = new TransformerOneTableToMulti<BaseObjectInSerial<ReceiverHtmlList, TransformerToOneTable>>(
                 "file", "AuthorFullWiki", new StankinsCommon.CtorDictionary()
                {
                    {nameof(Encoding),Encoding.UTF8 }
                }
                );
            data = await gatherLaureatesWiki.TransformData(data);
            data = await v.TransformData(data);
            data = await new FilterColumnDataWithRegex("li", @"(\([0-9]{4})|(, [0-9]{4})").TransformData(data);
            data = await v.TransformData(data);
            //data = await new ChangeTableNamesRegex(@"(?:.+\/)((?<name>.+))").TransformData(data);
            data = await v.TransformData(data);

            data = await new ChangeColumnName("li", "bookName").TransformData(data);
            data = await v.TransformData(data);
            data = await new FilterRemoveColumn("li_html").TransformData(data);
            data = await new FilterRemoveColumn("Year_html").TransformData(data);
            data = await new FilterRemoveColumn("Author_html").TransformData(data);
            data = await new FilterRemoveColumn("Title_html").TransformData(data);
            data = await new FilterRemoveColumn("Genre(s)_html").TransformData(data);
            data = await new FilterRemoveColumn("Country_html").TransformData(data);
            data = await v.TransformData(data);
            data = await (new TransformTrim()).TransformData(data);
            data = await v.TransformData(data);

            data = await new SenderExcel(@"D:\test\booker.xlsx").TransformData(data);
            data = await v.TransformData(data);
            var content = System.IO.File.ReadAllText("sqliteCreation.txt");
            data = await new SenderRazorTableOneByOne(content, @"D:\test\").TransformData(data);

        }

    }
}
