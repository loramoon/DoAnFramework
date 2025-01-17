﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Data;
using System.Globalization;


namespace DoAnFramework
{
    public abstract class BaseSqlExecutionStrategy: BaseExecutionStrategy
    {
        protected static readonly Type DecimalType = typeof(decimal);
        protected static readonly Type DoubleType = typeof(double);
        protected static readonly Type FloatType = typeof(float);
        protected static readonly Type ByteArrayType = typeof(byte[]);
        protected static readonly Type DateTimeType = typeof(DateTime);
        protected static readonly Type TimeSpanType = typeof(TimeSpan);

        private const int DefaultTimeLimit = 2 * 60 * 1000;

        public abstract IDbConnection GetOpenConnection(string databaseName);

        public abstract void DropDatabase(string databaseName);

        public virtual string GetDatabaseName() => Guid.NewGuid().ToString();

        protected virtual IExecutionResult<TestResult> Execute(
    IExecutionContext<TestsInputModel> executionContext,
    IExecutionResult<TestResult> result,
    Action<IDbConnection, TestContext> executionFlow)
        {
            result.IsCompiledSuccessfully = true;

            string databaseName = null;
            try
            {
                foreach (var test in executionContext.Input.Tests)
                {
                    databaseName = this.GetDatabaseName();

                    using (var connection = this.GetOpenConnection(databaseName))
                    {
                        executionFlow(connection, test);
                    }

                    this.DropDatabase(databaseName);
                }
            }
            catch (Exception ex)
            {
                if (!string.IsNullOrWhiteSpace(databaseName))
                {
                    this.DropDatabase(databaseName);
                }

                result.IsCompiledSuccessfully = false;
                result.CompilerComment = ex.Message;
            }

            return result;
        }
        public string ToHexString(byte[] bytes)
        {
            if (bytes == null)
            {
                throw new ArgumentNullException(nameof(bytes));
            }

            var hexChars = new[] { '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', 'A', 'B', 'C', 'D', 'E', 'F' };

            var bytesCount = bytes.Length;
            var resultChars = new char[(bytesCount * 2) + 2];

            resultChars[0] = '0';
            resultChars[1] = 'x';

            var bytesIndex = 0;
            var resultCharsIndex = 2;
            while (bytesIndex < bytesCount)
            {
                var @byte = bytes[bytesIndex++];
                resultChars[resultCharsIndex++] = hexChars[@byte / 0x10];
                resultChars[resultCharsIndex++] = hexChars[@byte % 0x10];
            }

            return new string(resultChars, 0, resultChars.Length);
        }
        protected virtual string GetDataRecordFieldValue(IDataRecord dataRecord, int index)
        {
            string result;

            if (dataRecord.IsDBNull(index))
            {
                result = null;
            }
            else
            {
                var fieldType = dataRecord.GetFieldType(index);

                // Using CultureInfo.InvariantCulture to have consistent decimal separator.
                if (fieldType == DecimalType)
                {
                    result = dataRecord.GetDecimal(index).ToString(CultureInfo.InvariantCulture);
                }
                else if (fieldType == DoubleType)
                {
                    result = dataRecord.GetDouble(index).ToString(CultureInfo.InvariantCulture);
                }
                else if (fieldType == FloatType)
                {
                    result = dataRecord.GetFloat(index).ToString(CultureInfo.InvariantCulture);
                }
                else if (fieldType == ByteArrayType)
                {
                    var bytes = (byte[])dataRecord.GetValue(index);
                    result = ToHexString(bytes);
                }
                else
                {
                    result = dataRecord.GetValue(index).ToString();
                }
            }

            return result;
        }

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

        protected virtual string FixCommandText(string commandText)
            => commandText;

        protected SqlResult ExecuteReader(
            IDbConnection connection,
            string commandText,
            int timeLimit = DefaultTimeLimit)
        {
            using (var command = connection.CreateCommand())
            {
                command.CommandText = commandText;

                using (var reader = command.ExecuteReader())
                {
                    var sqlTestResult = new SqlResult();
                    sqlTestResult.Completed = CodeHelpers.ExecuteWithTimeLimit(
                        TimeSpan.FromMilliseconds(timeLimit),
                        () =>
                        {
                            do
                            {
                                while (reader.Read())
                                {
                                    for (var i = 0; i < reader.FieldCount; i++)
                                    {
                                        var fieldValue = this.GetDataRecordFieldValue(reader, i);

                                        sqlTestResult.Results.Add(fieldValue);
                                    }
                                }
                            }
                            while (reader.NextResult());
                        });

                    return sqlTestResult;
                }
            }
        }

        protected void ProcessSqlResult(
            SqlResult sqlResult,
            IExecutionContext<TestsInputModel> executionContext,
            TestContext test,
            IExecutionResult<TestResult> result)
        {
            if (sqlResult.Completed)
            {
                var joinedUserOutput = string.Join(Environment.NewLine, sqlResult.Results);

                var checker = executionContext.Input.GetChecker();

                var checkerResult = checker.Check(
                    test.Input,
                    joinedUserOutput,
                    test.Output,
                    test.IsTrialTest);

                result.Results.Add(new TestResult
                {
                    Id = test.Id,
                    ResultType =
                        checkerResult.IsCorrect
                            ? TestRunResultType.CorrectAnswer
                            : TestRunResultType.WrongAnswer,
                    CheckerDetails = checkerResult.CheckerDetails
                });
            }
            else
            {
                result.Results.Add(new TestResult
                {
                    Id = test.Id,
                    TimeUsed = executionContext.TimeLimit,
                    ResultType = TestRunResultType.TimeLimit,
                });
            }
        }
    }
}
