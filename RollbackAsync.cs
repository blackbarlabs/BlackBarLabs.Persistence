using BlackBarLabs.Collections.Generic;
using BlackBarLabs.Core;
using BlackBarLabs.Core.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace BlackBarLabs.Persistence
{
    public class RollbackAsync<TSuccess, TFailure>
    {
        public sealed class RollbackResult
        {
            public TFailure FailureResult { get; private set; }

            public TSuccess SuccessResult { get; private set; }

            public bool IsFailure { get; private set; }

            private Func<Task> rollback = default(Func<Task>);

            private RollbackResult()
            {

            }

            internal static RollbackResult Success(TSuccess successResult, Func<Task> rollback)
            {
                return new RollbackResult()
                {
                    rollback = rollback,
                    SuccessResult = successResult,
                    IsFailure = false,
                };
            }

            internal static RollbackResult Failure(TFailure failureResult)
            {
                return new RollbackResult()
                {
                    rollback = default(Func<Task>),
                    FailureResult = failureResult,
                    IsFailure = true,
                };
            }

            internal Task Rollback()
            {
                if (default(Func<Task>) != rollback)
                    return rollback();
                return true.ToTask();
            }
        }

        public delegate Task<RollbackResult> RollbackTaskDelegate(Func<TSuccess, Func<Task>, RollbackResult> success, Func<TFailure, RollbackResult> failure);

        public RollbackAsync()
        {
            this.Tasks = new List<RollbackTaskDelegate>();
        }
        
        public List<RollbackTaskDelegate> Tasks;

        public void AddTask(RollbackTaskDelegate task)
        {
            this.Tasks.Add(task);
        }

        public async Task<TResult> ExecuteAsync<TResult>(
            Func<TSuccess[], TResult> success,
            Func<TFailure, TResult> failed)
        {
            var results = await Tasks
                .Select(async task =>
                {
                    var rollbackResult = await task.Invoke(
                        (successResultReturned, rollback) =>
                        {
                            return RollbackResult.Success(successResultReturned, rollback);
                            //{
                            //    success = successResultReturned,
                            //    failure = failureResult,
                            //    result = result,
                            //    rollback = rollback,
                            //};
                        },
                        (failureResultReturned) =>
                        {
                            return RollbackResult.Failure(failureResultReturned);
                        });
                    return rollbackResult;
                })
                .WhenAllAsync();

            var resultGlobal = await results.FirstOrDefault(
                result => result.IsFailure,
                async (failedResult) =>
                {
                    await results.Select(result => result.Rollback()).WhenAllAsync();
                    return failed(failedResult.FailureResult);
                },
                async () =>
                {
                    var successes = results.Select(result => result.SuccessResult).ToArray();
                    return await success(successes).ToTask();
                });
            return resultGlobal;
        }
    }
}
