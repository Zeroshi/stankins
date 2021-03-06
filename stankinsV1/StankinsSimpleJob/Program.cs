﻿using Microsoft.Extensions.CommandLineUtils;
using Newtonsoft.Json;

using SenderToFile;
using StankinsInterfaces;
using StankinsSimpleFactory;
using StanskinsImplementation;
using StringInterpreter;
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Logging;
namespace StankinsSimpleJob
{
    public class Program
    {
        enum AddDataSimpleJob
        {
            ExitChoice= 0,
            AddReceiver=1,
            AddTransformer=2,
            AddSender=3
        }

        static void Main(string[] args)
        {
            AddCompilationReferencesForRuntime();
            if (args?.Length == 0)
            {
                args = new string[] { "-h" };
            }
            //TODO: load assemblies on demand
            var app = new CommandLineApplication(throwOnUnexpectedArg: true);
            app.Name = Assembly.GetEntryAssembly().GetName().Name;
            app.HelpOption("-?|-h|--help");

            app.Command("generate", (cmd) => {
                cmd.HelpOption("-?|-h|--help");
                cmd.OnExecute(() =>
                {
                    return GenerateJobDefinition();
                });
            });
            app.Command("execute",(cmd)=>{
                cmd.HelpOption("-?|-h|--help");
                var file = cmd.Argument("fileWithSimpleJob", "file serialized as simple job");
                cmd.OnExecute(() =>
                {
                    return ExecuteJob(file.Value);
                   
                });
            });
            app.Execute(args);
            
        }

        public static int ExecuteJob(string fileName)
        {
            //var settings = new JsonSerializerSettings()
            //{
            //    TypeNameHandling = TypeNameHandling.Objects,
            //    Formatting = Formatting.Indented,
            //    Error = HandleDeserializationError
            //    //ConstructorHandling= ConstructorHandling.AllowNonPublicDefaultConstructor

            //};
            var valSerialized = File.ReadAllText(fileName);
            #region running the file from where it is
            var dir = Path.GetDirectoryName(fileName);
            if(!string.IsNullOrWhiteSpace(dir))
                Directory.SetCurrentDirectory(dir);
            #endregion
            IJob job=null;
            try
            {
                var newJob = new SimpleJob();
                newJob.UnSerialize(valSerialized);
                for (int nr = 0; nr < newJob.Receivers?.Count; nr++)
                {
                    newJob.Receivers[nr] = new ReceiverDecorator(newJob.Receivers[nr]);
                }
                for (int nr = 0; nr < newJob.Senders?.Count; nr++)
                {
                    newJob.Senders[nr] = new SendDecorator(newJob.Senders[nr]);
                }
                job = newJob;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                job = null;
                //TODO:log
            }
            if (job == null)
            {
                try
                {
                    //TODO: add decorators
                    job = new SimpleJobConditionalTransformers();
                    job.UnSerialize(valSerialized);
                }
                catch (Exception ex)
                {
                    Console.WriteLine(ex.Message);
                    job = null;
                    //TODO:log
                }
            }
            if(job == null)
            {
                throw new ArgumentException($"cannot deserialize {fileName}");
            };
            //var deserialized = JsonConvert.DeserializeObject(valSerialized, settings) as ISimpleJob;
            Console.WriteLine($"execute {job.GetType().FullName}");
            
            
            try
            {
                job.Execute().Wait();
                Console.WriteLine("OK");
                return 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.Message);
                return -1;
            }
        }

        private static void AddCompilationReferencesForRuntime()
        {
            
            var j = new SimpleJob();
            var x = (Microsoft.Extensions.DependencyModel.Resolution.ICompilationAssemblyResolver)null;
            var RvCSV = new ReceiverCSV.ReceiverCSVFileInt("ASdsa",System.Text.Encoding.UTF8);
            var rvSql = new ReceiverDBSqlServer.ReceiverTableSQLServerInt((ReceiverDB.DBTableData<int, System.Data.SqlClient.SqlConnection>)null);
#if !NETSTANDARD1_6
#if !NETCOREAPP1_1
#if !NETCOREAPP1_0
#if !NETCOREAPP2_0
            var ole = new ReceiverOLEDB.ReceiverOleDBDatabaseInt(null);
            var receiver = new ReceiverSolution.ReceiverFromSolution(null);
#endif
#endif
#endif
#endif
            var rvSqlIte =new ReceiverDBSQLite.ReceiverTableSQLiteInt((ReceiverDB.DBTableData<int, Microsoft.Data.Sqlite.SqliteConnection>)null);
            var rvBinary = new ReceiverFile.ReceiverFileFromStorageBinary(null);
            var senderCSV = new Sender_CSV(null);


            var senderJson = new Sender_JSON(null);
        }

        private static T GetFullObject<T>(SimpleJobFactory factory)
        {
            var t = typeof(T);
            int i = 1;
            foreach (var name in factory.NamesOfObjects(t))
            {
                Console.WriteLine($"{i++}){name}");
            }
            Console.Write($"Please enter {t.Name} id");
            var nr = int.Parse(Console.ReadLine());

            var nameSelected = factory.NamesOfObjects(t)[nr - 1];
            
            var pars = factory.GetOwnProperties  (t,nameSelected);
            var vals = GetValues(factory,pars);
            var obj = factory.GetObject(t,nameSelected,vals);

            return (T)obj;
        }
        private static object[] GetValues(SimpleJobFactory factory, ParameterInfo[] pars)
        {
            var vals = new  object[pars.Length];
            int i = 0;
            foreach (var item in pars)
            {
                var paramType = item.ParameterType;
                //TODO: what if paramtype is array/ienumerable??
                if (paramType != typeof(string) && paramType.GetTypeInfo().IsClass)
                {
                    var itemClass= factory.ConstructedObject(paramType);
                    var t = itemClass.GetType();
                    var propsClass= factory.GetOwnProperties(t, t.Name);
                    foreach(var prClass in propsClass)
                    {
                        Console.WriteLine($"{prClass.Name} ({prClass.ParameterType.Name})");
                    }
                    vals[i++] = itemClass;
                    //TODO: add propertied for this object
                    //e.g DBTableData
                }
                else
                {
                    //todo: add optional and default value
                    Console.WriteLine($"please give value for ({item.ParameterType.Name}){item.Name} ");
                    vals[i++] = Convert.ChangeType(Console.ReadLine(), item.ParameterType);
                }
            }
            return vals;
        }
        private static int GenerateJobDefinition()
        {
            var factory = new SimpleJobFactory();
            
            var job = GetFullObject<ISimpleJob>(factory);
            var choice = AddDataSimpleJob.ExitChoice;
            do
            {
                Console.WriteLine($"{(int)AddDataSimpleJob.ExitChoice}. Exit");
                Console.WriteLine($"{(int)AddDataSimpleJob.AddReceiver}. Add Receiver");
                Console.WriteLine($"{(int)AddDataSimpleJob.AddTransformer}. Add transformer");
                Console.WriteLine($"{(int)AddDataSimpleJob.AddSender}. Add Sender");
                choice = (AddDataSimpleJob)int.Parse(Console.ReadLine());

                switch (choice)
                {
                    case AddDataSimpleJob.ExitChoice:
                        break;
                    case AddDataSimpleJob.AddReceiver:
                        var rec = GetFullObject<IReceive>(factory);
                        job.Receivers.Add(job.Receivers.Count, rec);
                        break;
                    case AddDataSimpleJob.AddTransformer:
                        var tr = GetFullObject<IFilterTransformer>(factory);
                        job.FiltersAndTransformers.Add(job.FiltersAndTransformers.Count, tr);
                        break;
                    case AddDataSimpleJob.AddSender:
                        var s = GetFullObject<ISend>(factory);
                        job.Senders.Add(job.Senders.Count, s);
                        break;
                    default:
                        Console.WriteLine("not yet implemented");
                        break;
                }
            } while (choice != AddDataSimpleJob.ExitChoice);





            var serialized = job.SerializeMe();
            File.WriteAllText("a.txt", serialized);
            Process.Start("notepad.exe", "a.txt");
            return 0;
        }

        //TODO: log
        private static void HandleDeserializationError(object sender, Newtonsoft.Json.Serialization.ErrorEventArgs e)
        {
            var currentError = e.ErrorContext.Error.Message;
            e.ErrorContext.Handled = true;
            //Console.WriteLine($"Deserialization error {currentError}");
            //@class.Log(LogLevel.Error, 0, $"deserialization error {currentError}", ex, null);
            
        }
    }
}