﻿using Stankins.Interfaces;
using StankinsCommon;
using StankinsObjects;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace Stankins.Alive
{
    public class ReceiverWeb : AliveStatus, IReceive
    {
        public ReceiverWeb(CtorDictionary dict) : base(dict)
        {
            URL = GetMyDataOrThrow<string>(nameof(URL));
            Method = GetMyDataOrDefault<string>(nameof(Method), "GET");
            if (string.IsNullOrWhiteSpace(Method))
                Method = "GET";

            Name = nameof(ReceiverWeb);
        }
        public ReceiverWeb(string url) : this(url, null)
        {

        }
        public ReceiverWeb(string url,string method) : this(new CtorDictionary()
        {
            {nameof(url),url},
            {nameof(method),method }
        })
        {
        }

        public string URL { get; private set; }
        
        private readonly string Method;

        public override async Task<IDataToSent> TransformData(IDataToSent receiveData)
        {
            if (receiveData == null)
                receiveData = new DataToSentTable();
            DataTable results = CreateTable(receiveData);
            var sw = Stopwatch.StartNew();
            var ws = WebRequest.Create(URL) as HttpWebRequest;
            ws.Method = Method;
            var StartedDate = DateTime.UtcNow;
            try
            {
                using (var resp = await ws.GetResponseAsync())
                {
                    var r = resp as HttpWebResponse;
                    var sc = (int)r.StatusCode;
                    var text = "";
                    if ((sc >= 200) && (sc <= 299))
                    {
                        using (var rs = r.GetResponseStream())
                        {
                            using (var sr = new StreamReader(rs))
                            {
                                text = await sr.ReadToEndAsync();
                            }
                        }
                    }
                    results.Rows.Add("webrequest", Method, URL, true, sc,sw.ElapsedMilliseconds, text,null,StartedDate);



                }
            }
            catch(Exception ex)
            {
                results.Rows.Add("webrequest", Method, URL, false, null, sw.ElapsedMilliseconds, null, ex.Message,StartedDate);
            }
            
            
            return await Task.FromResult(receiveData) ;
        }
    }
}
