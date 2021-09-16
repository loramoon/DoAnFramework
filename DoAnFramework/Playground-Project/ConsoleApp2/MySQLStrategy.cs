﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Text.RegularExpressions;
using System.Globalization;
//using MySql.Data.MySqlClient;

namespace ConsoleApp2
{
    class MySQLStrategy
    {
        private const string DateTimeFormat = "yyyy-MM-dd HH:mm:ss";
        private const string TimeSpanFormat = "HH:mm:ss";
        private readonly Type DateTimeType = typeof(DateTime);
        private readonly Type TimeSpanType = typeof(TimeSpan);
        private readonly string sysDbConnectionString;
        private const int DefaultTimeLimit = 100;
        private readonly string restrictedUserId;
        private readonly string restrictedUserPassword;

        public MySQLStrategy(
            string sysDbConnectionString,
            string restrictedUserId,
            string restrictedUserPassword)
        {
            if (string.IsNullOrWhiteSpace(sysDbConnectionString))
            {
                throw new ArgumentException("Invalid sys DB connection string!", nameof(sysDbConnectionString));
            }

            if (string.IsNullOrWhiteSpace(restrictedUserId))
            {
                throw new ArgumentException("Invalid restricted user ID!", nameof(restrictedUserId));
            }

            if (string.IsNullOrWhiteSpace(restrictedUserPassword))
            {
                throw new ArgumentException("Invalid restricted user password!", nameof(restrictedUserPassword));
            }

            this.sysDbConnectionString = sysDbConnectionString;
            this.restrictedUserId = restrictedUserId;
            this.restrictedUserPassword = restrictedUserPassword;
        }
        protected virtual string FixCommandText(string commandText)
            => commandText;
        protected bool ExecuteNonQuery(IDbConnection connection, string commandText, int timeLimit = DefaultTimeLimit)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = this.FixCommandText(commandText);

                return CodeHelpers.ExecuteWithTimeLimit(
                    TimeSpan.FromMilliseconds(timeLimit),
                    () => command.ExecuteNonQuery());
            }
        }
        public  IDbConnection GetOpenConnection(string databaseName)
        {
            using (var connection = new MySqlConnection(this.sysDbConnectionString))
            {
                connection.Open();

                var createDatabaseQuery = $"CREATE DATABASE `{databaseName}`;";

                var createUserQuery = $@"
                    CREATE USER IF NOT EXISTS '{this.restrictedUserId}'@'%';
                    ALTER USER '{this.restrictedUserId}' IDENTIFIED BY '{this.restrictedUserPassword}'";
                /* SET PASSWORD FOR '{this.restrictedUserId}'@'%'=PASSWORD('{this.restrictedUserPassword}')"; */

                var grandPrivilegesToUserQuery = $@"
                    GRANT ALL PRIVILEGES ON `{databaseName}`.* TO '{this.restrictedUserId}'@'%';
                    FLUSH PRIVILEGES;";

                var enableLogBinTrustFunctionCreatorsQuery = "SET GLOBAL log_bin_trust_function_creators = 1;";

                this.ExecuteNonQuery(connection, createDatabaseQuery);
                this.ExecuteNonQuery(connection, createUserQuery);
                this.ExecuteNonQuery(connection, grandPrivilegesToUserQuery);
                this.ExecuteNonQuery(connection, enableLogBinTrustFunctionCreatorsQuery);
            }

            var workerConnection = new MySqlConnection(this.BuildWorkerDbConnectionString(databaseName));
            workerConnection.Open();

            return workerConnection;
        }

        public void DropDatabase(string databaseName)
        {
            using (var connection = new MySqlConnection(this.sysDbConnectionString))
            {
                connection.Open();

                this.ExecuteNonQuery(connection, $"DROP DATABASE IF EXISTS `{databaseName}`;");
            }
        }

        protected  string GetDataRecordFieldValue(IDataRecord dataRecord, int index)
        {
            
            if (!dataRecord.IsDBNull(index))
            {
                var fieldType = dataRecord.GetFieldType(index);

                if (fieldType == DateTimeType)
                {
                    return dataRecord.GetDateTime(index).ToString(DateTimeFormat, CultureInfo.InvariantCulture);
                }

                if (fieldType == TimeSpanType)
                {
                    return ((MySqlDataReader)dataRecord)
                        .GetTimeSpan(index)
                        .ToString(TimeSpanFormat, CultureInfo.InvariantCulture);
                }
            }

            return this.GetDataRecordFieldValue(dataRecord, index);
        }

        private string BuildWorkerDbConnectionString(string databaseName)
        {
            var userIdRegex = new Regex("UID=.*?;");
            var passwordRegex = new Regex("Password=.*?;");

            var workerDbConnectionString = this.sysDbConnectionString;

            workerDbConnectionString =
                userIdRegex.Replace(workerDbConnectionString, $"UID={this.restrictedUserId};");

            workerDbConnectionString =
                passwordRegex.Replace(workerDbConnectionString, $"Password={this.restrictedUserPassword}");

            workerDbConnectionString += $";Database={databaseName};Pooling=False;";

            return workerDbConnectionString;
        }
    }
}
