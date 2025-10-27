using System;
using System.Reflection;
using System.Threading.Tasks;

namespace io.github.hatayama.uLoopMCP
{
    /// <summary>
    /// Utilities to await arbitrary awaitable objects using the C# awaitable pattern.
    /// Supports Task, Task<T>, ValueTask, ValueTask<T>, UniTask, UniTask<T> and any type
    /// implementing GetAwaiter/OnCompleted/GetResult.
    /// </summary>
    internal static class AwaitableHelper
    {
        public static async Task<object> AwaitIfNeeded(object value)
        {
            if (value == null)
            {
                return null;
            }

            Type valueType = value.GetType();

            // Task and Task<T>
            if (typeof(Task).IsAssignableFrom(valueType))
            {
                Task task = (Task)value;
                await task.ConfigureAwait(false);

                if (valueType.IsGenericType && valueType.GetGenericTypeDefinition() == typeof(Task<>))
                {
                    PropertyInfo resultProperty = valueType.GetProperty("Result");
                    if (resultProperty != null)
                    {
                        object taskResult = resultProperty.GetValue(value);
                        return taskResult;
                    }
                }

                return null;
            }

            // ValueTask and ValueTask<T> via AsTask if available
            if (IsValueTask(valueType))
            {
                Task asTask = ConvertValueTaskToTask(value);
                await asTask.ConfigureAwait(false);
                return null;
            }
            if (IsGenericValueTask(valueType))
            {
                Task asTask = ConvertGenericValueTaskToTask(value);
                await asTask.ConfigureAwait(false);

                Type[] genericArgs = valueType.GetGenericArguments();
                if (genericArgs != null && genericArgs.Length == 1)
                {
                    Type taskType = typeof(Task<>).MakeGenericType(genericArgs[0]);
                    PropertyInfo resultProperty = taskType.GetProperty("Result");
                    if (resultProperty != null)
                    {
                        object resultValue = resultProperty.GetValue(asTask);
                        return resultValue;
                    }
                }
                return null;
            }

            // Awaitable pattern fallback (e.g., UniTask/UniTask<T> or custom awaitables)
            MethodInfo getAwaiterMethod = valueType.GetMethod("GetAwaiter", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (getAwaiterMethod != null)
            {
                object awaiter = getAwaiterMethod.Invoke(value, null);
                if (awaiter == null)
                {
                    return value;
                }

                Type awaiterType = awaiter.GetType();
                PropertyInfo isCompletedProperty = awaiterType.GetProperty("IsCompleted", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
                MethodInfo getResultMethod = awaiterType.GetMethod("GetResult", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
                MethodInfo onCompletedMethod = awaiterType.GetMethod("OnCompleted", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(Action) }, null);
                MethodInfo unsafeOnCompletedMethod = awaiterType.GetMethod("UnsafeOnCompleted", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(Action) }, null);

                if (isCompletedProperty != null)
                {
                    bool isCompleted = (bool)isCompletedProperty.GetValue(awaiter);
                    if (isCompleted)
                    {
                        object completedResult = InvokeGetResultSafely(getResultMethod, awaiter);
                        return completedResult;
                    }
                }

                TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();

                Action continuation = () =>
                {
                    try
                    {
                        object continuationResult = InvokeGetResultSafely(getResultMethod, awaiter);
                        tcs.TrySetResult(continuationResult);
                    }
                    catch (TargetInvocationException tie)
                    {
                        Exception inner = tie.InnerException ?? tie;
                        tcs.TrySetException(inner);
                    }
                    catch (Exception ex)
                    {
                        tcs.TrySetException(ex);
                    }
                };

                if (unsafeOnCompletedMethod != null)
                {
                    unsafeOnCompletedMethod.Invoke(awaiter, new object[] { continuation });
                }
                else if (onCompletedMethod != null)
                {
                    onCompletedMethod.Invoke(awaiter, new object[] { continuation });
                }
                else
                {
                    // No completion registration; treat as completed
                    object immediateResult = InvokeGetResultSafely(getResultMethod, awaiter);
                    return immediateResult;
                }

                object awaited = await tcs.Task.ConfigureAwait(false);
                return awaited;
            }

            // Not awaitable; return as-is
            return value;
        }

        private static bool IsValueTask(Type type)
        {
            return type.FullName == "System.Threading.Tasks.ValueTask";
        }

        private static bool IsGenericValueTask(Type type)
        {
            if (!type.IsGenericType)
            {
                return false;
            }
            Type generic = type.GetGenericTypeDefinition();
            return generic.FullName == "System.Threading.Tasks.ValueTask`1";
        }

        private static Task ConvertValueTaskToTask(object valueTask)
        {
            Type type = valueTask.GetType();
            MethodInfo asTaskMethod = type.GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (asTaskMethod != null)
            {
                object taskObj = asTaskMethod.Invoke(valueTask, null);
                return (Task)taskObj;
            }

            // Fallback: build a Task from awaiter
            MethodInfo getAwaiter = type.GetMethod("GetAwaiter", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (getAwaiter == null)
            {
                return Task.CompletedTask;
            }

            object awaiter = getAwaiter.Invoke(valueTask, null);
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            MethodInfo onCompleted = awaiter.GetType().GetMethod("OnCompleted", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(Action) }, null);
            MethodInfo unsafeOnCompleted = awaiter.GetType().GetMethod("UnsafeOnCompleted", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(Action) }, null);
            MethodInfo getResult = awaiter.GetType().GetMethod("GetResult", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

            Action cont = () =>
            {
                try
                {
                    if (getResult != null)
                    {
                        getResult.Invoke(awaiter, null);
                    }
                    tcs.TrySetResult(null);
                }
                catch (TargetInvocationException tie)
                {
                    Exception inner = tie.InnerException ?? tie;
                    tcs.TrySetException(inner);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            };

            if (unsafeOnCompleted != null)
            {
                unsafeOnCompleted.Invoke(awaiter, new object[] { cont });
            }
            else if (onCompleted != null)
            {
                onCompleted.Invoke(awaiter, new object[] { cont });
            }
            else
            {
                tcs.TrySetResult(null);
            }

            return tcs.Task;
        }

        private static Task ConvertGenericValueTaskToTask(object valueTaskT)
        {
            Type type = valueTaskT.GetType();
            MethodInfo asTaskMethod = type.GetMethod("AsTask", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic);
            if (asTaskMethod != null)
            {
                object taskObj = asTaskMethod.Invoke(valueTaskT, null);
                return (Task)taskObj;
            }

            // Fallback through awaiter
            MethodInfo getAwaiter = type.GetMethod("GetAwaiter", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);
            if (getAwaiter == null)
            {
                return Task.CompletedTask;
            }

            object awaiter = getAwaiter.Invoke(valueTaskT, null);
            TaskCompletionSource<object> tcs = new TaskCompletionSource<object>();
            MethodInfo onCompleted = awaiter.GetType().GetMethod("OnCompleted", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(Action) }, null);
            MethodInfo unsafeOnCompleted = awaiter.GetType().GetMethod("UnsafeOnCompleted", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, new Type[] { typeof(Action) }, null);
            MethodInfo getResult = awaiter.GetType().GetMethod("GetResult", BindingFlags.Public | BindingFlags.Instance | BindingFlags.NonPublic, null, Type.EmptyTypes, null);

            Action cont = () =>
            {
                try
                {
                    if (getResult != null)
                    {
                        object _ = getResult.Invoke(awaiter, null);
                    }
                    tcs.TrySetResult(null);
                }
                catch (TargetInvocationException tie)
                {
                    Exception inner = tie.InnerException ?? tie;
                    tcs.TrySetException(inner);
                }
                catch (Exception ex)
                {
                    tcs.TrySetException(ex);
                }
            };

            if (unsafeOnCompleted != null)
            {
                unsafeOnCompleted.Invoke(awaiter, new object[] { cont });
            }
            else if (onCompleted != null)
            {
                onCompleted.Invoke(awaiter, new object[] { cont });
            }
            else
            {
                tcs.TrySetResult(null);
            }

            return tcs.Task;
        }

        private static object InvokeGetResultSafely(MethodInfo getResultMethod, object awaiter)
        {
            if (getResultMethod == null)
            {
                return null;
            }

            object result = getResultMethod.Invoke(awaiter, null);
            return result;
        }
    }
}


