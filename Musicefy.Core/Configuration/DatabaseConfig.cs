using System;
using System.Configuration;
using System.Data;
using Dapper;

namespace Musicefy.Core.Configuration
{
    public static class DatabaseConfig
    {
        private const string DefaultConnectionString = "Data Source=musicefy.db";
        private const string ConnectionStringName = "MusicefyDb";

        public static string ConnectionString
        {
            get
            {
                var cs = ConfigurationManager.ConnectionStrings[ConnectionStringName];
                return cs?.ConnectionString ?? DefaultConnectionString;
            }
        }

        static DatabaseConfig()
        {
            SqlMapper.AddTypeHandler(new SqliteTimeSpanHandler());
            SqlMapper.AddTypeHandler(new SqliteDateTimeHandler());
        }

        private class SqliteTimeSpanHandler : SqlMapper.TypeHandler<TimeSpan>
        {
            public override void SetValue(IDbDataParameter parameter, TimeSpan value)
            {
                parameter.Value = value.Ticks.ToString();
            }

            public override TimeSpan Parse(object value)
            {
                if (value is string str)
                {
                    if (long.TryParse(str, out var ticks))
                        return TimeSpan.FromTicks(ticks);
                    if (TimeSpan.TryParse(str, out var ts))
                        return ts;
                }
                return TimeSpan.Zero;
            }
        }

        private class SqliteDateTimeHandler : SqlMapper.TypeHandler<DateTime>
        {
            public override void SetValue(IDbDataParameter parameter, DateTime value)
            {
                parameter.Value = value.ToString("o");
            }

            public override DateTime Parse(object value)
            {
                if (value is string str && DateTime.TryParse(str, out var dt))
                    return dt;
                return DateTime.MinValue;
            }
        }
    }
}
