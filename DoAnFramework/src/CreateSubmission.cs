﻿using System;
using System.Linq;

namespace DoAnFramework
{
    public class CreateSubmission
   : ICreateSubmission
    {
        private readonly IFormatter formatterServicesFactory;
        private readonly HttpService http;
        private readonly string endpoint;

        public CreateSubmission(string endpointRoot, IFormatter formatterServicesFactory)
        {
            this.formatterServicesFactory = formatterServicesFactory;
            this.Location = endpointRoot;
            this.endpoint = $"{endpointRoot}/executeSubmission?keepDetails=true&escapeTests=false";
            this.http = new HttpService();
        }

        public string Location { get; }

        public IExecutionResult<TResult> RunSubmission<TInput, TResult>(Submission<TInput> submission)
            where TResult : class, ISingleCodeRunResult, new()
        {
            var testInputSubmission = submission as Submission<TestsInputModel>;
            var submissionRequestBody = this.BuildRequestBody(testInputSubmission);

            var result = this.http.PostJson<object, RemoteSubmissionResult>(this.endpoint, submissionRequestBody);
            if (result.Exception != null)
            {
                throw new Exception(result.Exception.Message);
            }

            var executionResult = new ExecutionResult<TResult>
            {
                CompilerComment = result.ExecutionResult.CompilerComment,
                IsCompiledSuccessfully = result.ExecutionResult.IsCompiledSuccessfully,
                Results = result.ExecutionResult.TaskResult.TestResults
                    .Select(testResult =>
                    {
                        var test = testInputSubmission.Input.Tests.FirstOrDefault(t => t.Id == testResult.Id);
                        return this.BuildTestResult<TResult>(test, testResult);
                    })
                    .ToList(),
            };

            return executionResult;
        }

        private object BuildRequestBody(Submission<TestsInputModel> submission)
            => new
            {
                ExecutionType = this.formatterServicesFactory.Get<ExecutionType>()
                    .Format(submission.ExecutionType),
                ExecutionStrategy = this.formatterServicesFactory.Get<ExecutionStrategyType>()
                    .Format(submission.ExecutionStrategyType),
                FileContent = string.IsNullOrEmpty(submission.Code)
                    ? submission.FileContent
                    : null,
                Code = submission.Code ?? string.Empty,
                submission.TimeLimit,
                submission.MemoryLimit,
                ExecutionDetails = new
                {
                    MaxPoints = submission.MaxPoints,
                    CheckerType = this.formatterServicesFactory.Get<string>()
                        .Format(submission.Input.CheckerTypeName),
                    submission.Input.CheckerParameter,
                    submission.Input.Tests,
                    submission.Input.TaskSkeleton,
                    submission.Input.TaskSkeletonAsString,
                },
            };

        private TResult BuildTestResult<TResult>(TestContext test, TestResultResponseModel testResult)
            where TResult : class
        {
            Enum.TryParse(testResult.ResultType, out TestRunResultType resultType);

            var result = new TestResult
            {
                Input = test.Input,
                IsTrialTest = test.IsTrialTest,
                ExecutionComment = testResult.ExecutionComment,
                MemoryUsed = testResult.MemoryUsed,
                TimeUsed = testResult.TimeUsed,
                ResultType = resultType,
                Id = test.Id,
                CheckerDetails = new CheckerDetails
                {
                    Comment = testResult.CheckerDetails.Comment,
                    UserOutputFragment = testResult.CheckerDetails.UserOutputFragment,
                    ExpectedOutputFragment = testResult.CheckerDetails.ExpectedOutputFragment,
                },
            };

            return result as TResult;
        }
    }
}
