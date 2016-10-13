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
        public Func<Action<TSuccess>, Action<TFailure>, Task<Func<Task>>> [] Tasks;

        public async Task<TResult> ExecuteAsync<TResult>(
            Func<TSuccess[], TResult> success,
            Func<TFailure, TResult> failed)
        {
            var results = await Tasks
                .Select(async task =>
                {
                    var result = false;
                    var successResult = default(TSuccess);
                    var failureResult = default(TFailure);
                    var rollback = await task.Invoke(
                        (successResultReturned) =>
                        {
                            successResult = successResultReturned;
                            result = true;
                        },
                        (failureResultReturned) =>
                        {
                            failureResult = failureResultReturned;
                        });
                    return new
                    {
                        success = successResult,
                        failure = failureResult,
                        result = result,
                        rollback = rollback,
                    };
                })
                .WhenAllAsync();

            var resultGlobal = await results.FirstOrDefault(
                result => !result.result,
                async (failedResult) =>
                {
                    await results.Select(result => result.rollback()).WhenAllAsync();
                    return failed(failedResult.failure);
                },
                async () =>
                {
                    var successes = results.Select(result => result.success).ToArray();
                    return await success(successes).ToTask();
                });
            return resultGlobal;
        }
    }
}
