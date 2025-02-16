using Skylar.Models;
using SQLite.CodeFirst;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Skylar.Services.Data
{
    public class DBContext:DbContext
    {
        //ref: https://github.com/msallin/SQLiteCodeFirst

        public DbSet<User> users { get; set; }
        public DbSet<LocalCallRegister> callRegisters { get; set; }

        public DBContext() : base(new SQLiteConnection()
        {
            ConnectionString = new SQLiteConnectionStringBuilder()
            {
                DataSource = AppDomain.CurrentDomain.BaseDirectory + "SIPlocal.db",
                ForeignKeys = true,
            }.ConnectionString
        }, true)
        { }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            var sqliteConnectionInitializer = new SqliteCreateDatabaseIfNotExists<DBContext>(modelBuilder);
            Database.SetInitializer(sqliteConnectionInitializer);
        }
    }
}
