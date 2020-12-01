using System;
using System.Collections;
using System.IO;
using System.Text;
using System.Threading;
using NUnit.Framework;
using UnityEngine;

using Unity.Simulation;
#if ENABLE_CLOUDTESTS
using Unity.Simulation.Tools;
#endif
using UnityEngine.TestTools;
using Assert = UnityEngine.Assertions.Assert;

public class AsyncRequestTests
{
    private static AsyncRequest.ExecutionContext[] execContext = new[]
    {
        AsyncRequest.ExecutionContext.ThreadPool,
        AsyncRequest.ExecutionContext.JobSystem
    };
    
    [UnityTest]
    public IEnumerator AsyncRequest_AllocatesAndReturnsToPool()
    {
        using (var req = Manager.Instance.CreateRequest<AsyncRequest<object>>())
        {
            req.Enqueue( (AsyncRequest<object> r) =>
            {
                return AsyncRequest.Result.Completed;
            });
            req.Execute();

            while (!req.completed)
                yield return null;
        }
        
        Assert.IsTrue(Manager.Instance.requestPoolCount == 1,  "requestPoolCount == 1");
        using (var req = Manager.Instance.CreateRequest<AsyncRequest<object>>())
        {
            Assert.IsTrue(Manager.Instance.requestPoolCount == 0, "requestPoolCount == 0");

            req.Enqueue( (AsyncRequest<object> r) =>
            {
                return AsyncRequest.Result.Completed;
            });
            req.Execute();

            while (!req.completed)
                yield return null;
        }

        Assert.IsTrue(Manager.Instance.requestPoolCount == 1, "requestPoolCount == 1");
    }

#if ENABLE_CLOUDTESTS
    [CloudTest]
#endif
    [UnityTest]
    public IEnumerator AsyncRequest_StartingRequestNTimesProducesNResults([ValueSource("execContext")] AsyncRequest.ExecutionContext executionContext)
    {
        using (var req = Manager.Instance.CreateRequest<AsyncRequest<object>>())
        {
            var N = UnityEngine.Random.Range(10, 1000);

            for (int i = 0; i < N; ++i)
            {
                req.Enqueue( (AsyncRequest<object> r) =>
                {
                    return AsyncRequest.Result.Completed;
                });
            }
            req.Execute(executionContext);

            while (!req.completed)
                yield return null;

            Debug.Assert(req.results.Length == N);
        }
    }

#if ENABLE_CLOUDTESTS
    [CloudTest]
#endif
    [UnityTest]
    public IEnumerator AsyncRequest_JobSchedule_HappensOnMainThread()
    {
        var jobCompleted = false;

        var thread = new Thread(new ThreadStart(() =>
        {
            using (var req = Manager.Instance.CreateRequest<AsyncRequest<object>>())
            {
                req.Enqueue( r =>
                {
                    jobCompleted = true;
                    return AsyncRequest.Result.Completed;
                });
                req.Execute();
            }
        }));

        thread.Start();

        while (!jobCompleted)
            yield return null;

        thread.Join();
    }

    [UnityTest]
    public IEnumerator AsyncRequest_DispatchImmediately()
    {
        var tempFilePath = Path.Combine(Application.persistentDataPath, "Test_AsyncRequest.txt");
        var content = "This is an asyncrequest test";
        using (var req = Manager.Instance.CreateRequest<AsyncRequest<object>>())
        {
            req.Enqueue(r =>
            {
                File.WriteAllBytes(tempFilePath, Encoding.ASCII.GetBytes(content));
                return AsyncRequest.Result.Completed;
            });
            
            req.Execute(AsyncRequest.ExecutionContext.Immediate);
            Assert.IsTrue(File.Exists(tempFilePath));
            File.Delete(tempFilePath);
        }

        yield return null;
    }
}
