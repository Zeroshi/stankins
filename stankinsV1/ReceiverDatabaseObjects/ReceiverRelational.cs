﻿using StankinsInterfaces;
using StanskinsImplementation;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;

namespace ReceiverDatabaseObjects
{
    public abstract class ReceiverRelational:IReceive
    {
        public string ConnectionString{ get; set; }
        public string DatabaseName { get; set; }
        
        public string Name { get; set; }

        public IRowReceive[] valuesRead { get; set; }

        public ReceiverRelational()
        {
            Name = "get details from relational server";
        }
        protected abstract Task<KeyValuePair<string, object>[]> GetServerDetailsAsync();
        protected abstract Task<KeyValuePair<string,string>[]> GetDatabasesAsync();
        protected abstract Task<KeyValuePair<string, string>[]> GetTablesAsync(KeyValuePair<string,string> database);
        protected abstract Task<KeyValuePair<string, string>[]> GetViewsAsync(KeyValuePair<string, string> database);
        protected abstract Task<KeyValuePair<string, string>[]> GetColumnsAsync(KeyValuePair<string, string> table, KeyValuePair<string, string> database);
        public async Task LoadData()
        {
            var rr = new Dictionary<string, RowReadRelation>();
            var rrhServer= new RowReadRelation();
            foreach (var item in await GetServerDetailsAsync())
            {
                rrhServer.Values.Add(item.Key,item.Value);
            }
            rrhServer.Values.Add("PathID", "root");

            var dbsList = new List<IRowReceiveRelation>();
            rrhServer.Relations.Add("databases", dbsList);
            
            var dbs = await GetDatabasesAsync();
            bool allDatabases= string.IsNullOrWhiteSpace(DatabaseName);
            foreach(var db in dbs)
            {
                if (!allDatabases)
                {
                    bool ThisDatabase = string.Compare(db.Value, DatabaseName, StringComparison.OrdinalIgnoreCase) == 0;
                    if (!ThisDatabase)
                        continue;
                }
                var rrDatabase = new RowReadRelation();
                rrDatabase.Values.Add("ID", db.Key);
                rrDatabase.Values.Add("Name", db.Value);
                rrDatabase.Values.Add("PathID", db.Key);
                rr.Add(db.Key, rrDatabase);
                dbsList.Add(rrDatabase);
                var dbsTables= new List<IRowReceiveRelation>();
                rrDatabase.Relations.Add("tables", dbsTables);
                
                var tables=await GetTablesAsync(db);
                foreach(var table in tables)
                {
                    var rrTable = new RowReadRelation();
                    dbsTables.Add(rrTable);
                    rrTable.Values.Add("ID",table.Key);
                    rrTable.Values.Add("PathID", rrDatabase.Values["PathID"] + "_"+ table.Key);
                    rrTable.Values.Add("Name", table.Value);
                    
                    rr.Add(rrTable.Values["PathID"].ToString(), rrTable);
                    
                    var dbsColumns= new List<IRowReceiveRelation>();
                    rrTable.Relations.Add("columns", dbsColumns);
                    var cols = await GetColumnsAsync(table,db);
                    foreach (var col in cols)
                    {
                        var rrCol= new RowReadRelation();
                        rrCol.Values.Add("ID", col.Key);
                        rrCol.Values.Add("PathID", rrTable.Values["PathID"] + "_" + col.Key);
                        rrCol.Values.Add("Name", col.Value);
                        rr.Add(rrCol.Values["PathID"].ToString(), rrCol);
                        dbsColumns.Add(rrCol);
                    }
                    //TODO: add relationships
                }

                var dbsViews = new List<IRowReceiveRelation>();
                rrDatabase.Relations.Add("views", dbsViews);

                var views = await GetViewsAsync(db);

                foreach(var view in views)
                {
                    var rrView = new RowReadRelation();
                    dbsViews.Add(rrView);
                    rrView.Values.Add("ID", view.Key);
                    rrView.Values.Add("PathID", rrDatabase.Values["PathID"] + "_" + view.Key);
                    rrView.Values.Add("Name", view.Value);

                    rr.Add(rrView.Values["PathID"].ToString(), rrView);

                    var dbsColumns = new List<IRowReceiveRelation>();
                    rrView.Relations.Add("columns", dbsColumns);
                    var cols = await GetColumnsAsync(view, db);
                    foreach (var col in cols)
                    {
                        var rrCol = new RowReadRelation();
                        rrCol.Values.Add("ID", col.Key);
                        rrCol.Values.Add("PathID", rrView.Values["PathID"] + "_" + col.Key);
                        rrCol.Values.Add("Name", col.Value);
                        rr.Add(rrCol.Values["PathID"].ToString(), rrCol);
                        dbsColumns.Add(rrCol);
                    }
                }
                //TODO: add stored procs
                //TODO: add logins

                this.valuesRead = new IRowReceive[] { rrhServer };
            }
        }

        public void ClearValues()
        {
            valuesRead = null;
        }
    }
}
