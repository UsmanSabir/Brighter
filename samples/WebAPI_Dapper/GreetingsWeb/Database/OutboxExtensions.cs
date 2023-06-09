﻿using System;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Paramore.Brighter;
using Paramore.Brighter.Extensions.DependencyInjection;
using Paramore.Brighter.Extensions.Hosting;
using Paramore.Brighter.MsSql;
using Paramore.Brighter.MySql;
using Paramore.Brighter.Outbox.MsSql;
using Paramore.Brighter.Outbox.MySql;
using Paramore.Brighter.Outbox.PostgreSql;
using Paramore.Brighter.Outbox.Sqlite;
using Paramore.Brighter.PostgreSql;
using Paramore.Brighter.Sqlite;

namespace GreetingsWeb.Database
{

    public class OutboxExtensions
    {
        public static (IAmAnOutbox, Type) MakeOutbox(
            IWebHostEnvironment env,
            DatabaseType databaseType,
            RelationalDatabaseConfiguration configuration)
        {
            (IAmAnOutbox, Type) outbox;
            if (env.IsDevelopment())
            {
                outbox = MakeSqliteOutBox(configuration);
            }
            else
            {
                outbox = databaseType switch
                {
                    DatabaseType.MySql => MakeMySqlOutbox(configuration),
                    DatabaseType.MsSql => MakeMsSqlOutbox(configuration),
                    DatabaseType.Postgres => MakePostgresSqlOutbox(configuration),
                    DatabaseType.Sqlite => MakeSqliteOutBox(configuration),
                    _ => throw new InvalidOperationException("Unknown Db type for Outbox configuration")
                };
            }

            return outbox;
        }

        private static (IAmAnOutbox, Type) MakePostgresSqlOutbox(RelationalDatabaseConfiguration configuration)
        {
            return (new PostgreSqlOutbox(configuration), typeof(NpgsqlUnitOfWork));
        }

        private static (IAmAnOutbox, Type) MakeMsSqlOutbox(RelationalDatabaseConfiguration configuration)
        {
            return new(new MsSqlOutbox(configuration), typeof(MsSqlUnitOfWork));
        }

        private static (IAmAnOutbox, Type)  MakeMySqlOutbox(RelationalDatabaseConfiguration configuration)
        {
            return (new MySqlOutbox(configuration), typeof(MySqlUnitOfWork));
        }

        private static (IAmAnOutbox, Type) MakeSqliteOutBox(RelationalDatabaseConfiguration configuration)
        {
            return (new SqliteOutbox(configuration), typeof(SqliteUnitOfWork));
        }
    }
}
