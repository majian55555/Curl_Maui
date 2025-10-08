using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Dataflow;
using OneOf;

namespace Curl_maui;

public class StaticValues
{
    public static string CurrentSaveVideoFilePath()
    {
        return Path.Combine(FileSystem.Current.AppDataDirectory, $"Video_{DateTime.UtcNow.ToLocalTime().ToDateTimeFileString()}.mp4");
    }
    public static string CurrentConfigFilePath()
    {
        return Path.Combine(FileSystem.Current.AppDataDirectory, "curl_maui.config");
    }
    public static string GetDownloadsFolderPath()
    {
        return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),"Downloads");
    }
}

public static class Tasks
{
    public static ConfiguredTaskAwaitable CAF(this Task task)
    {
        return task.ConfigureAwait(false);
    }

    /// <summary>
    /// Extension created to enable the ablity to use a null conditional operator with CAF() as in 'await foo?.CAF()' 
    /// </summary>
    public static ConfiguredTaskAwaitable.ConfiguredTaskAwaiter GetAwaiter(this ConfiguredTaskAwaitable? task)
    {
        if (task is not null)
        {
            return task.Value.GetAwaiter();
        }
        return Task.CompletedTask.CAF().GetAwaiter();
    }

    /// <summary>
    /// Extension created to enable the ablity to use a null conditional operator with CAF() as in 'await foo?.CAF()' 
    /// </summary>
    public static ConfiguredTaskAwaitable<T>.ConfiguredTaskAwaiter GetAwaiter<T>(this ConfiguredTaskAwaitable<T>? task)
    {
        if (task is not null)
        {
            return task.Value.GetAwaiter();
        }
        return Task.FromResult<T>(default).CAF().GetAwaiter();
    }

    public static ConfiguredValueTaskAwaitable CAF(this ValueTask task)
    {
        return task.ConfigureAwait(false);
    }

    public static ConfiguredValueTaskAwaitable<T> CAF<T>(this ValueTask<T> task)
    {
        return task.ConfigureAwait(false);
    }

    public static ConfiguredTaskAwaitable<T> CAF<T>(this Task<T> task)
    { return task.ConfigureAwait(false); }

    public static Task<T> RunContinuationsAsynchronously<T>(this Task<T> task)
    {
        var tcs = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);
        task.ContinueWith((t, o) =>
        {
            if (t.IsFaulted)
            {
                tcs.SetException(t.Exception.InnerExceptions);
            }
            else if (t.IsCanceled)
            {
                tcs.SetCanceled();
            }
            else
            {
                tcs.SetResult(t.Result);
            }
        }, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return tcs.Task;
    }

    public static Task RunContinuationsAsynchronously(this Task task)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        task.ContinueWith((t, o) =>
        {
            if (t.IsFaulted)
            {
                tcs.SetException(t.Exception.InnerExceptions);
            }
            else if (t.IsCanceled)
            {
                tcs.SetCanceled();
            }
            else
            {
                tcs.SetResult(true);
            }
        }, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        return tcs.Task;
    }

    /// <summary>
    /// Use this on a task and await the result to guarantee the awaited task will only throw 
    /// OperationCanceledException (and that only when the exception token matches the given token)
    /// </summary>
    public static async Task OnlyThrowCancelled(this Task t, CancellationToken token,
        Action<Exception>? onError = null)
    {
        try
        { await t.CAF(); }
        catch (Exception ex) when (!(ex is OperationCanceledException cex) || cex.CancellationToken != token)
        { onError?.Invoke(ex); }
    }

    public static void WaitIgnoreCancelled(this Task t, CancellationToken token)
    {
        try
        { t.Wait(); }
        catch (AggregateException ex)
            when (ex.InnerException is OperationCanceledException cex
                && cex.CancellationToken == token)
        { }
    }

    public static async Task IgnoreCancelled(this Task t)
    {
        try
        { await t.CAF(); }
        catch (OperationCanceledException) { }
    }

    public static async Task<T> IgnoreCancelled<T>(this Task<T> t)
    {
        T ret = default;
        try
        { ret = await t.CAF(); }
        catch (OperationCanceledException) { }
        return ret;
    }

    public static async Task ThrowIfCancelled(this Task t, CancellationToken token)
    { await (await WhenAny(token, t).CAF()).CAF(); }

    public static async Task WaitDoRelease(this SemaphoreSlim mutex, Action toDo,
        CancellationToken token = default, int msTimeout = -1)
    {
        await mutex.WaitAsync(msTimeout, token).CAF();
        try
        { toDo(); }
        finally { mutex.Release(); }
    }
    public static async Task WaitDoRelease(this SemaphoreSlim mutex, Func<Task> toDo,
        CancellationToken token = default, int msTimeout = -1)
    {
        await mutex.WaitAsync(msTimeout, token).CAF();
        try
        { await toDo().CAF(); }
        finally { mutex.Release(); }
    }

    /// <summary>
    /// Returns either when the task completes or the timeout elapses, whichever comes first,
    /// with a result indicating which
    /// </summary>
    /// <param name="task">task on which to wait for the given amount of time</param>
    /// <param name="timeout">amount of time to wait</param>
    /// <returns>true if the amount of time elapses before the task completes</returns>
    public static async Task<bool> TakesLongerThan(this Task task, TimeSpan timeout)
    { return await Task.WhenAny(task, Task.Delay(timeout)).CAF() != task; }


    /// <summary>
    /// Returns either when the task completes or the timeout elapses, whichever comes first,
    /// with a result indicating which
    /// </summary>
    /// <param name="task">task on which to wait for the given amount of time</param>
    /// <param name="timeoutMs">amount of time to wait in milliseconds</param>
    /// <returns>true if the amount of time elapses before the task completes</returns>
    public static Task<bool> TakesLongerThan(this Task task, double timeoutMs)
    { return TakesLongerThan(task, TimeSpan.FromMilliseconds(timeoutMs)); }

    /// <summary>
    /// Returns either when the task completes or the timeout (if specified) elapses, whichever
    /// comes first. The result is false if the timeout elapsed first or the task's result is not
    /// as expected.
    /// </summary>
    /// <param name="task">Task on which to wait</param>
    /// <param name="status">Status the task should have when it completes</param>
    /// <param name="timeoutMs">Optional amount of time to wait in milliseconds.
    /// Not an optional parameter, DO NOT use -1 (-1 has a high probability of creating leaks)</param>
    /// <returns>false if the timeout elapsed first or the task's result is not as expected</returns>
    public static async Task<bool> RunsToDesiredStatus(this Task task, TaskStatus status, double timeoutMs)
    {
        if (timeoutMs <= 0)
        { await task.CAF(); }
        else if (await task.TakesLongerThan(timeoutMs).CAF())
        { return false; }
        return task.Status == status;
    }

    /// <summary>
    /// Returns the first of the given tasks to complete, or a task cancelled with the
    /// given cancellation token if it is cancelled
    /// </summary>
    /// <param name="tasks">tasks on which to wait for completion</param>
    /// <param name="token">token on which to wait for cancellation</param>
    /// <returns>first of the given tasks to complete, or a task cancelled with the 
    /// given cancellation token if it is cancelled</returns>
    public static async Task<Task> WhenAny(this IEnumerable<Task> tasks, CancellationToken token)
    {
        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var ctr = token.Register(() => tcs.TrySetCanceled(token));
        return await Task.WhenAny(tasks.Append(tcs.Task)).CAF();
    }

    /// <summary>
    /// Returns the first of the given tasks to complete, or a task cancelled with the
    /// given cancellation token if it is cancelled
    /// </summary>
    /// <param name="token">token on which to wait for cancellation</param>
    /// <param name="tasks">tasks on which to wait for completion</param>
    /// <returns>first of the given tasks to complete, or a task cancelled with the 
    /// given cancellation token if it is cancelled</returns>
    public static async Task<Task> WhenAny(CancellationToken token, params Task[] tasks)
    {
        return await tasks.WhenAny(token).CAF();
    }

    private static async Task InternalSafeWait(Task t, int timeoutms)
    {
        await Task.WhenAny(t, Task.Delay(timeoutms)).CAF();
    }

    public static void SafeWait(this Task t, int timeoutms)
    {
        InternalSafeWait(t, timeoutms).Wait();
    }
}

public static class Extensions
{
    public static string ToDateTimeFileString(this DateTime t)
    { return t.ToString("yyyy-MM-dd-HH-mm-ss"); }

    /// <summary>
    /// Opens a file, appends the specified line to the file, creates a new line after this one,
    /// and closes the file. If the file does not exist, this method creates the file.
    /// </summary>
    public static void FileAppendLine(this string filename, string line = "")
    { File.AppendAllText(filename, line + Environment.NewLine); }
    /// <summary>
    /// Returns the inner exception within this exception
    /// </summary>
    public static Exception Unwrap(this Exception ex)
    { return ex.InnerException == null ? ex : Unwrap(ex.InnerException); }

    public static bool SameTypeAs(this Exception ex, Exception other)
    { return ex.GetType().Name == other.GetType().Name; }

    /// <summary>
    /// Generates a string containing the exception's type and error message
    /// </summary>
    public static string ToTypeAndMessageString(this Exception ex)
    { return $"{ex.GetType().Name}: {ex.Message}"; }
    /// <summary>
    /// Checks whether an exception was due to an expected cancellation
    /// </summary>
    /// <param name="rightToken">The token that should have been used to cancel</param>
    /// <returns></returns>
    public static bool IsRightCancellation(this Exception ex, CancellationToken rightToken)
    { return ex is OperationCanceledException cex && cex.CancellationToken == rightToken; }
    /// <summary>
    /// Checks whether the operation was cancelled by the provided token
    /// </summary>
    public static bool WasCanceledBy(this OperationCanceledException cex, CancellationToken token)
    { return cex.CancellationToken == token; }
    /// <summary>
    /// Returns true if the exception either is not due to cancellation, or if it is a 
    /// cancellation exception but did not come from the provided token
    /// </summary>
    public static bool IsNotRightCancellation(this Exception ex, CancellationToken rightToken)
    { return !(ex is OperationCanceledException cex && cex.WasCanceledBy(rightToken)); }

    public static Tuple<T1, T2> ToTuple<T1, T2>(this KeyValuePair<T1, T2> pair)
    { return Tuple.Create(pair.Key, pair.Value); }

    public static IEnumerable<(T1, T2)> OuterProduct<T1, T2>(this IEnumerable<T1> items1, IEnumerable<T2> items2)
    {
        foreach (T1 item1 in items1)
        {
            foreach (T2 item2 in items2)
            { yield return (item1, item2); }
        }
    }

    public static SortedList<TKey, TValue> ToSortedList<TKey, TValue>(this IDictionary<TKey, TValue> dict,
        IComparer<TKey> comparer)
    { return dict == null ? new SortedList<TKey, TValue>(comparer) : new SortedList<TKey, TValue>(dict, comparer); }

    public static ConcurrentDictionary<TKey, TValue> ToConcurrentDictionary<T, TKey, TValue>(
        this IEnumerable<T> items, Func<T, TKey> getKey, Func<T, TValue> getValue)
    { return new ConcurrentDictionary<TKey, TValue>(items.ToDictionary(getKey, getValue)); }

    public static ConcurrentBag<T> ToConcurrentBag<T>(this IEnumerable<T> items)
    { return new ConcurrentBag<T>(items); }

    /// <summary> Attempts to remove and return the value that has the specified key </summary>
    /// <param name="key">The key of the element to remove</param>
    /// <returns>true if the object was removed successfully; otherwise, false</returns>
    public static bool TryRemove<TKey, TValue>(this ConcurrentDictionary<TKey, TValue> d, TKey key)
    { return d.TryRemove(key, out _); }

    public static void Clear<T>(this ConcurrentQueue<T> q)
    {
        while (q.TryDequeue(out _))
        { }
    }

    /// <summary>
    /// This extension method returns all available items in the IReceivableSourceBlock
    /// or an empty sequence if nothing is available. The function does not wait.
    /// </summary>
    /// <typeparam name="T">The type of items stored in the IReceivableSourceBlock</typeparam>
    /// <param name="buffer">the source where the items should be extracted from </param>
    /// <returns>The IList with the received items. Empty if no items were available</returns>
    public static bool TryReceiveAllEx<T>(this IReceivableSourceBlock<T> buffer, out IList<T> receivedItems)
    {
        /* Microsoft TPL Dataflow version 4.5.24 contains a bug in TryReceiveAll
         * Hence this function uses TryReceive until nothing is available anymore */
        if (!buffer.TryReceive(out T receivedItem))
        {
            receivedItems = null;
            return false;
        }
        receivedItems = new List<T>() { receivedItem };
        while (buffer.TryReceive(out receivedItem))
        {
            receivedItems.Add(receivedItem);
        }
        return true;
    }

    /// <summary>
    /// This extension method return one or more items in the IReceivableSourceBlock.
    /// It wait until first item arrived, then synchronously get rest items if there is any.
    /// </summary>
    /// <typeparam name="T">The type of items stored in the IReceivableSourceBlock</typeparam>
    /// <param name="buffer">the source where the items should be extracted from </param>
    /// <returns>The IList with the received items. Empty if no items were available</returns>
    // TODO: need to change this function name, since NET6 has the same name for IReceivableSourceBlock.
    // https://learn.microsoft.com/lb-lu/dotnet/api/system.threading.tasks.dataflow.dataflowblock.receiveallasync?view=net-7.0&viewFallbackFrom=windowsdesktop-6.0
    public static async Task<List<T>> ReceiveAllAsync<T>(this IReceivableSourceBlock<T> buffer, CancellationToken token = default)
    {
        T receivedItem = await buffer.ReceiveAsync(token).CAF();
        token.ThrowIfCancellationRequested();
        List<T> receivedItems = new List<T>() { receivedItem };
        while (buffer.TryReceive(out receivedItem))
        { receivedItems.Add(receivedItem); }
        return receivedItems;
    }

    /// <summary>
    /// Returns true if the task ran to completion OR was canceled or faulted
    /// </summary>
    public static bool IsComplete(this TaskStatus s)
    {
        switch (s)
        {
            case TaskStatus.Canceled:
            case TaskStatus.Faulted:
            case TaskStatus.RanToCompletion:
                return true;
            default: return false;
        }
    }

    /// <summary>
    /// Checks if two lists have equivalent values, irrespective of the ordering of those values
    /// </summary>
    public static bool ScrambledEquals<T>(this IEnumerable<T> list1, IEnumerable<T> list2)
    {
        var countPerItem = new Dictionary<T, int>();
        int nullCount1 = 0;
        int nullCount2 = 0;
        foreach (T item in list1)
        {
            if (item == null)
            {
                nullCount1++;
                continue;
            }
            if (countPerItem.ContainsKey(item))
            { countPerItem[item]++; }
            else
            { countPerItem.Add(item, 1); }
        }
        foreach (T item in list2)
        {
            if (item == null)
            {
                nullCount2++;
                continue;
            }
            if (countPerItem.ContainsKey(item))
            { countPerItem[item]--; }
            else
            { return false; }
        }
        return countPerItem.Values.All(c => c == 0) && nullCount1 == nullCount2;
    }

    public static bool EqualsAny<T>(this T thing, params T[] toCheck)
    {
        EqualityComparer<T> comparer = EqualityComparer<T>.Default;
        return toCheck.Any(x => comparer.Equals(thing, x));
    }
    /// <summary>
    /// Splits a list into several smaller lists of the specified size.
    /// </summary>
    public static IEnumerable<List<T>> SplitList<T>(this List<T> longList, int nSize = 10)
    {
        for (int i = 0; i < longList.Count; i += nSize)
        {
            yield return longList.GetRange(i, Math.Min(nSize, longList.Count - i));
        }
    }

    public static IEnumerable<ConcurrentBag<T>> SplitConcurrentBag<T>(this ConcurrentBag<T> longList, int nSize = 10)
    {
        for (int i = 0; i < longList.Count; i += nSize)
        {
            yield return longList.ToList().GetRange(i, Math.Min(nSize, longList.Count - i)).ToConcurrentBag();
        }
    }

    /// <summary>Get a random subset of an ordered set. The subset has a given subsetSize 
    /// and maintains the original order of the set.</summary>
    /// <typeparam name="T">List data type.</typeparam>
    /// <param name="set">The list from which to select a random subset.</param>
    /// <param name="subsetSize">The number of elements that should be in the random subset.</param>
    /// <param name="rand">The randomizer used to select the subset. Defaults to new Random().</param>
    /// <returns>A random subset that has a given subsetSize 
    /// and maintains the order of the original set.</returns>
    public static IReadOnlyList<T> GetRandomSubset<T>(this IReadOnlyList<T> set, int subsetSize, Random rand = null)
    { return set.GetIdxsRandomSubset(subsetSize, rand).Select(x => set[x]).ToList(); }

    /// <summary>Get the indexes of a random subset of an ordered set. The subset has a given subsetSize 
    /// and maintains the original order of the set.</summary>
    /// <typeparam name="T">List data type.</typeparam>
    /// <param name="set">The list from which to select a random subset.</param>
    /// <param name="subsetSize">The number of elements that should be in the random subset.</param>
    /// <param name="rand">The randomizer used to select the subset indexes. Defaults to new Random().</param>
    /// <returns>The indexes of a random subset that has a given subsetSize 
    /// and maintains the order of the original set.</returns>
    public static IReadOnlyList<int> GetIdxsRandomSubset<T>(this IReadOnlyList<T> set, int subsetSize, Random rand = null)
    {
        rand = rand ?? new Random();
        int totalSetSize = set.Count();
        if (totalSetSize < subsetSize)
        { throw new Exception("subsetSize cannot be larger than set.Count()"); }
        else if (subsetSize < 0)
        { throw new Exception("Cannot take a negatively sized subset"); }
        return Enumerable.Range(0, totalSetSize).OrderBy(_ => rand.Next()).Take(subsetSize).OrderBy(x => x).ToList();
    }

    /// <summary>Throws an exception if the two IEnumerables are not the same length.</summary>
    /// <typeparam name="T1">list1 type</typeparam>
    /// <typeparam name="T2">list2 type</typeparam>
    /// <param name="list1">First list to compare to second list</param>
    /// <param name="list2">Second list to compare to first list</param>
    /// <exception cref="Exception">The exception for a list length mismatch.</exception>
    public static void AssertSameSize<T1, T2>(
        this IEnumerable<T1> list1,
        IEnumerable<T2> list2)
    {
        if (list1.Count() != list2.Count())
        { throw new Exception("List1 and List2 must have the same Count"); }
    }

    /// <summary>
    /// Utility method for get bit value in byte. pos 0 is least significant bit, pos 7 is most.
    /// </summary>
    /// <returns>bit value in pos of b</returns>
    /// <param name="b">this byte</param>
    /// <param name="pos">position in this byte</param>
    public static bool GetBit(this byte b, int pos)
    {
        return (b & (1 << pos)) != 0;
    }

    /// <summary>
    ///  from https://stackoverflow.com/questions/27427527/how-to-get-a-complete-row-or-column-from-2d-array-in-c-sharp
    /// </summary>
    public static T[] GetRow<T>(this T[,] array, int row)
    {
        if (!typeof(T).IsPrimitive)
            throw new InvalidOperationException("Not supported for managed types.");

        if (array == null)
            throw new ArgumentNullException("array");

        int cols = array.GetUpperBound(1) + 1;
        T[] result = new T[cols];

        int size;

        if (typeof(T) == typeof(bool))
            size = 1;
        else if (typeof(T) == typeof(char))
            size = 2;
        else
            size = Marshal.SizeOf<T>();

        Buffer.BlockCopy(array, row * cols * size, result, 0, cols * size);

        return result;
    }

    public static async Task<string[]> ReadAllLinesAsync(this FileInfo info, StringSplitOptions splitOptions = StringSplitOptions.None)
    {
        string line = await info.ReadAllAsync();
        return line.Split(new[] { "\r\n", "\r", "\n" }, splitOptions);
    }

    public static async Task<string> ReadAllAsync(this FileInfo info)
    {
        using FileStream inputStream = info.OpenRead();
        byte[] buffer = new byte[inputStream.Length];
        int bufferIndex = await inputStream.ReadAsync(buffer, 0, (int)inputStream.Length);
        return System.Text.Encoding.ASCII.GetString(buffer);
    }

    public static Task<int> ReadAsyncEx(this NetworkStream stream, byte[] buffer, int offset, int count)
    {
        if (stream == null)
        { throw new ArgumentNullException(nameof(NetworkStream)); }
        return Task<int>.Factory.FromAsync(stream.BeginRead, stream.EndRead, buffer, offset, count, null);
    }
    public static Task WriteAsyncEx(this NetworkStream stream, byte[] buffer, int offset, int count)
    {
        if (stream == null)
        { throw new ArgumentNullException(nameof(NetworkStream)); }
        return Task.Factory.FromAsync(stream.BeginWrite, stream.EndWrite, buffer, offset, count, null);
    }
    public static Task<int> ReceiveAsyncEx(this Socket socket, byte[] buffer, SocketFlags socketFlags)
    {
        return Task<int>.Factory.FromAsync((targetBuffer, flags, callback, state) =>
            ((Socket)state).BeginReceive(targetBuffer, 0, targetBuffer.Length, flags, callback, state),
            asyncResult => ((Socket)asyncResult.AsyncState).EndReceive(asyncResult), buffer, socketFlags, state: socket);
    }

    /// <summary>
    /// Per https://learn.microsoft.com/en-us/dotnet/api/system.net.sockets.socket.receivetimeout?view=net-6.0, 
    /// the configured receive timeout only affects synchronous calls. So this is a workaround.
    /// </summary>
    /// <param name="socket">socket from which to receive</param>
    /// <param name="buffer">buffer into which bytes will be received</param>
    /// <param name="socketFlags">options for receive operation</param>
    /// <param name="token">token to cancel waiting</param>
    /// <returns>number of bytes received</returns>
    /// <exception cref="TimeoutException">thrown if times out before receive completes</exception>
    public static async Task<int> ReceiveAsyncWithTimeout(this Socket socket, byte[] buffer,
        SocketFlags socketFlags, CancellationToken token = default)
    {
        int bytesReceived;
        IAsyncResult asyncResult = socket.BeginReceive(buffer, 0, buffer.Length, socketFlags, null, null);
        Task<int> receiveTask = Task<int>.Factory.FromAsync(asyncResult, _ => socket.EndReceive(asyncResult));
        Task finished = await Task.WhenAny(receiveTask, Task.Delay(socket.ReceiveTimeout, token)).CAF();
        if (receiveTask == finished)
        {
            bytesReceived = await receiveTask.CAF();
            return bytesReceived;
        }
        else if (finished.Status == TaskStatus.RanToCompletion)
        { throw new TimeoutException(nameof(ReceiveAsyncWithTimeout)); }
        else // to throw correct cancellation exception
        { await finished.CAF(); }
        // this should not be reachable but the compiler doesn't know that.
        throw new UnreachableException();
    }
    public static Task<int> SendAsyncEx(this Socket socket, byte[] buffer, SocketFlags socketFlags)
    {
        return Task<int>.Factory.FromAsync((targetBuffer, flags, callback, state) =>
            ((Socket)state).BeginSend(targetBuffer, 0, targetBuffer.Length, flags, callback, state),
            asyncResult => ((Socket)asyncResult.AsyncState).EndSend(asyncResult), buffer, socketFlags, state: socket);
    }
    public static Task<int> SendAsyncEx(this Socket socket, IList<ArraySegment<byte>> buffer, SocketFlags socketFlags)
    {
        return Task<int>.Factory.FromAsync((targetBuffer, flags, callback, state) =>
            ((Socket)state).BeginSend(targetBuffer, flags, callback, state),
            asyncResult => ((Socket)asyncResult.AsyncState).EndSend(asyncResult), buffer, socketFlags, state: socket);
    }
    public static async Task<int> SendAsyncWithTimeout(this Socket socket, IList<ArraySegment<byte>> buffer, SocketFlags socketFlags)
    {
        int bytesSent;
        var asyncResult = socket.BeginSend(buffer, socketFlags, null, null);
        var sendTask = Task<int>.Factory.FromAsync(asyncResult, _ => socket.EndSend(asyncResult));
        if (sendTask == await Task.WhenAny(sendTask, Task.Delay(socket.SendTimeout)).CAF())
        { bytesSent = await sendTask.CAF(); }
        else
        { throw new TimeoutException(nameof(SendAsyncWithTimeout)); }
        return bytesSent;
    }

    public static IEnumerable<TOut> SelectWhereIs<TOut>(this IEnumerable<object> things)
        where TOut : class
    {
        foreach (var thing in things)
        {
            var ret = thing as TOut;
            if (ret != null)
            { yield return ret; }
        }
    }

    public static Stream GetStreamFromString(this string s)
    {
        var stream = new MemoryStream();
        var writer = new StreamWriter(stream);
        writer.Write(s);
        writer.Flush();
        stream.Position = 0;
        return stream;
    }

    /// <summary>
    /// Convert integer to shortest byte array without leading zeros
    /// </summary>
    public static byte[] ToShortestByteArray(this int value)
    {
        var bytes = BitConverter.GetBytes(value);
        bool leadingZero = true;
        List<byte> res = new List<byte>();
        for (int i = bytes.Length - 1; i >= 0; --i)
        {
            if (!leadingZero || bytes[i] != 0)
            {
                leadingZero = false;
                res.Insert(0, bytes[i]);
            }
        }
        return res.ToArray();
    }
}

/// <summary>
/// from https://devblogs.microsoft.com/pfxteam/building-async-coordination-primitives-part-1-asyncmanualresetevent/
/// </summary>
public class AsyncManualResetEvent
{
    private volatile TaskCompletionSource<bool> _tcs
        = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsSet { get { return _tcs.Task.IsCompleted; } }

    public AsyncManualResetEvent()
    { }

    public Task WaitAsync() { return _tcs.Task; }

    public Task<bool> TryWaitAsync(int timeoutMs, CancellationToken token = default)
    { return TryWaitAsync(TimeSpan.FromMilliseconds(timeoutMs), token); }

    public async Task<bool> TryWaitAsync(TimeSpan timeout, CancellationToken token = default)
    {
        Task waitForSet = WaitAsync();
        Task complete = await Task.WhenAny(waitForSet, Task.Delay(timeout, token)).CAF();
        return complete == waitForSet;
    }

    public async Task WaitAsync(int timeoutMs, CancellationToken token = default)
    {
        Task waitForSet = WaitAsync();
        Task complete = await Task.WhenAny(waitForSet, Task.Delay(timeoutMs, token)).CAF();
        await complete.CAF();
    }

    public void Set()
    { _tcs.TrySetResult(true); }

    public void Reset()
    {
        while (true)
        {
            var tcs = _tcs;
            if (!tcs.Task.IsCompleted
                || Interlocked.CompareExchange(ref _tcs, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously), tcs) == tcs)
            { return; }
        }
    }

    public void SetReset()
    {
        var oldTcs = Interlocked.Exchange(ref _tcs, new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously));
        oldTcs.TrySetResult(true);
    }
}

/// <summary>
/// Represents an error.
/// </summary>
public interface IError
{
    /// <summary>
    /// Description of the error
    /// </summary>
    string Description { get; }
}

/// <summary>
/// Represents an error with an error code (probably an Enum)
/// </summary>
/// <typeparam name="TCode">error code, probably an Enum</typeparam>
public interface IError<TCode> : IError
    where TCode : struct
{
    /// <summary>
    /// Error code
    /// </summary>
    TCode Code { get; }
}

/// <inheritdoc/>
public class Error : IError
{
    /// <inheritdoc/>
    public string Description { get; }

    public Error(string description)
    {
        Description = description;
    }

    public static readonly Canceled Canceled = new Canceled();
    public static readonly TimedOut TimedOut = new TimedOut();
}

public class Canceled : Error
{
    internal Canceled() : base("Operation was canceled") { }
}

public class TimedOut : Error
{
    internal TimedOut() : base("Operation timed out") { }
}

/// <inheritdoc/>
public class Error<TCode> : Error, IError<TCode> where TCode : struct
{
    /// <inheritdoc/>
    public TCode Code { get; }
    public Error(string description, TCode code)
        : base(description)
    {
        Code = code;
    }

    public Error(TCode code, string appendToCodeForDescription = null)
        : base(appendToCodeForDescription is null ? code.ToString()
              : $"{code}: {appendToCodeForDescription}")
    {
        Code = code;
    }
}

/// <summary>
/// An asynchronous event that can be externally completed with a result
/// </summary>
/// <typeparam name="T">type of the result</typeparam>
public class AsyncManualResetEvent<T>
{
    private volatile TaskCompletionSource<T> _tcs
        = new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously);

    public bool IsSet { get { return _tcs.Task.IsCompleted; } }

    public AsyncManualResetEvent()
    { }

    public Task<T> WaitAsync() { return _tcs.Task; }

    /// <summary>
    /// Try to wait for the event to be completed with a result with an optional timeout and cancellation
    /// </summary>
    /// <param name="timeout">timeout, use TimeSpan.FromMilliseconds(-1) to not time out</param>
    /// <param name="token">token to cancel waiting (will not throw canceled exception)</param>
    /// <returns>discriminated union object with indicating the outcome with the result if applicable</returns>
    public async Task<OneOf<Canceled, TimedOut, T>> TryWaitAsync(TimeSpan timeout, CancellationToken token = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(token);
        Task<T> waitForSet = WaitAsync();
        Task complete = await Task.WhenAny(waitForSet, Task.Delay(timeout, cts.Token)).CAF();
        cts.Cancel();
        return complete == waitForSet ? waitForSet.Result
            : (token.IsCancellationRequested ? Error.Canceled : Error.TimedOut);
    }

    /// <summary>
    /// Try to wait for the event to be completed with a result with an optional timeout and cancellation
    /// </summary>
    /// <param name="timeoutMs">timeout in milliseconds, use -1 to not time out</param>
    /// <param name="token">token to cancel waiting (will not throw canceled exception)</param>
    /// <returns>discriminated union object with indicating the outcome with the result if applicable</returns>
    public Task<OneOf<Canceled, TimedOut, T>> TryWaitAsync(double timeoutMs, CancellationToken token = default)
    { return TryWaitAsync(TimeSpan.FromMilliseconds(timeoutMs), token); }

    public void Set(T result)
    { _tcs.TrySetResult(result); }

    public void Reset()
    {
        while (true)
        {
            var tcs = _tcs;
            if (!tcs.Task.IsCompleted
                || Interlocked.CompareExchange(ref _tcs, new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously), tcs) == tcs)
            { return; }
        }
    }

    public void SetReset(T result)
    {
        var oldTcs = Interlocked.Exchange(ref _tcs, new TaskCompletionSource<T>(TaskCreationOptions.RunContinuationsAsynchronously));
        oldTcs.TrySetResult(result);
    }
}
